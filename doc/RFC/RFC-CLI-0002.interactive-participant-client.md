# RFC-CLI-0002 Interactive Participant CLI Client

ID：`RFC-CLI-0002`  
狀態：Accepted  
日期：2026-07-05  
關聯 WBS：`CLI-010`  
關聯 Test：`T-CLI-010`  
前置：`RFC-PRODUCT-0001`, `RFC-RUNTIME-0001`, `RFC-ACTOR-0001`, `CLI-009`, `HOST-007`

## 背景

`codex.fs.cli` currently proves basic host/status/send/attach/drain paths, but the intended product needs a terminal surface that behaves closer to a normal interactive coding-agent CLI while still using PTCS participant communication. The user should not need to know a session id for first use; the default target is the Foreman `SessionActor` participant. When the user selects another worker or participant, the CLI changes the target identity, not the communication fabric.

Important existing constraints:

- PTCS `CommSpaMessageFabric` remains the canonical direct/public/group chat and inbox fact source.
- PTCS `CommSpaActorFabric` owns worker/session actor routing and sharding; CLI is not an actor fabric.
- `codex.fs.runtime` / actor behavior owns prompt assembly, local compact, headless engine invocation and artifact/note persistence.
- CLI must not write MessageFabric, artifact store or chat history directly unless it is explicitly invoking a host/PTCS API that owns the operation.

## 目標

1. Define `codex.fs.cli` as an interactive terminal participant client, not a one-shot diagnostics helper.
2. Keep first-use UX simple: prompt without session id targets Foreman / `agent.codexfs.foreman`.
3. Support explicit target switching: session id, worker participant id, direct participant id, group and public channel.
4. Support operator perspective: the CLI can display which participant/perspective is active, but it does not impersonate another participant unless an authorized host/PTCS policy grants that identity.
5. Define engine/model/reasoning/invocation option handoff to runtime/actor without CLI doing prompt loop work.
6. Define future installed-tool verifier requirements over real host/PTCS fabric.

## 非目標

1. 不在本 RFC 實作 interactive TUI/readline loop。
2. 不新增另一套 terminal chat store。
3. 不讓 CLI 直接呼叫 headless Codex/Agy engine 來取代 runtime/actor。
4. 不把 standalone `codex.fs.host` `/chat` 或 diagnostics form 當成 product chat UI。
5. 不以 fake/mock host 或 fake mailbox 驗收 CLI participant behavior。

## 決策

### D1. CLI role and identity

`codex.fs.cli` is a terminal participant client. It sends user intent through host/PTCS APIs and renders PTCS replies/artifact references.

Default identity rules:

| Concept | Default / rule |
| --- | --- |
| CLI package command | `codex.fs.cli` |
| Short alias command | `codex.fs` from `codex.fs.tool` |
| CLI sender participant | `user.codexfs.cli` until profile support lands |
| Future explicit sender | `--participant-id <user.*>` or a named local profile |
| First-use target | Foreman `SessionActor` |
| Foreman participant | `<ptcs.sessionParticipantPrefix>.foreman`, e.g. `agent.codexfs.foreman` |
| Worker override | exact worker participant id supplied by `--worker-id` / target switch |

CLI must display active sender, target and host URI in interactive mode so the operator can see whether they are talking to Foreman, a worker, a group or public channel.

### D2. Command surface

Existing commands remain valid:

```text
codex.fs.cli host status --host <advertiseUri>
codex.fs.cli session send --prompt @prompt.md --host <advertiseUri>
codex.fs.cli session send --session sess-001 --prompt @prompt.md --host <advertiseUri>
codex.fs.cli session send --session sess-001 --worker-id agent.codexfs.worker-001 --prompt @prompt.md --host <advertiseUri>
codex.fs.cli session status --session foreman --host <advertiseUri>
codex.fs.cli session attach --session foreman --host <advertiseUri>
codex.fs.cli session drain --session foreman --host <advertiseUri>
```

Future interactive mode should add a stable terminal loop without breaking one-shot commands:

```text
codex.fs.cli chat --host <advertiseUri>
codex.fs.cli chat --host <advertiseUri> --target foreman
codex.fs.cli chat --host <advertiseUri> --target agent.codexfs.worker.sess-001.plan
codex.fs.cli chat --host <advertiseUri> --public
codex.fs.cli chat --host <advertiseUri> --group codexfs.session.sess-001
```

Interactive meta commands are terminal UI commands, not shell commands:

```text
/whoami
/participants
/target foreman
/target agent.codexfs.worker.sess-001.plan
/public
/group codexfs.session.sess-001
/model gpt-5-codex --reasoning high
/engine agy
/runs
/artifacts <run-id>
/notes <run-id>
/exit
```

### D3. Communication path

The terminal loop maps user input to PTCS/host operations:

```text
terminal user input
  -> CLI resolves active sender/target/options
  -> host/PTCS API validates and registers sender if needed
  -> MessageFabric direct/public/group send
  -> Foreman/worker actor consumes inbox and calls runtime
  -> runtime persists prompt/stdout/stderr/final/artifacts/notes
  -> actor sends MessageFabric reply/reference
  -> CLI poll/wait renders reply/reference
```

The CLI may call host control APIs for convenience, but the logical message still belongs to MessageFabric. CLI must not create its own chat transcript as canonical truth.

### D4. Target and perspective switching

Target selection is explicit and reversible:

| CLI state | Meaning |
| --- | --- |
| `target = foreman` | send to default Foreman participant. |
| `target = session:<id>` | send to `<ptcs.sessionParticipantPrefix>.<id>`. |
| `target = participant:<id>` | direct send to exact participant id. |
| `target = worker:<id>` | exact worker participant id, equivalent to participant target. |
| `target = public` | public channel send, consumed only by actors whose policy includes public. |
| `target = group:<id>` | group send through MessageFabric group membership. |

Perspective is a read/render concern. Viewing from Foreman perspective means querying/rendering messages relevant to that participant when authorized; it does not mean the CLI may forge `agent.*` sender ids. Any impersonation or delegated send must be a separate authorized host/PTCS policy, logged and visible.

### D5. Invocation options

CLI can collect operator intent for engine/model/reasoning and headless CLI parameters, but runtime/actor interprets them:

| Option group | CLI responsibility | Runtime/actor responsibility |
| --- | --- | --- |
| engine | accept `codex` / `agy` / profile id and send as intent metadata | choose adapter/version and validate capability |
| model | accept model/profile text | map to engine-specific invocation where supported |
| reasoning effort | accept known values such as `xhigh`, `high`, `medium` | render supported argv or reject with readable diagnostic |
| workspace/add-dir/sandbox | accept structured options | enforce policy and render engine argv |
| prompt/history/compact | none beyond sending user text/options | assemble prompt, compact, persist and ack |

Versioned Codex/Agy DU rendering remains in engine adapters. CLI should expose options through Argu and host/API DTOs, not stringly-typed shell passthrough.

### D6. Error handling and readiness

CLI must fail closed and readable:

- refused/unreachable host returns non-zero with endpoint guidance, not an unhandled .NET stack trace;
- missing installed command is a deployment failure, not a user prompt problem;
- `--host` must use `control.advertiseUri`, not guessed process id or localhost-only endpoint in clustered profiles;
- command help and OpenAPI/SDK docs must agree on option names and examples;
- one-shot commands and interactive mode must share parsing and HTTP/PTCS client code to avoid behavior drift.

## 驗收

This RFC slice is accepted when:

1. SD §14 / §14.2 record the interactive CLI participant contract.
2. WBS row `CLI-010` links to `doc/WBS.CLI-010.md`.
3. Test row `T-CLI-010` is `Pass` for the RFC slice while future installed-tool verifier remains planned.
4. DevLog/KM capture that CLI defaults to Foreman, supports target/perspective switching by contract, and does not own prompt loop or chat truth.

Future implementation acceptance requires:

- installed `codex.fs.cli` and `codex.fs` commands run from `C:\Users\Administrator\.dotnet\tools` or an isolated tool path;
- real host/PTCS fabric is reachable through advertised URI;
- CLI registers/sends as `user.*` participant and can send to Foreman without session id;
- CLI can switch to exact worker participant, public channel and group where supported;
- CLI can wait/poll replies and render run/artifact/note references;
- `misc/verifyCliParticipantChat.fsx` verifies real installed tool behavior without fake/mock mailbox.

## 關聯文件

- `doc/RFC/RFC-PRODUCT-0001.codexfs-agent-runtime-reset.md`
- `doc/RFC/RFC-RUNTIME-0001.prompt-loop-package-boundary.md`
- `doc/RFC/RFC-ACTOR-0001.session-worker-actor-model.md`
- `doc/WBS.CLI-010.md`
- `doc/WBS.md`
- `doc/Test.md`
- `doc/SD.md`
