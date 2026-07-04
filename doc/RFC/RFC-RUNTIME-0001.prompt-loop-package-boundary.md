# RFC-RUNTIME-0001 Prompt Loop Package Boundary

ID：`RFC-RUNTIME-0001`  
狀態：Accepted  
日期：2026-07-05  
關聯 WBS：`RUNTIME-001`  
關聯 Test：`T-RUNTIME-001`  
前置：`RFC-PRODUCT-0001`

## 背景

`RFC-PRODUCT-0001` accepted that prompt assembly, history splice, local compact, headless CLI invocation, stdio capture, note/artifact persistence and recovery boundary belong to runtime/session worker behavior. They should not be implemented inside `codex.fs.host` HTTP route handlers.

Current alpha evidence already includes useful pieces:

- `CodexFs.PromptAssembly.assemble` is pure prompt construction.
- `CodexFs.Compaction` provides deterministic local compact.
- `CodexFs.SessionBehavior` is the intended pure session state/effect boundary.
- `CodexFs.Host.SessionEngineCycle.runSingleCycleAsync` proves a real bounded PTCS inbox -> Agy -> artifact -> reply -> ack path, but it lives under host and is not the durable sharded actor loop.

This RFC defines how those pieces become a reusable runtime boundary before actor and interactive UI work expand.

## 目標

1. Define `codex.fs.runtime` as a reusable prompt-loop orchestration package/namespace.
2. Keep runtime independent from HTTP route handlers, Web UI layout and Akka actor shell details.
3. Let host, actor and future verifiers call the same runtime contract.
4. Preserve real E2E evidence from `SessionEngineCycle` while marking it as a migration candidate.
5. Define acceptance gates for a future `misc/verifyRuntimePromptLoop.fsx` real-path verifier.

## 非目標

1. 不在本 RFC 實作 project split 或搬移 code。
2. 不定義完整 `WorkerActor` / `SessionActor` sharding protocol；那是 `ACTOR-001`。
3. 不定義 WebSharper bundle UX；那是 `WEB-001`。
4. 不決定最終 durable storage provider；`PERSIST-001` 會定義 transcript/note/artifact policy。
5. 不以 fake/mock mailbox 作為 runtime acceptance。

## 決策

### D1. Runtime owns orchestration, adapters own transport

Runtime is the owner of the ordered prompt loop:

```text
inbox batch + session state + history refs + policy
  -> select cursor and persist prompt boundary
  -> assemble prompt and compact history if needed
  -> build normalized RunRequest
  -> invoke engine through engine/process port
  -> persist stdout/stderr/final/events/result/manifest
  -> write note/summary reference
  -> emit reply intent and ready-to-ack boundary
```

Transport adapters own concrete send/poll/ack:

| Adapter | Owns |
| --- | --- |
| Host diagnostics/control | HTTP DTO validation, control response shape, OpenAPI docs, calling runtime/MessageFabric services. |
| PTCS adapter | `CommSpaMessageFabric` send/poll/wait/ack/drain and durable task mapping. |
| Actor adapter | delivery, sharding entity lifecycle, participant registration, runtime invocation and reply/ack ordering. |
| CLI/Web clients | user interaction, target participant selection, engine/model/options input and artifact query UX. |

### D2. Runtime contract uses ports/effects

The preferred F# shape is functional records and DUs:

```fsharp
module CodexFs.Runtime

type RuntimeCycleInput =
    { SessionState: SessionBehavior.SessionState
      InboxBatch: PtcsMessageRef list
      HistoryEntries: CompactionEntry list
      EngineSurface: EngineSurface
      WorkingDirectory: string
      ArtifactRoot: string
      Policy: RuntimePolicy }

type RuntimeEffect =
    | PersistPromptBoundary of RunId * PtcsMessageRef list
    | PersistHistoryEntries of CompactionEntry list
    | InvokeEngine of RunRequest
    | PersistRunArtifacts of RunResult
    | WriteRunNote of RuntimeNote
    | SendReply of RuntimeReplyIntent
    | PersistReadyToAckBoundary of RuntimeAckBoundary
    | AckInbox of cursor: string option

val decideCycle : RuntimeCycleInput -> RuntimePlan
val interpretCycleAsync : RuntimePorts -> RuntimePlan -> CancellationToken -> Task<RuntimeCycleResult>
```

`decideCycle` should be deterministic and unit-testable. `interpretCycleAsync` is the side-effect interpreter. The interpreter may live in `codex.fs.runtime` once ports are defined, but concrete PTCS/Akka/HTTP code stays outside runtime.

### D3. Side-effect ordering is part of the contract

The minimum ordering is:

1. Select and persist the consumed MessageFabric cursor/message ids before engine execution.
2. Persist the rendered prompt and normalized run request.
3. Invoke the selected engine through the engine adapter/process runner.
4. Persist stdout, stderr, events when available, final message, result JSON and manifest.
5. Write note/summary reference according to redaction policy.
6. Send reply intent through the caller adapter.
7. Persist ready-to-ack boundary with reply evidence.
8. Ack the MessageFabric cursor only after the boundary is durable for the selected profile.

This preserves `OPS-002` ordering while making it reusable outside host.

### D4. Migration path

| Current piece | Migration decision |
| --- | --- |
| `CodexFs.PromptAssembly` | Keep as pure core/runtime helper; runtime owns when and how it is called. |
| `CodexFs.Compaction` | Keep deterministic compact contract; runtime owns trigger and history selection. |
| `CodexFs.Host.SessionEngineCycle.runSingleCycleAsync` | Treat as bounded host-era implementation evidence. Move orchestration into runtime; keep a host wrapper for diagnostics/E2E if useful. |
| `HostControl` route handlers | Validate DTOs and call runtime/MessageFabric services only; no prompt/history implementation. |
| `misc/verifyMessageToEngineReply.fsx` | Remains E2E evidence; future runtime verifier should reuse real PTCS + installed engine path without fake mailbox. |

### D5. Runtime package split criteria

Create a physical `codex.fs.runtime` project when at least one is true:

- actor implementation needs runtime without referencing `codex.fs.host`;
- CLI/verifier needs prompt loop without host HTTP/Kestrel dependencies;
- Web/PTCS embedded host needs runtime contracts without standalone host tool dependency.

Until then, modules may remain in existing projects, but new code must follow the runtime boundary and avoid HTTP/actor-specific dependencies.

## 驗收

This RFC slice is accepted when:

1. `doc/SD.md` records runtime contract, ports/effects, ordering and migration rules.
2. `doc/WBS.md` row `RUNTIME-001` is complete as an RFC slice and does not claim code split is done.
3. `doc/Test.md` row `T-RUNTIME-001` is `Pass` for docs and keeps future verifier execution as a separate implementation gate.
4. `doc/DevLog.md` and `MCP.KM.md` capture the runtime boundary.

Future implementation acceptance requires:

- `misc/verifyRuntimePromptLoop.fsx` exists and runs real runtime/pure behavior plus real PTCS/installed engine integration where applicable;
- host route tests prove route handlers delegate instead of assembling prompts;
- actor tests prove `SessionActor` calls runtime rather than duplicating prompt logic.

## 關聯文件

- `doc/RFC/RFC-PRODUCT-0001.codexfs-agent-runtime-reset.md`
- `doc/WBS.RUNTIME-001.md`
- `doc/WBS.md`
- `doc/Test.md`
- `doc/SD.md`
