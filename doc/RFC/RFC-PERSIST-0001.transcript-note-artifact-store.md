# RFC-PERSIST-0001 Transcript / Note / Artifact Store

ID：`RFC-PERSIST-0001`  
狀態：Accepted  
日期：2026-07-05  
關聯 WBS：`PERSIST-001`  
關聯 Test：`T-PERSIST-001`  
前置：`RFC-PRODUCT-0001`, `RFC-RUNTIME-0001`, `OPS-002`, `CLI-010`

## 背景

The product goal includes removing manual terminal-history copying. Each worker/session run must automatically persist enough evidence to reconstruct what prompt was sent, which PTCS messages were consumed, how the headless CLI was invoked, what stdout/stderr/final output was produced, what reply was sent and when the MessageFabric cursor became safe to ack.

Current evidence already exists:

- `E2E-002` writes prompt, PTCS batch, normalized request, rendered argv, stdout, stderr, final, result and manifest artifacts.
- `OPS-002` writes `session-boundary.json` after reply evidence and before MessageFabric ack.
- `RUNTIME-001` defines runtime as the owner of prompt-loop ordering and note/artifact persistence.
- `CLI-010` reserves `/notes <run-id>` and artifact rendering as client UX, but the storage policy is not yet explicit.

This RFC defines the transcript/note/artifact policy before runtime/actor implementation expands.

## 目標

1. Define the minimum persisted run evidence for every engine cycle.
2. Define private raw artifacts vs public/redacted export boundaries.
3. Define run note layout and how notes feed local compact without replacing raw artifacts.
4. Define manifest/reference rules used by MessageFabric replies, CLI, Web and future result vault.
5. Define verifier requirements for `misc/verifyTranscriptStore.fsx`.

## 非目標

1. 不在本 RFC 實作新的 storage provider。
2. 不決定最終 database/object-store/encrypted-store provider。
3. 不把 raw transcript 或 raw stdout/stderr commit 到 public repo。
4. 不以 local compact summary 取代完整 run evidence。
5. 不宣稱 volatile provider 已滿足 production crash-durable replay。

## 決策

### D1. Store ownership and default location

Runtime owns transcript/note/artifact write ordering. Host, actor, CLI and Web are adapters/clients.

Default file provider rules:

- `artifact.root` is explicit host config.
- Development verifier roots may live under gitignored `.codex.fs/`.
- Raw artifacts are private by default and must not be written under public repo tracked paths such as `doc/`, `log/`, `notes/` or source directories.
- Public exports are opt-in, redacted-only and must pass sensitive-text scan before commit/push.

### D2. Logical layout

Preferred file layout:

```text
<artifactRoot>/
  sessions/
    <session-id>/
      history.jsonl
      messagefabric-cursors.json
      compacted/
        <compact-id>.md
      runs/
        <run-id>/
          prompt.md
          ptcs-messages.jsonl
          request.json
          rendered-argv.json
          stdout.log
          stderr.log
          events.jsonl
          final.md
          result.json
          note.md
          redaction.json
          manifest.json
          session-boundary.json
```

`events.jsonl` is optional and only present when the selected engine supports structured events. `note.md` is a redacted human-readable summary, not the canonical raw transcript.

### D3. Minimum run evidence

Each engine run must persist:

| Evidence | Purpose |
| --- | --- |
| PTCS message/task identity | prove which MessageFabric messages or durable tasks entered the prompt |
| selected cursor | prove what will be acked after reply evidence |
| rendered prompt | reconstruct engine input |
| normalized request | reconstruct runtime/engine intent without shell parsing |
| rendered argv metadata | reconstruct CLI surface/version/options without secret values |
| process timing/outcome | started/completed, exit code, timeout/cancel/error |
| stdout/stderr | raw private artifact plus redacted display summary according to policy |
| final message | assistant final reply when available |
| event stream | structured JSONL when supported |
| result JSON | normalized outcome/result envelope |
| manifest | relative artifact refs, sha256, size, created UTC, engine/surface, PTCS refs |
| run note | redacted summary for humans, compact and UI/CLI browsing |
| ready-to-ack boundary | reply evidence and selected cursor written before MessageFabric ack |

### D4. Manifest and MessageFabric reply

The artifact manifest is the execution evidence index. MessageFabric reply bodies should contain a concise redacted summary and stable references, not raw transcript.

Reply body should include:

- run id;
- outcome;
- short final summary;
- artifact manifest reference;
- note reference when available;
- warning that raw artifacts may be private/local.

Durable result vault integration should store or reference the same manifest identity. Transport retry must not re-run completed backend work when the manifest/result identity already exists.

### D5. Run note policy

`note.md` is a redacted, human-readable run summary. It should include:

- session id, run id, engine/surface and outcome;
- consumed PTCS message ids and durable task/ticket ids when present;
- prompt summary, not necessarily full prompt;
- final answer summary or failure summary;
- open blockers / next actions when detectably present;
- artifact refs and manifest path;
- redaction hit count/summary, not matched secret values.

Run notes feed `CompactionEntry` values of kind `Run`, `Artifact`, `Decision`, `Blocker`, `OpenItem` or `Note`. Local compact can use notes and refs, but compacted history must preserve message ids, run ids and artifact refs. Compact summaries never overwrite raw private artifacts.

### D6. Redaction and export rules

Raw artifacts may contain user prompt text or engine output. They are private by default.

Rules:

- Do not dump environment variables.
- Do not record executable override values when they may contain secret material.
- Redact known high-risk token/key/private-key patterns before writing display summaries, MessageFabric replies, notes and public exports.
- High-risk findings block public export until removed/redacted or explicitly classified as false positive in an operation log.
- Public repo `notes/` is not the default artifact root. If a future export writes there, the changed notes must be scanned and committed according to repo harness rules.

### D7. Provider boundary

The first implementation can remain file-based, but runtime should depend on a provider-shaped boundary:

```fsharp
module CodexFs.Persistence

type RunEvidence =
    { SessionId: SessionId
      RunId: RunId
      PtcsMessages: PtcsMessageRef list
      PtcsTask: PtcsTaskRef option
      PromptMarkdown: string
      RequestJson: string
      RenderedArgvJson: string
      Stdout: string
      Stderr: string
      EventsJsonl: string option
      FinalMarkdown: string option
      ResultJson: string }

type PersistencePort =
    { WriteRunEvidence: RunEvidence -> Task<ArtifactManifest>
      WriteRunNote: ArtifactManifest -> string -> Task<ArtifactRef>
      WriteReadyToAckBoundary: RuntimeAckBoundary -> Task<ArtifactRef>
      AppendHistoryEntry: CompactionEntry -> Task<unit>
      LoadHistory: SessionId -> Task<CompactionEntry list> }
```

Concrete names may change during implementation, but the separation must remain: runtime calls a persistence port; host/actor owns concrete provider construction.

## 驗收

This RFC slice is accepted when:

1. SD §12/§13 record the transcript/note/artifact policy.
2. WBS row `PERSIST-001` links to `doc/WBS.PERSIST-001.md`.
3. Test row `T-PERSIST-001` is `Pass` for the RFC slice while future verifier remains planned.
4. DevLog/KM capture private/raw vs public/redacted boundaries.

Future implementation acceptance requires:

- `misc/verifyTranscriptStore.fsx` exists and runs against real runtime/host path;
- verifier writes prompt/stdout/stderr/final/result/manifest/note/session-boundary under a private artifact root;
- manifest contains relative refs with sha256/size/created UTC;
- MessageFabric reply contains redacted summary and manifest/note refs, not raw transcript;
- changed public exports fail when high-risk secret patterns remain;
- local compact input preserves message ids, run ids and artifact refs.

## 關聯文件

- `doc/RFC/RFC-PRODUCT-0001.codexfs-agent-runtime-reset.md`
- `doc/RFC/RFC-RUNTIME-0001.prompt-loop-package-boundary.md`
- `doc/WBS.PERSIST-001.md`
- `doc/WBS.md`
- `doc/Test.md`
- `doc/SD.md`
