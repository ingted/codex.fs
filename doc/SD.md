# Software Design

版本：`0.2.0-draft`  
狀態：Draft  
對應文件：`doc/Requirement.md`, `doc/SA.md`

## 1. 設計目標

本設計將 `codex.fs` 拆成可逐步交付的 core contracts、host runtime、CLI client、engine adapters 與 PTCS fabric integration。初版實作應先完成 terminal-to-host-to-PTCS-MessageFabric-to-engine 的 real path，再擴充 PTCS Web UI。

設計取向：

- domain logic 使用 F# records、DU、functions。
- actor/message communication 使用 PTCS `CommSpaActorFabric` / `CommSpaMessageFabric`，不自行建立平行 fabric。
- argument parsing 使用 `FAkka.Argu`。
- CLI version differences 使用 module + adapter registry，不使用 runtime generic parser。
- host `--version` 保留為印出自身版本，不作 engine parser selection。
- public API 必須採 comment-as-SDK-doc policy；HTTP control surface 若存在，必須產生 OpenAPI/Swagger 文件。

## 2. Project layout

預期 solution layout：

```text
src/
  codex.fs/
    codex.fs.fsproj
  codex.fs.host/
    codex.fs.host.fsproj
  codex.fs.host.tool/
    codex.fs.host.tool.fsproj
  codex.fs.cli/
    codex.fs.cli.fsproj
  codex.fs.tool/
    codex.fs.tool.fsproj
  codex.fs.engine.codex/
    codex.fs.engine.codex.fsproj
  codex.fs.engine.agy/
    codex.fs.engine.agy.fsproj
  codex.fs.ptcs/
    codex.fs.ptcs.fsproj
tests/
  codex.fs.tests/
doc/
  Requirement.md
  SA.md
  SD.md
```

Package IDs:

| Package | Assembly namespace | Purpose |
| --- | --- | --- |
| `codex.fs` | `CodexFs` | Core engine/artifact/compaction contracts。 |
| `codex.fs.host` | `CodexFs.Host` | Referenceable host runtime/control library package。 |
| `codex.fs.host.tool` | `CodexFs.HostTool` | Thin dotnet tool wrapper; command name `codex.fs.host`。 |
| `codex.fs.cli` | `CodexFs.Cli` | Terminal client dotnet tool package；installed command name is `codex.fs.cli`。 |
| `codex.fs.tool` | `CodexFs.Tool` | Short alias dotnet tool package；installed command name is `codex.fs`，delegates to the same CLI command surface。 |
| `codex.fs.engine.codex` | `CodexFs.Engine.Codex` | Codex CLI adapter。 |
| `codex.fs.engine.agy` | `CodexFs.Engine.Agy` | Agy CLI adapter。 |
| `codex.fs.ptcs` | `CodexFs.Ptcs` | Thin integration over PTCS ActorFabric/MessageFabric。 |

There is no standalone `codex.fs.akka` fabric package in the initial design. PTCS owns the fabric.

`RFC-PRODUCT-0001` adds the following target split for upcoming work:

| Future package | Preferred purpose | Current migration rule |
| --- | --- | --- |
| `codex.fs.runtime` | Prompt/history assembly, local compact, headless invocation loop, stdio capture, notes/artifacts and recovery boundary. | New prompt-loop code should be written as reusable runtime modules, not inside HTTP routes. Existing bounded helpers under host must be treated as migration candidates. |
| `codex.fs.actor` | PTCS ActorFabric adapter with `WorkerActor`, specialized `SessionActor`, spawn/register/route protocol and durable delivery. | Actor code must depend on PTCS ActorFabric/MessageFabric contracts and may call runtime; it must not implement another mailbox fabric. |
| `codex.fs.web` | PTCS WebSharper AI chat bundle/extension such as `useAIChat(...)`. | Web UI must plug into PTCS Host/WebSharper and use the same PTCS hub/fabric. |
| `codex.fs.persistence` | Transcript/note/artifact provider boundary when file store and note policy outgrow core runtime. | Raw terminal transcript persistence must remain redacted and outside public repo by default. |
| `codex.fs.protocol` | Stable DTO/protocol package if CLI/Web/host need shared contracts without host dependency. | Split only when more than one public consumer needs DTOs without referencing host. |

## 3. Core domain model

```fsharp
module CodexFs.Domain

type SessionId = SessionId of string
type RunId = RunId of string

type PtcsMessageRef =
    { MessageId: string
      Cursor: string option
      FromParticipantId: string
      ToParticipantId: string option
      GroupId: string option
      CorrelationId: string option }

type PtcsTaskRef =
    { TaskId: string option
      TicketId: string option
      OperationId: string option
      TaskRealityId: string option
      ResultQueryHandle: string option }

type EngineKind =
    | Codex
    | Agy
    | Custom of string

type Capability =
    | SingleTurnHeadless
    | Continuation
    | StructuredEventStream
    | FinalMessageFile
    | WorkspaceDirectories
    | SandboxMode
    | ModelSelection
    | Timeout
    | LogFile

type EngineSurface =
    { Kind: EngineKind
      VersionText: string
      SurfaceId: string
      Capabilities: Set<Capability> }

type RunRequest =
    { RunId: RunId
      SessionId: SessionId
      Engine: EngineKind
      SurfaceId: string option
      WorkingDirectory: string
      PromptPath: string
      ArtifactDirectory: string
      Timeout: TimeSpan
      AdditionalDirectories: string list
      PtcsMessages: PtcsMessageRef list
      PtcsTask: PtcsTaskRef option
      Metadata: Map<string, string> }

type RunOutcome =
    | Completed
    | Failed
    | TimedOut
    | Cancelled

type RunResult =
    { RunId: RunId
      Outcome: RunOutcome
      ExitCode: int option
      StartedUtc: DateTimeOffset
      CompletedUtc: DateTimeOffset option
      ArtifactManifestPath: string
      FinalMessagePath: string option }
```

Core types reference PTCS identities as data, but do not depend on live PTCS runtime.

## 4. Engine adapter contract

```fsharp
module CodexFs.Engine

type EngineProbe =
    { ExecutablePath: string
      VersionText: string
      Surfaces: EngineSurface list }

type RenderedCommand =
    { FileName: string
      Arguments: string list
      WorkingDirectory: string
      Environment: Map<string, string option>
      RedactedDisplay: string }

type EngineAdapter =
    { Kind: EngineKind
      Probe: CancellationToken -> Task<EngineProbe>
      CanHandle: EngineSurface -> RunRequest -> bool
      Render: EngineSurface -> RunRequest -> RenderedCommand
      MapArtifacts: EngineSurface -> RunRequest -> RunResult -> Task<unit> }
```

設計規則：

- adapter owns CLI argv rendering。
- host owns process lifetime。
- adapter 不直接啟動 process。
- adapter 不應讀取 secret value 來產生 display string。
- `RenderedCommand.RedactedDisplay` 用於 log，不能包含敏感值。

Implemented process orphan recovery primitive:

```fsharp
module CodexFs.ProcessRunner

type ProcessLease =
    { ProcessId: int
      ProcessName: string
      StartedUtc: DateTimeOffset
      Marker: string }

type OrphanRecoveryOptions =
    { StartTimeTolerance: TimeSpan
      KillGracePeriod: TimeSpan }

type OrphanRecoveryOutcome =
    | NotRunning
    | LeaseMismatch
    | Terminated
    | TerminationFailed
    | RecoveryFailed of string

val recoverLeasedProcessAsync : OrphanRecoveryOptions -> ProcessLease -> Task<OrphanRecoveryResult>
```

Rules:

- Recovery must operate from a codex.fs-owned lease; it must not scan and kill arbitrary process names.
- A process is killable only when pid, process name and observed start time match the saved lease within tolerance.
- `Marker` is non-secret provenance stored with the lease/artifact; it is not a credential and must not contain user prompt text.
- `OPS-001` proves controlled orphan cleanup only; durable recovery ordering remains `OPS-002`.

## 5. Codex CLI adapter design

Codex CLI `0.142.x` surface module：

```fsharp
module CodexFs.Engine.Codex.V0_142.Exec

type SandboxMode =
    | ReadOnly
    | WorkspaceWrite
    | DangerFullAccess

type Args =
    { Prompt: string option
      Cd: string option
      AddDir: string list
      Sandbox: SandboxMode option
      Json: bool
      OutputLastMessage: string option
      OutputSchema: string option
      Model: string option
      Profile: string option
      SkipGitRepoCheck: bool
      ColorNever: bool }
```

Codex render policy:

- Use `codex exec`.
- Prefer stdin or prompt file content policy decided by host.
- Use `--json` when artifact capture requires structured events.
- Use `--output-last-message <artifact/final.md>`.
- Use `--cd <working-dir>` instead of relying on ambient process cwd when possible.

## 6. Agy CLI adapter design

Agy CLI `1.0.x` surface module：

```fsharp
module CodexFs.Engine.Agy.V1_0.Print

type Args =
    { Print: bool
      PromptAlias: bool
      PrintTimeout: TimeSpan option
      AddDirs: string list
      Project: string option
      NewProject: bool
      Conversation: string option
      Continue: bool
      Model: string option
      LogFile: string option
      Sandbox: bool
      DangerouslySkipPermissions: bool }
```

Agy render policy:

- Use `agy --print` or `agy --prompt` for single-turn headless execution.
- In Agy `1.0.16`, `--prompt` is a boolean alias for `--print`; prompt body delivery is handled by the run prompt artifact/stdin boundary, not by a `--prompt <text>` argv pair.
- Capture stdout/stderr as primary artifacts.
- Do not assume JSONL event stream unless future probe confirms support.
- If `--log-file` is used, set it under run artifact directory.

## 7. Versioned CLI DU policy

每個 CLI surface 使用獨立 module：

```text
CodexFs.Engine.Codex.V0_142.Exec
CodexFs.Engine.Agy.V1_0.Print
```

新增 CLI surface 時：

1. 新增 module。
2. 新增 DU/record args model。
3. 新增 probe matcher。
4. 新增 render tests。
5. 不修改舊 module 行為，除非修正 bug。

不建議設計：

```text
--version 0.142 -> runtime generic parse <CodexV0142Args>
```

原因：

- `--version` 應保留給 tool 自身版本。
- F# generic 不適合作 runtime parser dispatch。
- engine surface 可能由 CLI probe 得知，不應要求 user 手動輸入。

建議 CLI option：

```text
codex.fs run --engine codex --engine-surface codex-exec-0.142
codex.fs run --engine agy --engine-surface agy-print-1.0
```

`--engine-surface` optional；未指定時由 host probe 與 default policy 決定。

## 8. PTCS fabric integration design

`codex.fs.ptcs` should be a thin adapter over PTCS runtime types.

Expected PTCS contracts:

```fsharp
// From PulseTrade.Comm.Spa
type CommSpaMessageFabric
type CommSpaDurableMessageFabric
type CommSpaActorFabric
type DurableIngress
```

Integration records:

```fsharp
module CodexFs.Ptcs

type PtcsFabricRuntime =
    { Hub: obj
      MessageFabric: obj
      DurableMessageFabric: obj option
      ActorFabric: obj option
      DurableIngress: obj option }

type PtcsSessionBinding =
    { SessionId: SessionId
      ParticipantId: string
      ReplyParticipantId: string option
      GroupId: string option
      InboxLimit: int
      IncludePublic: bool
      IncludeGroups: bool }
```

Implementation should use concrete PTCS types in the PTCS package, while core stays independent.

Package/reference decision:

- First supported PTCS package: `PulseTrade.Comm.Spa [0.2.5-beta71]`.
- `codex.fs` core remains independent from PTCS runtime packages.
- `codex.fs.ptcs` is the thin integration package that references `PulseTrade.Comm.Spa [0.2.5-beta71]` and exposes compile-time boundary constants/types.
- `FAkka.Argu` is aligned to exact `[10.1.301]` in `codex.fs` because PTCS beta71 depends on `FAkka.Argu 10.1.301`.
- Evidence sources: `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\PulseTrade.Comm.Spa.fsproj` declares `0.2.5-beta71`; `G:\PulseTrade.fs\Libs\PulseTrade.Comm\src\PulseTrade.Comm.Spa.Host\PulseTrade.Comm.Spa.Host.fsproj` consumes exact `[0.2.5-beta71]`; PTC verification docs record beta71 as the current Host/GW/RN aligned package baseline.
- No `nuget.config` is required. Restore must resolve from configured NuGet sources/global cache/library-packs.

Message operations map to PTCS:

| codex.fs operation | PTCS API |
| --- | --- |
| register session participant | `CommSpaMessageFabric.RegisterParticipantAsync` |
| send prompt/reply | `SendAsync` |
| poll/wait pending inbox | `PollInboxAsync` / `WaitInboxAsync` |
| ack processed batch | `AckAsync` |
| drain for terminal attach | `DrainInboxAsync` |
| durable agent task | `CommSpaDurableMessageFabric.SubmitAgentTaskDurableAsync` |

Implemented MVP wrapper:

```fsharp
module CodexFs.Ptcs.MessageFabricBinding

type SessionBinding =
    { ParticipantId: string
      ReplyParticipantId: string option
      GroupId: string option
      InboxLimit: int
      IncludePublic: bool
      IncludeGroups: bool }

val registerParticipantAsync :
    CommSpaMessageFabric -> SessionBinding -> ParticipantRegistration -> Task<RegisterParticipantReply>

val sendAsync :
    CommSpaMessageFabric -> OutboundMessage -> Task<MessageFabricEnvelope>

val pollInboxAsync :
    CommSpaMessageFabric -> SessionBinding -> MessageFabricCursor -> Task<MessageFabricInboxBatch>

val waitInboxAsync :
    CommSpaMessageFabric -> SessionBinding -> MessageFabricCursor -> TimeSpan -> TimeSpan -> CancellationToken option -> Task<MessageFabricInboxBatch>

val tryUpsertConfiguredGroupAsync :
    CommSpaMessageFabric -> SessionBinding -> Task<MessageFabricGroupView option>

val ackInboxAsync :
    CommSpaMessageFabric -> SessionBinding -> MessageFabricCursor -> Task<MessageFabricAckResult>

val drainInboxAsync :
    CommSpaMessageFabric -> SessionBinding -> MessageFabricCursor -> Task<MessageFabricInboxBatch>
```

The wrapper is intentionally stateless. Cursor truth remains PTCS MessageFabric.

Implemented durable handoff wrapper:

```fsharp
module CodexFs.Ptcs.DurableMessageFabricBinding

type DurableFabric =
    { Ingress: DurableIngress
      Fabric: CommSpaDurableMessageFabric }

type DurableProviderProof =
    { Mode: DurableIngressMode
      ProfileId: string
      IsCrashDurable: bool
      SupportsDeliveryRetry: bool
      PendingCount: int
      DeadLetterCount: int
      ImplementationKind: string
      MissingRequirements: string list
      SatisfiesShardedDeliveryProvider: bool }

type DurableAgentTask =
    { AgentTaskId: string
      ParentRequestId: string option
      FromParticipantId: string
      ToParticipantId: string
      Intent: string
      Body: string
      ContentType: string option
      Tags: string list
      ReplyToParticipantId: string option
      CorrelationId: string option
      OperationId: string option
      IdempotencyKey: string option
      EntityId: string option
      VaultProfileId: string option
      ResultMaxBytes: int64 option
      CreatedAtUtc: DateTimeOffset option
      DeadlineAtUtc: DateTimeOffset option }

val createVolatileDurableFabric : unit -> DurableFabric
val createDurableFabric : CommHub -> DurableIngress -> DurableFabric
val volatileProviderProofAsync : DurableFabric -> CancellationToken -> Task<DurableProviderProof>
val registerParticipantAsync : DurableFabric -> SessionBinding -> ParticipantRegistration -> Task<MessageFabricDurableResult<RegisterParticipantReply>>
val submitAgentTaskAsync : DurableFabric -> DurableAgentTask -> Task<MessageFabricAgentTaskAccepted>
val pollInboxAsync : DurableFabric -> SessionBinding -> MessageFabricCursor -> Task<MessageFabricInboxBatch>
val ackInboxAsync : DurableFabric -> SessionBinding -> MessageFabricCursor -> Task<MessageFabricDurableResult<MessageFabricAckResult>>
val queryTicketAsync : DurableFabric -> string -> CancellationToken -> Task<DurableTaskStatus>
```

Rules:

- `createVolatileDurableFabric` uses PTCS `CommSpaDurableIngress.createVolatile` and `CommSpaMessageFabric.createDurable`; this is real PTCS durable admission, not a fake mailbox。
- Volatile provider proof must fail closed for production sharded delivery requirements. `DurableProviderProof.SatisfiesShardedDeliveryProvider = false` and `MissingRequirements` includes provider-specific sharding delivery runtime requirements。
- `submitAgentTaskAsync` maps codex.fs task fields to PTCS `MessageFabricAgentTaskEnvelope` and calls `CommSpaDurableMessageFabric.SubmitAgentTaskDurableAsync`。
- The returned `MessageFabricAgentTaskAccepted` is an admission/delivery ticket and target inbox message reference. It does not mean the worker has executed the task or persisted codex.fs artifacts。
- Crash-durable restart/readback, task result vault retention, and ack-after-artifact/reply recovery ordering remain `OPS-002` / future selected provider profile scope。

Actor operations map to PTCS:

| codex.fs operation | PTCS API |
| --- | --- |
| package-owned local fabric | `CommSpaActorFabric.startWithOptions` |
| caller-owned region attachment | `CommSpaActorFabric.requiredConfig` + `attachRegionToSystem` |
| caller-owned proxy attachment | `CommSpaActorFabric.requiredConfig` + `attachProxyToSystem` |
| fabric health | `CommSpaActorFabric.HealthAsync` and non-secret health properties |

## 9. Host service design

```fsharp
module CodexFs.Host

type HostConfig =
    { ArtifactRoot: string
      DefaultEngine: EngineKind
      EnabledEngines: EngineKind list
      EngineExecutableOverrides: Map<EngineKind, string>
      DefaultTimeout: TimeSpan
      MaxPendingMessagesPerTurn: int
      Compaction: CompactionPolicy
      Redaction: RedactionPolicy
      ControlEndpoint: HostControlEndpointConfig
      ApiDocs: ApiDocsConfig
      Ptcs: PtcsHostConfig }

type HostControlEndpointConfig =
    { Protocol: string
      BindAddress: string
      Port: int option
      AdvertiseUri: string
      AllowLoopbackOnly: bool }

type ApiDocsConfig =
    { GenerateXmlDocs: bool
      GenerateOpenApi: bool
      ExposeSwaggerUi: bool
      SwaggerRoutePrefix: string option
      IncludeExamples: bool }

type PtcsHostConfig =
    { FabricMode: string
      SessionParticipantPrefix: string
      ReplyParticipantId: string option
      DurableAgentTasks: bool
      DefaultInboxLimit: int }
```

Implemented MVP config loader:

```fsharp
module CodexFs.HostConfig

val defaults : HostConfig

val loadFromMap : Map<string, string> -> HostConfigLoadResult

type HostConfigLoadResult =
    { Config: HostConfig option
      Issues: HostConfigIssue list
      Diagnostics: HostConfigDiagnostic list }

type HostConfigDiagnostic =
    { Key: string
      Value: string
      WasRedacted: bool }
```

Accepted setting keys are case-insensitive and normalized internally. MVP keys include:

| Area | Keys |
| --- | --- |
| artifact | `artifact.root` |
| engine | `engine.default`, `engine.enabled`, `engine.codex.executable`, `engine.agy.executable`, `timeout.default` |
| session turn | `message.maxPendingPerTurn` |
| control endpoint | `control.protocol`, `control.bindAddress`, `control.port`, `control.advertiseUri`, `control.allowLoopbackOnly` |
| API docs | `apiDocs.generateXmlDocs`, `apiDocs.generateOpenApi`, `apiDocs.exposeSwaggerUi`, `apiDocs.swaggerRoutePrefix`, `apiDocs.includeExamples` |
| PTCS | `ptcs.fabricMode`, `ptcs.sessionParticipantPrefix`, `ptcs.replyParticipantId`, `ptcs.durableAgentTasks`, `ptcs.defaultInboxLimit` |
| policies | `compaction.maxSummaryChars`, `compaction.recentEntryCount`, `compaction.maxEntryTextChars`, `redaction.enableHighRiskRules` |

Validation rules:

- fatal parse/validation issue returns `Config = None`。
- diagnostics are redacted with core `Redaction.redactHighRisk` and must not echo raw token-like values。
- `AllowLoopbackOnly = true` permits single-node development loopback config。
- `AllowLoopbackOnly = false` rejects loopback bind or advertised URI; clustered/production hosts must advertise a routable address。

```fsharp

type HostCommand =
    | CreateSession of CreateSessionRequest
    | SubmitMessage of SubmitMessageRequest
    | CancelRun of RunId
    | GetSessionStatus of SessionId
    | GetRunManifest of RunId

type HostReply =
    | SessionCreated of SessionId
    | MessageAccepted of string
    | RunAccepted of RunId
    | SessionStatus of SessionStatus
    | RunManifest of ArtifactManifest
    | HostError of HostError
```

Host responsibilities:

- load config。
- initialize PTCS fabric runtime。
- initialize engine registry。
- initialize artifact store。
- start session workers。
- expose control endpoint for CLI/Web/admin callers。
- expose Swagger UI only when the selected host control endpoint is HTTP and the active profile allows it。
- compose and call runtime/actor services; do not own canonical prompt/history assembly, local compact, stdio note persistence or worker orchestration semantics。

Implemented minimal runtime package:

```fsharp
namespace CodexFs.Host

module HostRuntime

type HostRuntime =
    { Config: HostConfig
      ConfigDiagnostics: HostConfigDiagnostic list
      Status: HostRuntimeStatus
      StartedUtc: DateTimeOffset option
      MessageFabric: CommSpaMessageFabric option }

type HostHealth =
    { Status: HostRuntimeStatus
      DefaultEngine: string
      EnabledEngines: string list
      ControlAdvertiseUri: string
      PtcsFabricMode: string
      HasMessageFabric: bool
      MessageFabricType: string option
      EngineOverrideKeys: string list
      RedactedDiagnostics: HostConfigDiagnostic list }

val tryCreateFromLoadResult : HostConfigLoadResult -> Result<HostRuntime, HostConfigIssue list>
val startWithMessageFabric : DateTimeOffset -> CommSpaMessageFabric -> HostRuntime -> HostRuntime
val startInProcessMessageFabric : DateTimeOffset -> HostRuntime -> HostRuntime
val health : HostRuntime -> HostHealth
val healthSummary : HostRuntime -> string
val stop : HostRuntime -> HostRuntime
```

Rules:

- `startWithMessageFabric` starts the runtime with a caller-owned PTCS `CommSpaMessageFabric`. PTCS Host integration should use this seam so browser-visible participants and worker/session participants share the same hub/fabric。
- `startInProcessMessageFabric` initializes a real PTCS `CommSpaMessageFabric` through `codex.fs.ptcs`; it is not an alternate mailbox and the current minimal slice does not create an ActorSystem。
- `health` and `healthSummary` expose non-secret operational metadata and redacted config diagnostics only。
- executable override values are omitted from health; only engine override keys are shown。
- HTTP listener, endpoint DTOs and Swagger exposure remain `HOST-003` / `DOC-003` scope。
- `local` / `in-process` means node-local object ownership only; it must not imply a `127.0.0.1` ActorSystem contract。
- PTCS ActorSystem / sharded cluster setup is outside this in-process MessageFabric slice; when `HostRuntime` wires `CommSpaActorFabric`, Akka remoting/sharding bind and canonical advertise host must come from the cluster profile and be reachable by peer nodes, such as LAN IP or DNS, not `127.0.0.1` / `localhost`。
- `127.0.0.1` is allowed only for explicitly selected single-node development profiles where no cross-node actor/session communication is expected。
- Prompt assembly, history splice, local compact, engine invocation and stdio/note persistence belong to `codex.fs.runtime` / `SessionBehavior` / worker actor behavior. `HostControl` route handlers may validate input, call runtime services and return DTOs only.

Host control endpoint decision:

- MVP protocol is HTTP.
- Single-node development may bind to loopback only when `AllowLoopbackOnly = true`.
- Production and clustered profiles must bind to a LAN or otherwise routable address and publish an `AdvertiseUri` reachable by other nodes/clients.
- The advertised URI, not `localhost`, is the address other nodes and CLI clients use.
- HTTP is control plane only. Actor/session collaboration and message truth remain PTCS `CommSpaActorFabric` / `CommSpaMessageFabric`.
- Swagger/OpenAPI documents the HTTP control surface, not MessageFabric as a separate transport.

Implemented HTTP control package surface:

```fsharp
namespace CodexFs.Host

module HostControl

module Routes =
    val Root : string // "/"
    val LegacyChat : string // "/chat"
    val DiagnosticsSessionSend : string // "/diagnostics/session-send"
    val Health : string // "/api/codexfs/host/health"
    val SessionMessages : string // "/api/codexfs/session/{sessionId}/messages"
    val ForemanMessages : string // "/api/codexfs/foreman/messages"

type HostControlContract =
    { Protocol: string
      BindAddress: string
      Port: int
      BindUri: string
      AdvertiseUri: string
      DiagnosticsSessionSendUri: string
      HealthUri: string
      OpenApiJsonUri: string
      SwaggerUiUri: string
      AllowLoopbackOnly: bool
      GenerateOpenApi: bool
      ExposeSwaggerUi: bool
      Endpoints: HostControlEndpointDefinition list }

type HostControlHealthResponse =
    { Status: string
      DefaultEngine: string
      EnabledEngines: string list
      ArtifactRoot: string
      ControlProtocol: string
      BindAddress: string
      Port: int
      AdvertiseUri: string
      DiagnosticsSessionSendUri: string
      HealthUri: string
      AllowLoopbackOnly: bool
      PtcsFabricMode: string
      PtcsSessionParticipantPrefix: string
      PtcsDefaultInboxLimit: int
      DurableAgentTasks: bool
      HasMessageFabric: bool
      MessageFabricType: string
      StartedUtc: string
      EngineOverrideKeys: string list
      Warnings: string list
      Diagnostics: HostControlDiagnosticResponse list }

val buildContract : HostConfig -> HostControlContract
val healthResponse : HostControlContract -> HostRuntime -> HostControlHealthResponse
val chatPostAsync : HostRuntime -> HostControlContract -> HttpRequest -> Task<IResult> // diagnostics form handler
val tryStartAsync : DateTimeOffset -> CancellationToken -> HostRuntime -> Task<Result<HostControlServer, HostConfigIssue list>>
val stopAsync : CancellationToken -> HostControlServer -> Task<HostRuntime>
```

Rules:

- `tryStartAsync` starts a real Kestrel HTTP listener using `control.bindAddress` and `control.port`, and exposes `GET /`, `GET /chat`, `GET/POST /diagnostics/session-send`, CLI send/read endpoints, plus `GET /api/codexfs/host/health`.
- `GET /` is the operator landing page for control-only mode. It must return HTTP 200 HTML and link to diagnostics, health, OpenAPI JSON and Swagger UI when those docs endpoints are enabled; it must state control-only mode is not the product chat UI.
- `GET /chat` in control-only mode is a legacy guard/redirect page only. It must not present a prompt composer. It points operators to the canonical PTCS Web chat and may link to diagnostics.
- `GET /diagnostics/session-send` is the standalone diagnostics form. `POST /diagnostics/session-send` accepts `application/x-www-form-urlencoded` fields `sessionId`, `workerId`, and `prompt`, then sends through `acceptSessionMessageAsync`.
- Diagnostics `sessionId` is optional; blank defaults to `foreman`. This keeps first-use diagnostics and CLI aligned with the default package foreman.
- `POST /api/codexfs/foreman/messages` is the CLI default route when the caller does not know a session id. It sends to session id `foreman`, deriving target participant `<ptcs.sessionParticipantPrefix>.foreman` unless `workerId` overrides it.
- `POST /api/codexfs/session/{sessionId}/messages` remains the explicit existing-session route.
- Standalone diagnostics and CLI endpoints must not create a parallel durable chat store; production browser chat still uses caller-owned PTCS MessageFabric / ActorFabric from the PTCS Host process or a peer PTCS cluster node.
- `HostControlContract.HealthUri` is built from `control.advertiseUri`; CLI/Web/admin callers must use the advertised URI, not the bind URI when these differ.
- Non-loopback clustered profiles are validated by `HostConfig`; `control.allowLoopbackOnly = false` rejects loopback bind/advertise config before HTTP start.
- The health endpoint returns non-secret operational metadata only. It reports executable override keys but never executable override values.
- Starting the HTTP control endpoint may initialize the in-process PTCS MessageFabric via `HostRuntime`; this HTTP slice still does not create an ActorSystem and does not become a MessageFabric transport.
- Future ActorSystem initialization belongs to the PTCS ActorFabric/session-worker slice and must use the same non-loopback cluster profile rules as above.
- Endpoint definitions include success/failure examples and typed response metadata so `DOC-003` / `DOC-004` can expose generated OpenAPI JSON and Swagger UI without hand-written YAML.

`RFC-WEB-0002` adds a separate product Web profile:

- `control-only` mode is the current HTTP control/diagnostics surface and is never product chat.
- `ptcs-webshell` mode is the only acceptable product Web profile for `codex.fs.host.tool`.
- `ptcs-webshell` must host or compose PTCS classic `/chat` shell with nav tabs, participant list, thread/session area and composer.
- `ptcs-webshell` registers codex.fs AI WebSharper Bundle and shares the same PTCS `CommHub`, `CommSpaMessageFabric` and ActorFabric profile as worker participants.
- If an external PTCS Host process already owns the shell, codex.fs integrates through package/bundle/actor registration instead of starting a separate browser chat.

Host standalone tool contract:

- `codex.fs.host` command is provided by package `codex.fs.host.tool`; the existing `codex.fs.host` package remains a normal referenceable NuGet library package.
- The tool is a thin wrapper over `HostConfig`, `HostRuntime`, and `HostControl`; it must not fork a second host protocol.
- Argument parsing uses `FAkka.Argu`.
- Commands:
  - `codex.fs.host status --setting <key=value>` loads config and prints non-secret local runtime health without binding an HTTP listener.
  - `codex.fs.host start --setting <key=value> [--run-seconds <n>]` starts the real HTTP control endpoint through `HostControl.tryStartAsync`.
- `--run-seconds` is for bounded automation and verification; omitting it runs until Ctrl+C.
- Clustered/non-dev usage must set `control.bindAddress`, `control.port`, `control.advertiseUri`, and `control.allowLoopbackOnly=false` with a LAN/DNS-reachable advertised URI. Loopback remains dev-only.
- Handoff to a user must run from an installed global tool or an isolated tool path. Do not leave a long-running `dotnet run` process over `bin/Debug` as the handed-off host because it can lock build outputs.
- Global tool handoff must verify `C:\Users\Administrator\.dotnet\tools\codex.fs.cli.exe --help`, `C:\Users\Administrator\.dotnet\tools\codex.fs.exe --help`, `codex.fs.cli --help`, `codex.fs --help`, and `C:\Users\Administrator\.dotnet\tools\codex.fs.host.exe --help` before claiming CLI/tool availability.
- The tool does not wire durable task handoff into the host worker loop, does not implement process lease persistence, and does not initialize an ActorSystem; those remain `OPS-002` / future host-worker slices.

## 10. API documentation / SDK docs design

API documentation is part of the implementation contract, not a post-processing task.

Documentation sources:

| Surface | Required documentation source | Generated output |
| --- | --- | --- |
| Public F# modules/types/functions | XML doc comments (`///`) with summary/remarks/param/returns/example where applicable | NuGet SDK docs / generated reference site |
| Host HTTP control endpoints | typed DTOs + endpoint metadata + XML comments | OpenAPI v3 document and Swagger UI |
| CLI commands | `FAkka.Argu` DU metadata + command examples | CLI help text and docs snippets |
| PTCS integration operations | SD mapping table + API comments on adapter functions | SDK docs and integration guide |

Tooling decision:

- OpenAPI JSON MVP: ASP.NET Core HTTP host with `Microsoft.AspNetCore.OpenApi` (`AddOpenApi` / `MapOpenApi`) plus XML comments and typed endpoint metadata.
- Swagger UI MVP: `Swashbuckle.AspNetCore.SwaggerUi` may be used as UI assets over the generated OpenAPI document when the host profile allows it; it is not the source of truth.
- OpenAPI future candidates: NSwag may be evaluated later for client generation.
- SDK reference docs: use F# XML documentation output as the canonical source; FSharp.Formatting/fsdocs is the preferred first reference-site generator for F# APIs, while DocFX remains an optional cross-language/site evaluation.
- Examples: keep examples close to the API through XML `<example>` blocks or doc-tested snippets when tooling supports it.

Rules:

- Do not maintain a hand-written Swagger YAML as the source of truth when the host endpoint can generate OpenAPI from typed contracts.
- Public API comments must document behavior, failure modes, idempotency expectations, security/redaction notes, and PTCS MessageFabric side effects.
- Request DTO docs must describe every field, default, validation rule, and whether a value is persisted, redacted, or sent through MessageFabric.
- Response DTO docs must describe outcome states, nullable fields, artifact references, and error semantics.
- Every endpoint or SDK function added by a WBS item must include at least one success example and one meaningful failure/error example unless the API is internal-only.
- Swagger UI must not expose secret values. Production exposure should be disabled by default or guarded by the host deployment profile.
- API examples must use non-secret sample values and must not include real local paths unless the example is explicitly marked local-only.

WBS definition of done for API-facing items:

- XML doc comments are updated for all new or changed public types, DU cases, record fields, modules and functions.
- Host HTTP endpoints, if changed, have updated OpenAPI metadata and Swagger-visible examples.
- Parameters and outputs are documented in code comments and in any affected SD/Test/WBS detail rows.
- Tests or verification commands cover generated OpenAPI availability when an HTTP host is part of the slice.
- Breaking API changes update migration notes before the package/tool is published.

Implemented HTTP docs routes:

- Package references in `codex.fs.host`:
  - `Microsoft.AspNetCore.OpenApi [10.0.9]`.
  - `Microsoft.OpenApi [2.7.5]` direct override to avoid GHSA-v5pm-xwqc-g5wc affected 2.0.x transitive versions.
  - `Swashbuckle.AspNetCore.SwaggerUI [10.2.3]`.
- Operator landing page route: `GET /`, returning HTML with links to health, OpenAPI JSON and Swagger UI when enabled.
- OpenAPI JSON route: `GET /openapi/v1.json`, mapped through `MapOpenApi("/openapi/{documentName}.json")` when `apiDocs.generateOpenApi = true`.
- Swagger UI route: `/<apiDocs.swaggerRoutePrefix>/index.html`, enabled only when both `apiDocs.generateOpenApi = true` and `apiDocs.exposeSwaggerUi = true`.
- Test profile uses `apiDocs.swaggerRoutePrefix = docs`, therefore the advertised UI URI is `<control.advertiseUri>/docs/index.html`.
- Root, OpenAPI JSON and Swagger UI must be verified through `HostControlContract.AdvertiseUri` / `OpenApiJsonUri` / `SwaggerUiUri`, not through localhost-only URLs.
- User-facing host release evidence must include browser or Playwright verification for root and Swagger UI, plus a JSON/OpenAPI check proving expected paths are present.

## 11. Session behavior design

Domain behavior should be testable without Akka and without live PTCS process. After `RFC-PRODUCT-0001`, this section is the runtime prompt-loop contract, not a host route contract. `codex.fs.host` may host the implementation; actor shells and CLI verifiers should be able to call the same runtime behavior.

```fsharp
module CodexFs.SessionBehavior

type SessionState =
    { SessionId: SessionId
      ParticipantId: string
      Status: SessionStatus
      HistoryPath: string
      LastCursor: string option
      ActiveRun: RunId option
      CurrentSummaryPath: string option }

type SessionCommand =
    | InboxBatchReceived of PtcsMessageRef list
    | EngineRunCompleted of RunResult
    | EngineRunFailed of RunResult
    | Tick

type SessionEffect =
    | PersistMessageBatch of PtcsMessageRef list
    | StartRun of RunRequest
    | PersistRunResult of RunResult
    | SendMessageFabricReply of body: string * tags: string list
    | AckMessageFabricCursor of cursor: string option
    | CompactHistory

let decide (state: SessionState) (command: SessionCommand) : SessionState * SessionEffect list =
    failwith "design placeholder"
```

Akka shell, if needed, is an adapter around this behavior and PTCS ActorFabric. It must not introduce a second persistent truth for messages.

### 11.1 Local compaction design

`SESS-003` resolves `SD-TBD-003` for the MVP: local compaction uses a deterministic rule-based compactor in `codex.fs` core. It does not call the selected engine and does not require a dedicated LLM adapter. A future engine-backed or dedicated-adapter compactor may reuse the same input/output contract after the host has durable artifacts and PTCS binding in place.

Core contract:

```fsharp
module CodexFs.Compaction

type CompactionEntryKind =
    | Message
    | Decision
    | Blocker
    | OpenItem
    | Run
    | Artifact
    | Note

type CompactionEntry =
    { EntryId: string
      Kind: CompactionEntryKind
      Text: string
      MessageRefs: PtcsMessageRef list
      RunRefs: RunId list
      ArtifactRefs: string list
      Tags: string list
      CreatedUtc: DateTimeOffset option }

type CompactionPolicy =
    { MaxSummaryChars: int option
      RecentEntryCount: int
      MaxEntryTextChars: int option
      PreserveKinds: Set<CompactionEntryKind> }

type DroppedEntryReason =
    | NotRecent
    | Budget

type DroppedEntry =
    { EntryId: string
      Reason: DroppedEntryReason }

type CompactionResult =
    { SummaryMarkdown: string
      PreservedEntryIds: string list
      DroppedEntries: DroppedEntry list
      PreservedMessageRefs: PtcsMessageRef list
      PreservedRunRefs: RunId list
      PreservedArtifactRefs: string list
      OverBudget: bool }
```

Retention rules:

- `Decision`, `Blocker`, `OpenItem`, `Run`, and `Artifact` entries are retained by default.
- Any entry carrying `PtcsMessageRef`, `RunId`, or artifact reference is retained even if its kind is `Note` or `Message`.
- Recent non-critical entries are retained by `RecentEntryCount`.
- Summary body text may be truncated per entry, but retained refs and ids must remain visible.
- `MaxSummaryChars` is a soft budget for non-critical recent entries. Mandatory retained content may exceed the budget and sets `OverBudget = true`; it must not silently drop blockers, decisions, open items, message ids, run ids, or artifact refs.

### 11.2 Worker actor model target

`RFC-ACTOR-0001` defines this model for future actor implementation:

```text
WorkerActor
  - register/refresh PTCS participant
  - consume direct/public/group or actor-routed work
  - call codex.fs runtime
  - persist transcript/artifacts/notes
  - send MessageFabric reply/result reference
  - coordinate child workers when policy allows

SessionActor : WorkerActor
  - stable Foreman/session participant identity
  - default human target from CLI/Web
  - session history/compaction policy owner
  - spawns/registers worker participants
```

Default identity mapping:

| Concept | Default |
| --- | --- |
| Foreman entity id | `foreman` |
| Foreman participant id | `<ptcs.sessionParticipantPrefix>.foreman`, e.g. `agent.codexfs.foreman` |
| Session entity id | stable sanitized session id |
| Session participant id | `<ptcs.sessionParticipantPrefix>.<sessionEntityId>` |
| Child worker participant id | configurable prefix, default `<ptcs.sessionParticipantPrefix>.worker.<sessionEntityId>.<workerId>` |

MessageFabric scope policy:

| Actor | Direct | Public | Group |
| --- | --- | --- | --- |
| Foreman SessionActor | yes | yes, policy-controlled | yes, session/control groups |
| SessionActor | yes | optional by policy | yes, session group |
| Child worker | yes | optional by policy | yes, assigned task/session groups |

Rules:

- `SessionActor` is a specialized `WorkerActor`; it may call runtime itself or spawn child workers.
- Actors register/refresh PTCS participants with `Kind = Some "agent"` where supported.
- First-use CLI/Web prompt without session id targets Foreman; explicit worker id targets the exact worker participant id.
- Actor shells call `CodexFs.Runtime` and do not duplicate prompt assembly, compaction or artifact/note persistence logic.
- Stateful external request chains must use `Akka.Delivery` or `Akka.Cluster.Sharding.Delivery` for the selected profile.
- Delivery confirm and MessageFabric ack happen only after runtime has persisted ready-to-ack evidence and reply/result reference.
- Task/result identity follows PTCS `RFC-SPA-UPSTREAM-0004` result-vault boundary: operation id, task id, idempotency key, run id and artifact manifest reference must demultiplex retries/out-of-order results without re-running completed backend work.
- Cluster bind/canonical advertise addresses must be LAN/DNS-routable outside explicit single-node dev.
- ActorSystem ownership follows PTCS external attachment: merge `CommSpaActorFabric.requiredConfig` before `ActorSystem.Create`, validate/ensure before attach, then use `attachRegionToSystem` or `attachProxyToSystem`.

### 11.3 Runtime package boundary

`RFC-RUNTIME-0001` defines runtime as the reusable prompt-loop boundary. Runtime owns orchestration; host/PTCS/actor/CLI/Web adapters own concrete transport and UI concerns.

Runtime owns this ordered loop:

```text
inbox batch + session state + history refs + policy
  -> persist consumed cursor/message ids
  -> assemble prompt and compact if needed
  -> build normalized RunRequest
  -> invoke engine through engine/process port
  -> persist stdout/stderr/final/events/result/manifest
  -> write note/summary reference
  -> emit reply intent and ready-to-ack boundary
```

Preferred F# contract shape:

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

Rules:

- `decideCycle` is deterministic and unit-testable.
- `interpretCycleAsync` owns side-effect ordering through explicit ports; concrete PTCS/Akka/HTTP code stays outside runtime.
- `HostControl` route handlers validate DTOs and call runtime/PTCS services only; they must not assemble prompts or decide compaction policy.
- Actor shells call runtime from delivery/sharding handlers and do not duplicate prompt logic.
- Existing `CodexFs.Host.SessionEngineCycle.runSingleCycleAsync` is bounded host-era E2E evidence and a migration candidate, not the final durable sharded runtime loop.

Implemented RUNTIME-002 extraction:

- `CodexFs.RuntimePromptLoop` in `src/codex.fs/RuntimePromptLoop.fs` is the reusable planning boundary for current single-turn runtime work.
- `planPrompt` accepts `RuntimePromptInput` and returns `RuntimePromptPlan` with deterministic prompt markdown plus consumed MessageFabric JSONL evidence.
- `planAgyPrintExecution` accepts the stored prompt artifact path and returns normalized `RunRequest`, request JSON, rendered Agy argv JSON and `ProcessRunner.ProcessCommand`.
- `replyIntent` returns a redacted direct MessageFabric reply intent; it does not send through PTCS itself.
- `readyToAckBoundaryText` writes `phase=ready-to-ack`, consumed message ids, reply evidence, manifest path, final path and `persistedBeforeAck=true`.
- `CodexFs.Host.SessionEngineCycle.runSingleCycleAsync` remains the concrete adapter/interpreter for this slice: it polls PTCS, writes artifacts, invokes `ProcessRunner`, sends the reply, writes ready-to-ack boundary, then acks MessageFabric.
- This extraction unblocks `ACTOR-002` because ActorFabric worker shells can call the same runtime plan instead of duplicating host-era prompt/request/argv/reply logic. It does not satisfy production sharded durability by itself.

Implemented ACTOR-002 ActorFabric proof:

- `CodexFs.Ptcs.ActorFabricBinding` is the PTCS ActorFabric-backed worker shell boundary for the reset slice.
- `WorkerParticipantSpec` describes Foreman/Worker participant identity, display name, kind and non-secret UI labels.
- `CodexWorkerActor` runs on the PTCS-owned `ActorSystem`; it handles `EnsureParticipantRegistered` and `SpawnWorkerParticipant`, then registers Foreman/Worker as PTCS `agent` participants through `MessageFabricBinding.registerParticipantAsync`.
- `spawnWorker` is the direct actor spawn seam used by tests and future host composition; the test starts real `CommSpaActorFabric` with LAN `ClusterHost`, not a loopback-only HTTP shortcut.
- This proof is intentionally limited to participant visibility over real PTCS ActorFabric/MessageFabric. Durable sharded delivery, passivation/recovery and invoking `RuntimePromptLoop` from actor delivery handlers remain future implementation hardening.

Implemented design target for ACTOR-003 actor runtime artifact provider:

- `CodexFs.Ptcs.RuntimeMessageFabricCycle` becomes the concrete PTCS runtime cycle adapter. It owns the bounded side-effect ordering that was previously host-only: register participants, poll MessageFabric, call `RuntimePromptLoop`, write prompt/batch/request/rendered argv/stdout/stderr/final/result/manifest artifacts, send a PTCS direct reply, write `session-boundary.json`, then ack the selected cursor.
- `CodexFs.Host.SessionEngineCycle` remains public host API but is reduced to a config wrapper over `RuntimeMessageFabricCycle`. Host config may choose engine/executable/artifact root/timeout/session prefix/reply participant; host no longer owns prompt-loop sequencing.
- `CodexFs.Ptcs.ActorFabricBinding.CodexWorkerActor` handles `RunRuntimeCycle` by registering its PTCS participant and invoking `RuntimeMessageFabricCycle.runSingleCycleAsync` against the shared `CommSpaMessageFabric`.
- This slice is real-path but non-production-durable: it uses PTCS ActorFabric and MessageFabric, the installed `agy` headless CLI and the file artifact store, but does not yet claim sharded crash-durable delivery replay.

Preferred ACTOR-003 contract:

```fsharp
module CodexFs.Ptcs.RuntimeMessageFabricCycle

type RuntimeCycleOptions =
    { SessionId: string
      SessionParticipantId: string
      ReplyParticipantId: string option
      Engine: EngineKind
      ExecutablePath: string
      WorkingDirectory: string
      ArtifactRoot: string
      Timeout: TimeSpan
      SystemInstruction: string option
      AdditionalDirectories: string list
      InboxLimit: int }

type RuntimeCycleResult =
    { Status: string
      SessionId: string
      SessionParticipantId: string
      RunId: string
      ConsumedMessageCount: int
      AckCursor: string
      Outcome: string
      ExitCode: string
      ArtifactManifestPath: string
      PersistenceBoundaryPath: string
      FinalMessagePath: string
      RunNotePath: string
      ReplyMessageId: string
      ReplyBody: string }

val runSingleCycleAsync :
    CommSpaMessageFabric -> RuntimeCycleOptions -> CancellationToken -> Task<RuntimeCycleResult>

module CodexFs.Ptcs.ActorFabricBinding

type RunRuntimeCycle =
    { SessionId: string
      SessionParticipantId: string option
      ReplyParticipantId: string option
      Engine: EngineKind option
      ExecutablePath: string option
      WorkingDirectory: string option
      ArtifactRoot: string
      Timeout: TimeSpan option
      SystemInstruction: string option
      AdditionalDirectories: string list }

type RuntimeCycleCompleted =
    { ParticipantId: string
      ActorPath: string
      NodeAddress: string
      Result: RuntimeMessageFabricCycle.RuntimeCycleResult }
```

## 12. Artifact manifest design

```fsharp
type ArtifactKind =
    | RequestJson
    | PtcsMessageBatchJsonl
    | PromptMarkdown
    | RenderedArgvJson
    | StdoutLog
    | StderrLog
    | EventJsonl
    | FinalMarkdown
    | ResultJson
    | SessionBoundaryJson
    | RunNoteMarkdown
    | RedactionJson
    | CompactionMarkdown

type ArtifactRef =
    { Kind: ArtifactKind
      Path: string
      Sha256: string
      Size: int64
      CreatedUtc: DateTimeOffset }

type ArtifactManifest =
    { RunId: RunId
      SessionId: SessionId
      Engine: EngineKind
      SurfaceId: string
      PtcsMessages: PtcsMessageRef list
      PtcsTask: PtcsTaskRef option
      StartedUtc: DateTimeOffset
      CompletedUtc: DateTimeOffset option
      Outcome: RunOutcome
      Artifacts: ArtifactRef list }
```

`RFC-PERSIST-0001` defines the transcript/note/artifact policy used by future runtime/actor work.

Implemented WEBR-007 note/ref contract:

- `RuntimeMessageFabricCycle` writes `note.md` as `RunNoteMarkdown`.
- `manifest.json` includes the note artifact ref.
- MessageFabric reply body includes `manifest=...; final=...; note=...; summary=...`.
- `session-boundary.json` includes `runNotePath` alongside manifest/final path and reply evidence.
- `RuntimeCycleResult.RunNotePath` exposes the note path to host, actor and verifier callers.

Preferred private file layout:

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

Minimum run evidence:

| Evidence | Required rule |
| --- | --- |
| PTCS message/task identity | Persist consumed message ids, cursors and durable task/ticket ids before ack. |
| prompt / request / argv | Persist rendered prompt, normalized request and rendered argv metadata before or with engine execution evidence. |
| stdout/stderr/final/events | Persist raw private artifacts where policy allows; events are optional and engine-capability dependent. |
| result/manifest | Manifest records relative artifact refs, SHA-256, size, created UTC, engine/surface, PTCS refs and outcome. |
| run note | `note.md` is a redacted human-readable summary for CLI/Web/compact; it is not the raw transcript. |
| ready-to-ack boundary | `session-boundary.json` records reply evidence and selected cursor before MessageFabric ack. |

Provider boundary:

- runtime calls a persistence port for run evidence, notes, history entries, compaction output and ready-to-ack boundary;
- host/actor constructs the concrete provider and supplies `artifact.root`;
- first provider can remain file-based, but raw private artifacts must stay outside tracked public repo paths unless explicitly exported through a redacted export workflow;
- MessageFabric replies carry redacted summaries and manifest/note refs, not raw prompt/stdout/stderr bodies.

## 13. Redaction design

Redaction applies before writing display logs and before sending MessageFabric replies.

```fsharp
type RedactionRule =
    { Name: string
      Pattern: string
      Replacement: string
      Severity: RedactionSeverity }

type RedactionResult =
    { Text: string
      Hits: RedactionHit list }
```

Raw CLI stdout/stderr policy is configurable:

- default: write raw private artifact plus redacted display summary。
- public export: redacted only。
- never log environment variable values。
- MessageFabric body should use redacted summary and artifact references, not full raw transcript by default。
- high-risk redaction hits block public export until removed/redacted or recorded as false positive in an operation log。
- public repo `notes/` is not the default artifact root; if an export writes there, changed notes must pass sensitive scanning before commit/push。

## 14. CLI client design

`codex.fs.cli` is the canonical CLI package id and installs the explicit terminal command `codex.fs.cli`. `codex.fs.tool` installs the short alias command `codex.fs`; both commands use the same parser and HTTP client.

```text
codex.fs.cli session create --engine codex|agy --host <advertiseUri>
codex.fs.cli session send --session <id> --prompt <text-or-file> --host <advertiseUri>
codex.fs.cli session send --session <id> --worker-id <participantId> --prompt <text-or-file> --host <advertiseUri>
codex.fs session create --engine codex|agy --host <advertiseUri>
codex.fs session send --session <id> --prompt <text-or-file> --host <advertiseUri>
codex.fs session send --session <id> --worker-id <participantId> --prompt <text-or-file> --host <advertiseUri>
codex.fs session send --session <id> --prompt @prompt.md --host <advertiseUri>
codex.fs session attach --session <id> --host <advertiseUri>
codex.fs session drain --session <id> --host <advertiseUri>
codex.fs run status --run <id> --host <advertiseUri>
codex.fs run artifacts --run <id> --host <advertiseUri>
codex.fs host status --host <advertiseUri>
codex.fs engine probe
```

Argument parsing:

- Implement with `FAkka.Argu` DU per command group。
- Use `defaultArgumentsText` pattern for `.fsx` demos only; compiled tools use argv through Argu。
- Do not parse user prompts as shell commands。
- Root `--help`, `-h`, `help`, `/?`, and empty argv must print command help and return exit code 0 for dotnet tool ergonomics.

`codex.fs` should submit through host APIs that ultimately use PTCS MessageFabric; it should not write artifacts or MessageFabric streams directly.

Implemented compiled CLI parser package:

```fsharp
namespace CodexFs.Cli

module Cli

type CliArgument =
    | Session of ParseResults<SessionCommand>
    | Run of ParseResults<RunCommand>
    | Host of ParseResults<HostCommand>
    | Engine of ParseResults<EngineCommand>

val argumentParser : unit -> ArgumentParser<CliArgument>
val argumentParserFor : string -> ArgumentParser<CliArgument>
val examples : string list
val helpText : unit -> string
val helpTextFor : string -> string
val tryParse : string array -> Result<unit, string>
val tryParseFor : string -> string array -> Result<unit, string>
val tryParseHostStatus : string array -> Result<HostStatusOptions option, string>
val tryResolvePromptText : (string -> string) -> string -> Result<string, string>
```

Command groups:

- `session create --engine <codex|agy> --host <advertiseUri>`.
- `session send --session <id> --prompt <text-or-file> --host <advertiseUri>`.
- `session send --session <id> --worker-id <participantId> --prompt <text-or-file> --host <advertiseUri>`.
- `session attach --session <id> --host <advertiseUri>`.
- `session drain --session <id> --host <advertiseUri>`.
- `run status --run <id> --host <advertiseUri>`.
- `run artifacts --run <id> --host <advertiseUri>`.
- `host status --host <advertiseUri>`.
- `engine probe --engine <codex|agy> --executable <path-or-command>`.

Rules:

- `CLI-001` only defines parser/help/examples and the compiled entrypoint; real host calls belong to `CLI-002` / `CLI-003` / `CLI-004`.
- No command should interpret prompt text as shell commands.
- `--prompt @file` is a CLI client convenience. The CLI reads the file content locally and sends prompt text through host HTTP; the host endpoint does not read caller filesystem paths.
- Examples use non-secret LAN sample URI `http://192.168.10.20:8788`.

Implemented host status path:

```fsharp
module CodexFs.Cli.CliHttp

val hostStatusUri : string -> string
val getHostStatusAsync : HttpClient -> CancellationToken -> Cli.HostStatusOptions -> Task<CliHttpResult>
```

Rules:

- `host status --host <advertiseUri>` calls `GET /api/codexfs/host/health`.
- CLI must use the advertised URI. Production/clustered use must not rely on localhost-only host addresses.
- Output is the host JSON body for early terminal usability; formatting can become a later WBS row.

Implemented session send path:

```fsharp
module HostControl

type SessionSendRequest =
    { Prompt: string
      FromParticipantId: string
      WorkerId: string
      Tags: string list
      CorrelationId: string }

type SessionSendResponse =
    { Status: string
      SessionId: string
      SessionParticipantId: string
      TargetParticipantId: string
      FromParticipantId: string
      MessageId: string
      Cursor: string
      CorrelationId: string
      Tags: string list }

val sessionMessagesUri : HostControlContract -> string -> string
val sendSessionMessageAsync : HostRuntime -> string -> SessionSendRequest -> Task<IResult>

module CodexFs.Cli.CliHttp

type CliHttpResult =
    { StatusCode: int
      IsSuccess: bool
      Body: string }

val transportFailure : string -> exn -> CliHttpResult
val sessionSendUri : string -> string -> string
val foremanSendUri : string -> string
val sendSessionMessageAsync : HttpClient -> CancellationToken -> Cli.SessionSendOptions -> Task<CliHttpResult>
```

Rules:

- Route without `--session`: `POST /api/codexfs/foreman/messages`.
- Route with `--session`: `POST /api/codexfs/session/{sessionId}/messages`.
- `Cli.SessionSendOptions.SessionId` is `string option`; `None` means the default Foreman/SessionWorker and must be the first-use UX.
- Host derives session participant id as `<ptcs.sessionParticipantPrefix>.<sessionId>`.
- Default foreman target is `<ptcs.sessionParticipantPrefix>.foreman`; explicit session target is `<ptcs.sessionParticipantPrefix>.<sessionId>`.
- `WorkerId` blank/null means the host sends a direct MessageFabric message to `SessionParticipantId`.
- When `WorkerId` is supplied, host treats it as the exact target worker participant id and sends the direct MessageFabric message there instead; `SessionParticipantId` remains the foreman identity for the session.
- Host registers sender/session participants in PTCS and registers the override worker participant when one is supplied.
- CLI sends to the host advertised URI; CLI does not write MessageFabric or artifacts directly.
- CLI HTTP helpers catch `HttpRequestException`, `TaskCanceledException`, and invalid URI operation errors. They return `CliHttpResult` with `StatusCode = 0`, `IsSuccess = false`, and readable guidance instead of throwing an unhandled stack trace.
- CLI error text must remind operators to use `control.advertiseUri` and not the `codex.fs.host` process id as the HTTP port.
- `CLI-002` validates send-to-inbox through the host status path. Full attach/drain/status transcript behavior belongs to `CLI-003`.

Implemented session inbox read path:

```fsharp
module HostControl

type SessionInboxMessageResponse =
    { MessageId: string
      Cursor: string
      FromParticipantId: string
      Body: string
      CorrelationId: string
      Tags: string list }

type SessionInboxResponse =
    { Status: string
      SessionId: string
      SessionParticipantId: string
      PendingCount: int
      NextCursor: string
      Messages: SessionInboxMessageResponse list
      Transcript: string }

val sessionStatusUri : HostControlContract -> string -> string
val sessionAttachUri : HostControlContract -> string -> string
val sessionDrainUri : HostControlContract -> string -> string
val sessionStatusAsync : HostRuntime -> string -> Task<IResult>
val sessionAttachAsync : HostRuntime -> string -> Task<IResult>
val sessionDrainAsync : HostRuntime -> string -> Task<IResult>

module CodexFs.Cli.CliHttp

val getSessionStatusAsync : HttpClient -> CancellationToken -> Cli.SessionTargetOptions -> Task<CliHttpResult>
val attachSessionAsync : HttpClient -> CancellationToken -> Cli.SessionTargetOptions -> Task<CliHttpResult>
val drainSessionAsync : HttpClient -> CancellationToken -> Cli.SessionTargetOptions -> Task<CliHttpResult>
```

Rules:

- `GET /api/codexfs/session/{sessionId}/status` polls current inbox without acknowledging messages.
- `POST /api/codexfs/session/{sessionId}/attach` performs a bounded wait and returns transcript JSON without acknowledging messages.
- `POST /api/codexfs/session/{sessionId}/drain` returns current messages and acknowledges the returned cursor.
- CLI read commands use the host advertised URI and never bypass host-owned MessageFabric binding.
- `Transcript` is terminal-oriented output for early CLI usability; durable artifacts and engine replies remain E2E scope.

Implemented E2E single-cycle runner before ACTOR-003:

```fsharp
module CodexFs.Host.SessionEngineCycle

type SingleCycleOptions =
    { SessionId: string
      Engine: EngineKind option
      ExecutablePath: string option
      WorkingDirectory: string option
      ArtifactRoot: string option
      Timeout: TimeSpan option
      SystemInstruction: string option
      AdditionalDirectories: string list }

type SingleCycleResult =
    { Status: string
      SessionId: string
      SessionParticipantId: string
      RunId: string
      ConsumedMessageCount: int
      AckCursor: string
      Outcome: string
      ExitCode: string
      ArtifactManifestPath: string
      PersistenceBoundaryPath: string
      FinalMessagePath: string
      RunNotePath: string
      ReplyMessageId: string
      ReplyBody: string }

val runSingleCycleAsync : HostRuntime -> SingleCycleOptions -> CancellationToken -> Task<SingleCycleResult>
```

Rules:

- `runSingleCycleAsync` is a bounded package helper for `E2E-002`; ACTOR-003 refactors it into a host wrapper over `CodexFs.Ptcs.RuntimeMessageFabricCycle`.
- The function polls the session inbox once, assembles a prompt, invokes the selected installed engine, persists artifacts, sends a PTCS reply, writes a ready-to-ack session boundary, then acknowledges the inbox cursor.
- Current real engine implementation is Agy `1.0.x --print`; Agy flags must render before `--print`, and the prompt text is the final positional argument.
- Persisted artifacts include prompt markdown, PTCS message batch JSONL, normalized request JSON, rendered argv JSON, stdout, stderr, final markdown, result JSON, note markdown, manifest JSON, and `session-boundary.json`.
- `session-boundary.json` records phase `ready-to-ack`, selected cursor, consumed PTCS message ids, reply message id/body, manifest path, final message path and run note path. It must be written after reply evidence and before MessageFabric ack.
- Reply body contains a redacted/truncated summary plus artifact references; it must not contain the raw prompt transcript.
- `OPS-002` proves bounded single-cycle ack-after-artifact-and-reply-boundary ordering. Crash restart rehydration and sharded provider replay remain future worker-loop/provider scope.

## 14.1 PTCS Web chat profile integration

Existing PTCS Web chat is implemented by `G:\PulseTrade.fs\Libs\PulseTrade.Comm\src\PulseTrade.Comm.Spa.Host` over the PTCS package in `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa`.

Standalone `codex.fs.host` exposes `/chat` only as a legacy guard page. It must not become a product chat room or prompt composer. Browser chat UX is PTCS WebSharper chat room; codex.fs workers become visible there by registering/communicating as PTCS participants through the same `CommSpaMessageFabric` / `CommSpaActorFabric`.

Current deployment profile rules:

- `http://127.0.0.1:82/chat` is the local PTCS.Login chat entry for browser validation in this environment.
- `https://my-ai.co.in:81/chat` is the public GitHub OAuth profile; redirecting to GitHub login is expected for unauthenticated browsers.
- PTCS `/chat/api/agents` lists registered `agent` participants plus the fixed public channel.
- PTCS chat send uses `/sync/ws` `chat-send` or `/chat/api/send`, then projects the persisted chat message into `CommSpaMessageFabric`.
- Public channel messages are visible in the chat thread, but codex.fs workers only receive them when worker actors poll/wait the same PTCS MessageFabric with public/group inclusion enabled.

codex.fs integration rule:

- A standalone `codex.fs.host` tool uses a package-owned in-process PTCS MessageFabric and therefore does not make participants visible in an already running PTCS Web process.
- Production PTCS Web integration must reference `codex.fs.host` package from the PTCS Host process or a peer cluster node and start runtime with caller-owned PTCS fabric, e.g. `HostRuntime.startWithMessageFabric`.
- CLI/diagnostics are control surfaces for testing and operations. They must not define the human chat IA. The default terminal send path goes to `foreman` so the user does not need to invent or know a session id.
- The full worker actor loop must register session/worker participants as `Kind = Some "agent"` in the same PTCS hub/fabric and consume direct/public/group scopes according to its session policy.
- Browser acceptance must use real PTCS Host and real MessageFabric evidence; fake/mock UI smoke is not an acceptance path.

`RFC-WEB-0001` accepts the first `codex.fs.web` bundle contract. The Web bundle is a PTCS WebSharper extension following the Dynamic extension pattern:

```text
PTCS Host / CommHub
  -> useAIChat(options, runtime/control registration)
  -> RegisterClientExtension(...)
  -> RegisterClientExtensionScriptAsset(...)
  -> RegisterClientExtensionJsonPostHandler(...)
  -> WebSharper bundle
```

Recommended extension identity:

```fsharp
ExtensionId = "codex-fs-ai-chat"
DisplayName = Some "codex.fs AI Chat"
AppendPageShapes =
  [ { Shape = "codexfs-ai-chat"
      Label = Some "AI Chat"
      Badge = Some "ai"
      ClassName = Some "codexfs-ai-chat" } ]
```

The bundle must be generated from WebSharper/F# and must not require hand-written JavaScript. Same-origin JSON handlers are limited to registered allow-list operations such as metadata, participant capabilities, host health or artifact summary lookups; they are not generic proxies and do not become MessageFabric.

Implemented WEBR-007 PTCS artifact renderer:

```text
MessageFabric reply body
  -> PTCS classic chat thread
  -> codex.fs.web PulseTradeRegisterRenderer
  -> fallback bridge scans PTCS pre.message-body nodes
  -> codexfs-artifact-reply card
```

Renderer card contract:

| Test id | Meaning |
| --- | --- |
| `codexfs-artifact-reply` | Root card for a codex.fs worker run reply. |
| `codexfs-artifact-run` | Run id row. |
| `codexfs-artifact-outcome` | Outcome row. |
| `codexfs-artifact-manifest` | Manifest path relative to artifact root. |
| `codexfs-artifact-final` | Final message path relative to artifact root. |
| `codexfs-artifact-note` | Run note path relative to artifact root. |
| `codexfs-artifact-summary` | Redacted summary text. |

The fallback bridge exists because current PTCS classic chat appends raw `<pre class="message-body">` nodes and does not call the registered message renderer in that path. The bridge only upgrades PTCS-rendered message bodies that match the codex.fs reply grammar; it does not create a parallel chat store or change MessageFabric.

`HostWebShell.registeredHub` registers default `agent.codexfs.foreman` so the PTCS participant list has a first-use Foreman target. This is a product participant identity, not test data; actor-backed execution still owns actual work.

### 14.2 Interactive CLI and AI chat bundle target

`CLI-010` and `WEB-001` refine the UX after the product reset:

- `codex.fs.cli` is the terminal participant client. It defaults to Foreman/SessionActor, supports switching participant/worker perspective, engine/model/reasoning/invocation options, run/artifact query, and readable transport failures.
- `codex.fs.web` should follow the PTCS Dynamic/WebSharper extension style, exposing a `useAIChat(...)`-like bundle for participant perspective, model/reasoning controls and artifact references.
- Both clients send user intent through PTCS MessageFabric and/or host control APIs that delegate to runtime/actor services. Neither client owns a separate chat history.

`RFC-CLI-0002` accepts the terminal-side participant contract:

| CLI concept | Contract |
| --- | --- |
| sender identity | current baseline `user.codexfs.cli`; future explicit identity uses `--participant-id <user.*>` or a named local profile |
| default target | Foreman `SessionActor`, entity id `foreman`, participant `<ptcs.sessionParticipantPrefix>.foreman` |
| explicit session target | derives `<ptcs.sessionParticipantPrefix>.<sessionId>` |
| exact worker/participant target | direct send to supplied participant id; no additional derivation |
| public target | MessageFabric public channel; worker consumption is actor policy controlled |
| group target | MessageFabric group id; membership is PTCS fabric state |
| perspective | authorized read/render perspective only; it must not silently forge `agent.*` sender identity |

Future interactive command shape:

```text
codex.fs.cli chat --host <advertiseUri>
codex.fs.cli chat --host <advertiseUri> --target foreman
codex.fs.cli chat --host <advertiseUri> --target agent.codexfs.worker.sess-001.plan
codex.fs.cli chat --host <advertiseUri> --public
codex.fs.cli chat --host <advertiseUri> --group codexfs.session.sess-001
```

Terminal meta commands are parsed by the CLI and converted to host/PTCS operations; they are not shell commands:

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

Invocation options collected by CLI are intent metadata. Runtime/actor owns validation against policy, engine adapter selection, versioned argv rendering, prompt assembly, local compact, transcript/note/artifact persistence and MessageFabric ack ordering.

`RFC-WEB-0001` accepts the browser-side equivalent:

| Web concept | Contract |
| --- | --- |
| extension package | `codex.fs.web`; split `codex.fs.web.server` only when registration/handler size justifies it |
| host integration | PTCS `CommHub` extension registration, script asset registration and fixed JSON POST handlers |
| default target | Foreman/SessionActor participant, matching CLI first-use target vocabulary |
| participant list | real PTCS participants, including spawned workers registered as `agent` participants |
| public target | PTCS MessageFabric public scope; worker consumption is actor/session policy controlled |
| group target | PTCS MessageFabric group id and membership |
| perspective | authorized read/render policy, not sender impersonation |
| controls | engine/model/reasoning/invocation widgets emit normalized intent metadata |
| artifacts | redacted final summary plus run id, manifest ref and note ref; raw prompt/stdout/stderr stay under persistence policy |

Future Web verifier `misc/verifyPtcsAiChatBundle.fsx` must drive a real browser against PTCS Host `/chat`, load the extension manifest/assets, send public/direct/group messages through real MessageFabric, verify Foreman/worker participants, switch authorized perspective, and render real artifact/note refs. Standalone `codex.fs.host` `/chat`, fake mailboxes or internal-only UI smoke cannot satisfy `T-WEB-001` implementation acceptance.

### 14.3 PTCS classic webshell rewrite

`RFC-WEB-0002` corrects the implementation target after the Dynamic bundle mismatch.

The target Web composition is:

```text
PTCS classic /chat shell
  -> nav tabs / participant list / thread / composer
  -> codex.fs.web WebSharper Bundle
  -> useAIChat(...) CommHub registration
  -> PTCS MessageFabric public/direct/group
  -> PTCS ActorFabric SessionActor/WorkerActor
  -> codex.fs.runtime prompt loop
  -> headless Codex/Agy
  -> artifacts/notes/reply refs
```

Required project shape:

| Project | Required shape |
| --- | --- |
| `codex.fs.web` | WebSharper Bundle project like `PulseTrade.Comm.Spa.Dynamic`; exact PTCS package reference; generated assets under `wwwroot/js`; no hand-written JavaScript files. Minimal `JS.Inline` is allowed only for PTCS global renderer hook interop. |
| `codex.fs.web.server` or server module | `useAIChat(...)` registration, extension metadata, script assets and fixed JSON handlers over `CommHub`. |
| `codex.fs.host` | control-only mode plus product `ptcs-webshell` composition mode; product mode must expose PTCS classic shell. |
| `codex.fs.actor` | PTCS ActorFabric Foreman/Worker participants visible in PTCS participant list. |
| `codex.fs.runtime` | prompt-loop owner, not HTTP route handler or browser code. |

Cut from product acceptance:

- standalone `GET /chat` guard page;
- `GET/POST /diagnostics/session-send`;
- manually built HTML chat page in `codex.fs.host`;
- fake/mock browser or mailbox verifier;
- any Web path that does not show PTCS classic tabs + participant list + thread/composer.

The first implementation WBS after this RFC is `WEBR-002`: source/API inventory of PTCS classic shell and Dynamic bundle. No new Web code should be written before that inventory maps the actual PTCS route, DTO and extension APIs.

`WEBR-002` inventory result:

| Area | Confirmed source/API |
| --- | --- |
| PTCS shell routes | `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\Server.fs` routes `/` -> `/chat`, `/chat`, `/sets`, `/actors`, `/chat/api/agents`, `/chat/api/thread`, `/chat/api/send`, `/sync/ws`, append page APIs and client extension endpoints. |
| PTCS chat DOM | `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\Client.fs` mounts `.page.chat-grid`, `nav-chat` / `nav-sets` / `nav-actors`, `chat-participant`, `chat-work`, `chat-pending-state`, `thread-list`, `chat-composer`, `chat-draft`, and `chat-send`. |
| Participant source | `/chat/api/agents` uses `CommHub.ListParticipants(Some "agent", Some true)` plus fixed `channel.public`; worker visibility therefore requires real PTCS `agent` registration. |
| Message path | Browser sends `chat-send` through `/sync/ws` first and falls back to `/chat/api/send`; server chat thread reads from durable chat stream and in-memory thread view. |
| Extension seam | `CommHub.RegisterClientExtension`, `RegisterClientExtensionScriptAsset`, `RegisterClientExtensionJsonPostHandler`, `ClientExtensionRegistration`, `ClientExtensionScriptAsset`, and `AppendPageShapeTemplateRegistration`. |
| Dynamic baseline | `PulseTrade.Comm.Spa.Dynamic` is a `WebSharperProject=Bundle` package with `wwwroot\js`, exact `PulseTrade.Comm.Spa [0.2.5-beta71]`, server `useDynamicSdui(...)`, and WebSharper/F# client modules. |
| codex.fs cut-list | `codex.fs.host` `/chat` remains a PTCS guard page; `/diagnostics/session-send` remains diagnostics/control only; neither is product Web acceptance. |

Verifier: `misc/verifyPtcsClassicShellInventory.fsx`; detail: `doc/WEBR-002.PTCS-classic-shell-inventory.md`.

`WEBR-003` implementation result:

| Area | Implemented contract |
| --- | --- |
| Project | `src/codex.fs.web/codex.fs.web.fsproj` is a WebSharper Bundle package with `PackageId=codex.fs.web`, `AssemblyName=CodexFs.Web`, target `net10.0`, and generated package on build. |
| Dependencies | Exact `PulseTrade.Comm.Spa [0.2.5-beta71]` and `WebSharper.FSharp 10.1.5.674`; no ProjectReference backdoor to PTCS source. |
| Bundle output | `WebSharperBundleOutputDir=wwwroot\js`; generated files are tracked package content like `PulseTrade.Comm.Spa.Dynamic`, while WebSharper `websharper.log` is ignored. |
| Server seam | `CodexFs.Web.Server.CommHubExtensions.useAIChat(...)` registers fixed metadata JSON handler, generated script assets, extension manifest and append-page shape template over real PTCS `CommHub` APIs. |
| Client seam | `CodexFs.Web.Client.AIChatClient.Main` is the WebSharper SPA entrypoint; WEBR-006 adds the PTCS append-input renderer for AI target/perspective/invocation controls. |
| Build stability | `wsconfig.json` sets `buildService=false` and `buildServiceLogging=false` because WebSharper build service can leave `websharper.log` locked/inaccessible on Windows. |

Verifier: `misc/verifyCodexFsWebBundle.fsx`; passed 2026-07-05 11:07 +08:00 and checks nupkg `content/wwwroot/js` assets. Regression build/test also passed: `dotnet build .\codex.fs.slnx`; `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore`.

`WEBR-004` implementation result:

| Area | Implemented contract |
| --- | --- |
| Test binding | `tests/codex.fs.Tests` references `src/codex.fs.web/codex.fs.web.fsproj`, so `useAIChat()` is compiled and exercised with the rest of the product tests. |
| Extension manifest | `CommHub.useAIChat()` registers `codex-fs-ai-chat`, display name `codex.fs AI Chat`, metadata schema `codex.fs.web.ai-chat.v1`, and append shape `codexfs-ai-chat`. |
| Script assets | Generated WebSharper head/main scripts and runtime script are registered through PTCS `RegisterClientExtensionScriptAsset` and can be read back via `TryGetClientExtensionScriptAsset`. |
| Fixed JSON handler | Metadata endpoint `/client-extensions/codexfs-ai-chat/metadata` is registered through `RegisterClientExtensionJsonPostHandler` and dispatches through `TryHandleClientExtensionJsonPost`. |
| Append page template | `codexfs-ai-chat` template carries `agent.codexfs.foreman` default/key placeholder, prompt metadata placeholder and tags `codex.fs`, `ai-chat`, `agent`. |

Verifier: `misc/verifyUseAIChatRegistration.fsx`; passed 2026-07-05 11:37 +08:00 and runs real `dotnet build` plus full `codex.fs.Tests`. This unblocks `WEBR-005` host composition against PTCS classic `/chat`.

`WEBR-005` implementation result:

| Area | Implemented contract |
| --- | --- |
| Host config | `HostConfig.WebShell` adds explicit `web.profile=control-only|ptcs-webshell`, `web.bindAddress`, `web.port`, `web.advertiseUri`, `web.allowLoopbackOnly`, `web.actorFabric`, and `web.pcslRoot`. Control-only remains the default. |
| Runtime health | `HostRuntime.healthSummary` reports webshell profile without leaking default loopback URI when profile is `control-only`; `ptcs-webshell` reports advertised chat binding, actor fabric mode and explicit PCSL root when present. |
| Product composition | `CodexFs.Host.HostWebShell.tryStartAsync` creates one PTCS `CommHub`, registers `useAIChat()`, creates `CommSpaMessageFabric` from that same hub, and starts PTCS `Server.start` for classic `/chat`; product deployments must set a dedicated `web.pcslRoot` outside tool/build output. |
| Host tool | `codex.fs.host start --setting web.profile=ptcs-webshell ...` starts the product PTCS shell; default `start` still launches the ASP.NET control host. |
| Boundary | The legacy ASP.NET `/chat` guard is not treated as product chat. The product path is the PTCS Suave `/chat` server returned by `HostWebShell`. |

Verifier: `misc/verifyHostPtcsWebProfile.fsx`; passed 2026-07-05 12:32 +08:00 and runs real `dotnet build` plus full `codex.fs.Tests`. It binds to the machine LAN IP, verifies PTCS `/chat` manifest and `codex-fs-ai-chat`, fetches the generated WebSharper script asset, checks `/healthz`, rejects guard/diagnostics HTML and verifies host tool bounded start.

`WEBR-008` implementation result:

| Area | Implemented contract |
| --- | --- |
| Control root | Root HTML states the ASP.NET host is a control host and product browser chat requires `web.profile=ptcs-webshell`. |
| Legacy `/chat` | Control-only `/chat` remains a guard/pointer and is tested to have no composer, no form and no PTCS extension manifest. |
| Diagnostics | `/diagnostics/session-send` remains diagnostics-only and is tested to have no PTCS extension manifest. |
| Regression verifier | `misc/verifyNoStandaloneChatProductPath.fsx` checks source markers and runs full tests to prevent guard/diagnostics routes from being treated as product chat. |

Verifier: `misc/verifyNoStandaloneChatProductPath.fsx`; passed 2026-07-05 13:03 +08:00.

`WEBR-006` implementation result:

| Area | Implemented contract |
| --- | --- |
| Metadata | `AIChatExtensionOptions.defaultMetadataJson` now declares schema `codex.fs.web.ai-chat.v1`, intent schema `codex.fs.web.ai-intent.v1`, defaults, target/perspective modes, engine options and invocation options. |
| PTCS renderer | `CodexFs.Web.Client.AIChatClient.Main` registers `codexfs-ai-chat-append-input` through PTCS `PulseTradeRegisterAppendInputRenderer` for append shape `codexfs-ai-chat`. |
| Controls | The renderer exposes target mode/id, perspective mode/id, engine, model, reasoning, invocation mode, approval, prompt and send controls. Default target is Foreman (`agent.codexfs.foreman`), default engine is Agy and default invocation is `exec`. |
| Message payload | Send emits an append value with schema `codex.fs.web.ai-intent.v1`; `valueText` contains target/perspective/engine/invocation/body/tags JSON and `keyJson` stays the selected PTCS key. The browser never renders or sends CLI argv. |
| Layout | Controls use a responsive WebSharper-generated grid with fixed input heights, white background and inner scroll when PTCS mobile append area is height-constrained. Desktop and mobile evidence is recorded under `G:\codex.fs\log\20260705\webr006-host8-*`. |
| Package assets | `codex.fs.host`, `codex.fs.host.tool` and tests copy PTCS package `build/**` assets from `PulseTrade.Comm.Spa [0.2.5-beta71]`; otherwise PTCS classic `/chat` loads but `/build/PulseTrade.Comm.Spa.js` returns 404. |
| PTCS key rule | PTCS append-page `add-key` expects `keyJson` to be a JSON literal such as `"agent.codexfs.foreman"`. Plain `agent.codexfs.foreman` is a 400 Bad Request because it is not valid JSON. |
| PCSL caveat | `web.pcslRoot` creates the codex.fs hub with `CommHub.createEmptyWithPcslRoot`; however PTCS package `Server` currently has a static initializer path that can read the default AppContext `pcsl` before the explicit hub is passed. If default tool/build-output PCSL is corrupted, product host startup can fail before codex.fs configuration runs. |
| WebSocket caveat | `ptcs-webshell` currently serves the classic shell and HTTP fallback APIs; `/sync/ws` returned 503 in WEBR-006 browser evidence. HTTP fallback still allowed page create, add-key and append, but production UX should fix the WebSocket route before high-volume interactive use. |

Verifier: `misc/verifyAiIntentControls.fsx`; passed 2026-07-05 14:58 +08:00 after `dotnet build .\codex.fs.slnx --no-restore`, full `codex.fs.Tests`, and Playwright evidence on `http://10.28.112.93:18488/page/webr006-ai8`.

## 15. Testing design preview

Detailed test plan belongs in `doc/Test.md`, but SD expects:

- adapter render tests for Codex/Agy surfaces。
- engine probe tests using captured help/version fixtures。
- session behavior pure tests。
- artifact manifest tests。
- CLI parser tests with `FAkka.Argu`。
- PTCS MessageFabric integration tests with real `CommSpaMessageFabric`。
- durable agent task handoff tests with `CommSpaDurableMessageFabric` when durable profile is enabled。
- process runner tests using controlled command fixtures, not as production validation。
- real path verification for installed Codex/Agy where available。
- OpenAPI/Swagger generation tests for host HTTP control endpoints when HTTP is selected。
- SDK documentation generation check for public packages before NuGet-facing release。

## 16. Implementation sequence preview

1. Core domain + artifact manifest。
2. Engine adapter contract。
3. Codex `0.142.x` adapter。
4. Agy `1.0.x` adapter。
5. File artifact store。
6. PTCS MessageFabric session binding。
7. Minimal `codex.fs.host` with PTCS local fabric。
8. API documentation baseline: XML docs, OpenAPI/Swagger profile for HTTP host surface, and generated SDK docs pipeline。
9. `codex.fs.cli` terminal client package / `codex.fs.tool` short alias package。
10. Durable agent task handoff via `CommSpaDurableMessageFabric`。
11. Compaction。
12. PTCS Web UI extension/RFC and local82 profile verifier。
13. Product reset follow-up: runtime package boundary (`RUNTIME-001`), worker actor RFC (`ACTOR-001`), interactive CLI client (`CLI-010`), PTCS AI chat bundle (`WEB-001`) and transcript/note persistence provider (`PERSIST-001`)。

## 17. Open design items

| ID | Item |
| --- | --- |
| SD-TBD-001 | Resolved: HTTP control endpoint. Clustered profiles must use bind address + advertised LAN/routable URI; localhost is dev-only. |
| SD-TBD-002 | Resolved for RFC slice by `PERSIST-001`: preferred artifact root layout, private raw/public redacted boundary, run notes and provider-shaped persistence boundary are defined. Multi-workspace provider implementation remains future work. |
| SD-TBD-003 | Resolved for MVP: rule-based local compaction in `codex.fs`; selected-engine or dedicated LLM compaction is a future adapter over the same contract. |
| SD-TBD-004 | Resolved: first supported PTCS package is `PulseTrade.Comm.Spa [0.2.5-beta71]`; `codex.fs.ptcs` owns the exact reference while `codex.fs` core remains PTCS-independent. |
| SD-TBD-005 | Whether standalone host starts package-owned PTCS fabric by default or requires an existing PTCS host. |
| SD-TBD-006 | Resolved for MVP: OpenAPI JSON uses `Microsoft.AspNetCore.OpenApi`; Swagger UI uses `Swashbuckle.AspNetCore.SwaggerUi` only as optional UI assets; XML docs are canonical for SDK docs; FSharp.Formatting/fsdocs is preferred for F# reference-site generation. |
| SD-TBD-007 | Planned by `RUNTIME-001`: exact migration from bounded host helper to reusable `codex.fs.runtime` modules. |
| SD-TBD-008 | Resolved for RFC slice by `ACTOR-001`: `WorkerActor` / `SessionActor` protocol, sharding entity ids, delivery semantics and participant registration lifecycle. Implementation/verifier remains future work. |
| SD-TBD-009 | Resolved for RFC slice by `CLI-010`: interactive CLI participant identity, Foreman default, target/perspective switching and invocation option handoff are defined. Interactive command implementation/verifier remains future work. |
