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
    val Health : string // "/api/codexfs/host/health"

type HostControlContract =
    { Protocol: string
      BindAddress: string
      Port: int
      BindUri: string
      AdvertiseUri: string
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
val tryStartAsync : DateTimeOffset -> CancellationToken -> HostRuntime -> Task<Result<HostControlServer, HostConfigIssue list>>
val stopAsync : CancellationToken -> HostControlServer -> Task<HostRuntime>
```

Rules:

- `tryStartAsync` starts a real Kestrel HTTP listener using `control.bindAddress` and `control.port`, and exposes `GET /` plus `GET /api/codexfs/host/health`.
- `GET /` is the operator landing page. It must return HTTP 200 HTML and link to health, OpenAPI JSON and Swagger UI when those docs endpoints are enabled.
- `HostControlContract.HealthUri` is built from `control.advertiseUri`; CLI/Web/admin callers must use the advertised URI, not the bind URI when these differ.
- Non-loopback clustered profiles are validated by `HostConfig`; `control.allowLoopbackOnly = false` rejects loopback bind/advertise config before HTTP start.
- The health endpoint returns non-secret operational metadata only. It reports executable override keys but never executable override values.
- Starting the HTTP control endpoint may initialize the in-process PTCS MessageFabric via `HostRuntime`; this HTTP slice still does not create an ActorSystem and does not become a MessageFabric transport.
- Future ActorSystem initialization belongs to the PTCS ActorFabric/session-worker slice and must use the same non-loopback cluster profile rules as above.
- Endpoint definitions include success/failure examples and typed response metadata so `DOC-003` / `DOC-004` can expose generated OpenAPI JSON and Swagger UI without hand-written YAML.

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

Domain behavior should be testable without Akka and without live PTCS process.

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

val sessionSendUri : string -> string -> string
val sendSessionMessageAsync : HttpClient -> CancellationToken -> Cli.SessionSendOptions -> Task<CliHttpResult>
```

Rules:

- Route: `POST /api/codexfs/session/{sessionId}/messages`.
- Host derives session participant id as `<ptcs.sessionParticipantPrefix>.<sessionId>`.
- Default target is the derived session worker / 包工頭 participant. `WorkerId` blank/null means the host sends a direct MessageFabric message to `SessionParticipantId`.
- When `WorkerId` is supplied, host treats it as the exact target worker participant id and sends the direct MessageFabric message there instead; `SessionParticipantId` remains the foreman identity for the session.
- Host registers sender/session participants in PTCS and registers the override worker participant when one is supplied.
- CLI sends to the host advertised URI; CLI does not write MessageFabric or artifacts directly.
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

Implemented E2E single-cycle runner:

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
      ReplyMessageId: string
      ReplyBody: string }

val runSingleCycleAsync : HostRuntime -> SingleCycleOptions -> CancellationToken -> Task<SingleCycleResult>
```

Rules:

- `runSingleCycleAsync` is a bounded package helper for `E2E-002`; it is not the durable sharded actor loop.
- The function polls the session inbox once, assembles a prompt, invokes the selected installed engine, persists artifacts, sends a PTCS reply, writes a ready-to-ack session boundary, then acknowledges the inbox cursor.
- Current real engine implementation is Agy `1.0.x --print`; Agy flags must render before `--print`, and the prompt text is the final positional argument.
- Persisted artifacts include prompt markdown, PTCS message batch JSONL, normalized request JSON, rendered argv JSON, stdout, stderr, final markdown, result JSON, manifest JSON, and `session-boundary.json`.
- `session-boundary.json` records phase `ready-to-ack`, selected cursor, consumed PTCS message ids, reply message id/body, manifest path and final message path. It must be written after reply evidence and before MessageFabric ack.
- Reply body contains a redacted/truncated summary plus artifact references; it must not contain the raw prompt transcript.
- `OPS-002` proves bounded single-cycle ack-after-artifact-and-reply-boundary ordering. Crash restart rehydration and sharded provider replay remain future worker-loop/provider scope.

## 14.1 PTCS Web chat profile integration

Existing PTCS Web chat is implemented by `G:\PulseTrade.fs\Libs\PulseTrade.Comm\src\PulseTrade.Comm.Spa.Host` over the PTCS package in `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa`.

Current deployment profile rules:

- `http://127.0.0.1:82/chat` is the local PTCS.Login chat entry for browser validation in this environment.
- `https://my-ai.co.in:81/chat` is the public GitHub OAuth profile; redirecting to GitHub login is expected for unauthenticated browsers.
- PTCS `/chat/api/agents` lists registered `agent` participants plus the fixed public channel.
- PTCS chat send uses `/sync/ws` `chat-send` or `/chat/api/send`, then projects the persisted chat message into `CommSpaMessageFabric`.
- Public channel messages are visible in the chat thread, but codex.fs workers only receive them when worker actors poll/wait the same PTCS MessageFabric with public/group inclusion enabled.

codex.fs integration rule:

- A standalone `codex.fs.host` tool uses a package-owned in-process PTCS MessageFabric and therefore does not make participants visible in an already running PTCS Web process.
- Production PTCS Web integration must reference `codex.fs.host` package from the PTCS Host process or a peer cluster node and start runtime with caller-owned PTCS fabric, e.g. `HostRuntime.startWithMessageFabric`.
- The full worker actor loop must register session/worker participants as `Kind = Some "agent"` in the same PTCS hub/fabric and consume direct/public/group scopes according to its session policy.
- Browser acceptance must use real PTCS Host and real MessageFabric evidence; fake/mock UI smoke is not an acceptance path.

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

## 17. Open design items

| ID | Item |
| --- | --- |
| SD-TBD-001 | Resolved: HTTP control endpoint. Clustered profiles must use bind address + advertised LAN/routable URI; localhost is dev-only. |
| SD-TBD-002 | Exact artifact root layout for multi-workspace use. |
| SD-TBD-003 | Resolved for MVP: rule-based local compaction in `codex.fs`; selected-engine or dedicated LLM compaction is a future adapter over the same contract. |
| SD-TBD-004 | Resolved: first supported PTCS package is `PulseTrade.Comm.Spa [0.2.5-beta71]`; `codex.fs.ptcs` owns the exact reference while `codex.fs` core remains PTCS-independent. |
| SD-TBD-005 | Whether standalone host starts package-owned PTCS fabric by default or requires an existing PTCS host. |
| SD-TBD-006 | Resolved for MVP: OpenAPI JSON uses `Microsoft.AspNetCore.OpenApi`; Swagger UI uses `Swashbuckle.AspNetCore.SwaggerUi` only as optional UI assets; XML docs are canonical for SDK docs; FSharp.Formatting/fsdocs is preferred for F# reference-site generation. |
