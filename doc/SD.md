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
  codex.fs.cli/
    codex.fs.cli.fsproj
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
| `codex.fs.host` | `CodexFs.Host` | Host runtime package and dotnet tool。 |
| `codex.fs.cli` | `CodexFs.Cli` | Terminal client。 |
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
codex.fs.cli run --engine codex --engine-surface codex-exec-0.142
codex.fs.cli run --engine agy --engine-surface agy-print-1.0
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
val startInProcessMessageFabric : DateTimeOffset -> HostRuntime -> HostRuntime
val health : HostRuntime -> HostHealth
val healthSummary : HostRuntime -> string
val stop : HostRuntime -> HostRuntime
```

Rules:

- `startInProcessMessageFabric` initializes a real PTCS `CommSpaMessageFabric` through `codex.fs.ptcs`; it is not an alternate mailbox and does not create an ActorSystem。
- `health` and `healthSummary` expose non-secret operational metadata and redacted config diagnostics only。
- executable override values are omitted from health; only engine override keys are shown。
- HTTP listener, endpoint DTOs and Swagger exposure remain `HOST-003` / `DOC-003` scope。
- PTCS ActorSystem / sharded cluster setup is outside this in-process MessageFabric slice; production `CommSpaActorFabric` must bind/advertise a LAN or otherwise routable address, not `127.0.0.1`。

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
    val Health : string // "/api/codexfs/host/health"

type HostControlContract =
    { Protocol: string
      BindAddress: string
      Port: int
      BindUri: string
      AdvertiseUri: string
      HealthUri: string
      AllowLoopbackOnly: bool
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

- `tryStartAsync` starts a real Kestrel HTTP listener using `control.bindAddress` and `control.port`, and exposes `GET /api/codexfs/host/health`.
- `HostControlContract.HealthUri` is built from `control.advertiseUri`; CLI/Web/admin callers must use the advertised URI, not the bind URI when these differ.
- Non-loopback clustered profiles are validated by `HostConfig`; `control.allowLoopbackOnly = false` rejects loopback bind/advertise config before HTTP start.
- The health endpoint returns non-secret operational metadata only. It reports executable override keys but never executable override values.
- Starting the HTTP control endpoint may initialize the in-process PTCS MessageFabric via `HostRuntime`; it still does not create an ActorSystem and does not become a MessageFabric transport.
- Endpoint definitions include success/failure examples and typed response metadata (`Produces<HostControlHealthResponse>`) so `DOC-003` can add generated OpenAPI JSON and Swagger UI without hand-written YAML.

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
- OpenAPI JSON route: `GET /openapi/v1.json`, mapped through `MapOpenApi("/openapi/{documentName}.json")` when `apiDocs.generateOpenApi = true`.
- Swagger UI route: `/<apiDocs.swaggerRoutePrefix>/index.html`, enabled only when both `apiDocs.generateOpenApi = true` and `apiDocs.exposeSwaggerUi = true`.
- Test profile uses `apiDocs.swaggerRoutePrefix = docs`, therefore the advertised UI URI is `<control.advertiseUri>/docs/index.html`.
- OpenAPI JSON and Swagger UI must be verified through `HostControlContract.OpenApiJsonUri` / `SwaggerUiUri`, not through localhost-only URLs.

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

`codex.fs.cli` commands:

```text
codex.fs.cli session create [--engine codex|agy]
codex.fs.cli session send --session <id> --prompt <text-or-file>
codex.fs.cli session attach --session <id>
codex.fs.cli session drain --session <id>
codex.fs.cli run status --run <id>
codex.fs.cli run artifacts --run <id>
codex.fs.cli host status
codex.fs.cli engine probe
```

Argument parsing:

- Implement with `FAkka.Argu` DU per command group。
- Use `defaultArgumentsText` pattern for `.fsx` demos only; compiled tools use argv through Argu。
- Do not parse user prompts as shell commands。

`codex.fs.cli` should submit through host APIs that ultimately use PTCS MessageFabric; it should not write artifacts or MessageFabric streams directly.

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
val examples : string list
val helpText : unit -> string
val tryParse : string array -> Result<unit, string>
```

Command groups:

- `session create --engine <codex|agy> --host <advertiseUri>`.
- `session send --session <id> --prompt <text-or-file> --host <advertiseUri>`.
- `session attach --session <id> --host <advertiseUri>`.
- `session drain --session <id> --host <advertiseUri>`.
- `run status --run <id> --host <advertiseUri>`.
- `run artifacts --run <id> --host <advertiseUri>`.
- `host status --host <advertiseUri>`.
- `engine probe --engine <codex|agy> --executable <path-or-command>`.

Rules:

- `CLI-001` only defines parser/help/examples and the compiled entrypoint; real host calls belong to `CLI-002` / `CLI-003`.
- No command should interpret prompt text as shell commands.
- Examples use non-secret LAN sample URI `http://192.168.10.20:8788`.

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
9. `codex.fs.cli` terminal client。
10. Durable agent task handoff via `CommSpaDurableMessageFabric`。
11. Compaction。
12. PTCS Web UI extension/RFC。

## 17. Open design items

| ID | Item |
| --- | --- |
| SD-TBD-001 | Resolved: HTTP control endpoint. Clustered profiles must use bind address + advertised LAN/routable URI; localhost is dev-only. |
| SD-TBD-002 | Exact artifact root layout for multi-workspace use. |
| SD-TBD-003 | Resolved for MVP: rule-based local compaction in `codex.fs`; selected-engine or dedicated LLM compaction is a future adapter over the same contract. |
| SD-TBD-004 | Resolved: first supported PTCS package is `PulseTrade.Comm.Spa [0.2.5-beta71]`; `codex.fs.ptcs` owns the exact reference while `codex.fs` core remains PTCS-independent. |
| SD-TBD-005 | Whether standalone host starts package-owned PTCS fabric by default or requires an existing PTCS host. |
| SD-TBD-006 | Resolved for MVP: OpenAPI JSON uses `Microsoft.AspNetCore.OpenApi`; Swagger UI uses `Swashbuckle.AspNetCore.SwaggerUi` only as optional UI assets; XML docs are canonical for SDK docs; FSharp.Formatting/fsdocs is preferred for F# reference-site generation. |
