namespace CodexFs

open System

/// Core shared domain vocabulary for codex.fs.
module Domain =

    /// Stable identifier for a long-lived agent session.
    type SessionId = SessionId of string

    /// Stable identifier for one engine execution run inside a session.
    type RunId = RunId of string

    /// Reference to a PTCS MessageFabric message consumed by a session worker.
    type PtcsMessageRef =
        { /// PTCS message identity.
          MessageId: string
          /// MessageFabric cursor associated with the message, when available.
          Cursor: string option
          /// Participant that produced the message.
          FromParticipantId: string
          /// Direct recipient participant, when the message was direct.
          ToParticipantId: string option
          /// Group recipient, when the message was sent to a group.
          GroupId: string option
          /// Correlation id used for idempotency and traceability.
          CorrelationId: string option }

    /// Reference to a PTCS durable task handoff.
    type PtcsTaskRef =
        { /// Durable task id, when assigned by PTCS.
          TaskId: string option
          /// Durable ingress ticket id, when assigned by PTCS.
          TicketId: string option
          /// Operation id associated with the task.
          OperationId: string option
          /// Reality id for task/result state tracking.
          TaskRealityId: string option
          /// Handle used to query task result state.
          ResultQueryHandle: string option }

    /// Supported engine families.
    type EngineKind =
        /// OpenAI Codex CLI.
        | Codex
        /// Agy CLI.
        | Agy
        /// Custom engine family identified by a stable name.
        | Custom of string

    /// Capability advertised by a concrete engine surface.
    type Capability =
        /// Engine can run a single headless prompt.
        | SingleTurnHeadless
        /// Engine can continue an existing conversation.
        | Continuation
        /// Engine can emit structured event stream output.
        | StructuredEventStream
        /// Engine can write final assistant text to a file.
        | FinalMessageFile
        /// Engine can include additional workspace directories.
        | WorkspaceDirectories
        /// Engine exposes sandbox mode selection.
        | SandboxMode
        /// Engine exposes model selection.
        | ModelSelection
        /// Engine supports execution timeout control.
        | Timeout
        /// Engine can write a dedicated log file.
        | LogFile

    /// Concrete CLI surface discovered for an engine family.
    type EngineSurface =
        { /// Engine family.
          Kind: EngineKind
          /// Raw version text returned by the engine.
          VersionText: string
          /// Stable surface id used by adapter registry.
          SurfaceId: string
          /// Capabilities supported by this surface.
          Capabilities: Set<Capability> }

    /// Normalized request for one engine run.
    type RunRequest =
        { /// Run identity.
          RunId: RunId
          /// Session identity.
          SessionId: SessionId
          /// Requested engine family.
          Engine: EngineKind
          /// Optional concrete surface id override.
          SurfaceId: string option
          /// Working directory used for the engine process.
          WorkingDirectory: string
          /// Path to the prompt file prepared by the session worker.
          PromptPath: string
          /// Directory where run artifacts must be written.
          ArtifactDirectory: string
          /// Execution timeout.
          Timeout: TimeSpan
          /// Additional directories exposed to the engine.
          AdditionalDirectories: string list
          /// PTCS MessageFabric messages that contributed to this run.
          PtcsMessages: PtcsMessageRef list
          /// Optional PTCS durable task reference.
          PtcsTask: PtcsTaskRef option
          /// Non-secret metadata associated with the run.
          Metadata: Map<string, string> }

    /// Terminal outcome of one engine run.
    type RunOutcome =
        /// Engine completed successfully.
        | Completed
        /// Engine failed before producing an accepted result.
        | Failed
        /// Engine exceeded its timeout.
        | TimedOut
        /// Run was cancelled by caller or host policy.
        | Cancelled

    /// Normalized result for one engine run.
    type RunResult =
        { /// Run identity.
          RunId: RunId
          /// Terminal outcome.
          Outcome: RunOutcome
          /// Process exit code, when a process was started.
          ExitCode: int option
          /// UTC start time.
          StartedUtc: DateTimeOffset
          /// UTC completion time, when terminal.
          CompletedUtc: DateTimeOffset option
          /// Path to the artifact manifest produced for this run.
          ArtifactManifestPath: string
          /// Path to final message text, when produced by the engine.
          FinalMessagePath: string option }
