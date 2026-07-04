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
    { Prompt: string option
      Print: bool
      PrintTimeout: TimeSpan option
      AddDir: string list
      Project: string option
      NewProject: bool
      Conversation: string option
      ContinueLatest: bool
      Model: string option
      LogFile: string option
      Sandbox: bool
      DangerouslySkipPermissions: bool }
```

Agy render policy:

- Use `agy --print` or `agy --prompt` for single-turn headless execution.
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

Message operations map to PTCS:

| codex.fs operation | PTCS API |
| --- | --- |
| register session participant | `CommSpaMessageFabric.RegisterParticipantAsync` |
| send prompt/reply | `SendAsync` |
| poll/wait pending inbox | `PollInboxAsync` / `WaitInboxAsync` |
| ack processed batch | `AckAsync` |
| drain for terminal attach | `DrainInboxAsync` |
| durable agent task | `CommSpaDurableMessageFabric.SubmitAgentTaskDurableAsync` |

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
      Ptcs: PtcsHostConfig }

type PtcsHostConfig =
    { FabricMode: string
      SessionParticipantPrefix: string
      ReplyParticipantId: string option
      DurableAgentTasks: bool
      DefaultInboxLimit: int }

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

## 10. Session behavior design

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

## 11. Artifact manifest design

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

## 12. Redaction design

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

## 13. CLI client design

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

## 14. Testing design preview

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

## 15. Implementation sequence preview

1. Core domain + artifact manifest。
2. Engine adapter contract。
3. Codex `0.142.x` adapter。
4. Agy `1.0.x` adapter。
5. File artifact store。
6. PTCS MessageFabric session binding。
7. Minimal `codex.fs.host` with PTCS local fabric。
8. `codex.fs.cli` terminal client。
9. Durable agent task handoff via `CommSpaDurableMessageFabric`。
10. Compaction。
11. PTCS Web UI extension/RFC。

## 16. Open design items

| ID | Item |
| --- | --- |
| SD-TBD-001 | Host endpoint protocol for CLI client while MessageFabric remains communication fact source. |
| SD-TBD-002 | Exact artifact root layout for multi-workspace use. |
| SD-TBD-003 | Whether compaction uses selected engine or dedicated adapter. |
| SD-TBD-004 | First supported PTCS package version and exact NuGet reference range. |
| SD-TBD-005 | Whether standalone host starts package-owned PTCS fabric by default or requires an existing PTCS host. |
