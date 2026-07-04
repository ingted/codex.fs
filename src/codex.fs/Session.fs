namespace CodexFs

open CodexFs.Domain

/// Pure session state machine used by host/actor adapters.
module SessionBehavior =

    /// Logical status of one session worker.
    type SessionStatus =
        /// No active work; host may poll or wait for inbox messages.
        | Idle
        /// Host is polling or waiting on MessageFabric.
        | PollingInbox
        /// Host should assemble a prompt from the persisted message batch.
        | PreparingPrompt
        /// External CLI engine is running.
        | RunningEngine
        /// Host should persist run artifacts and normalized result.
        | PersistingArtifacts
        /// Host should reply through MessageFabric or durable result boundary.
        | ReplyingViaMessageFabric
        /// Host should compact history.
        | Compacting
        /// Host should acknowledge the MessageFabric cursor that has been incorporated into prompt history.
        | AckingInbox

    /// Durable state carried by one session worker.
    type SessionState =
        { /// Session identity.
          SessionId: SessionId
          /// PTCS participant id owned by this session.
          ParticipantId: string
          /// Current logical status.
          Status: SessionStatus
          /// Path to persisted full history.
          HistoryPath: string
          /// Last MessageFabric cursor that has been incorporated into prompt history.
          LastCursor: string option
          /// Active run id, when an engine process is running.
          ActiveRun: RunId option
          /// Path to current compacted summary, when available.
          CurrentSummaryPath: string option }

    /// External event applied to session state.
    type SessionCommand =
        /// Host received a MessageFabric inbox batch.
        | InboxBatchReceived of PtcsMessageRef list
        /// Prompt assembly completed and produced a normalized run request.
        | PromptPrepared of RunRequest
        /// Engine run completed successfully.
        | EngineRunCompleted of RunResult
        /// Engine run failed, timed out or was cancelled.
        | EngineRunFailed of RunResult
        /// MessageFabric reply was sent.
        | ReplySent
        /// MessageFabric cursor was acknowledged.
        | InboxAcked of cursor: string option
        /// History compaction completed.
        | CompactCompleted of summaryPath: string option
        /// Timer or host loop tick.
        | Tick

    /// Side effect requested by the pure session state machine.
    type SessionEffect =
        /// Poll or wait on MessageFabric inbox.
        | PollInbox
        /// Persist the message batch before prompt assembly.
        | PersistMessageBatch of PtcsMessageRef list
        /// Assemble prompt for the message batch.
        | PreparePrompt of PtcsMessageRef list
        /// Start an engine run.
        | StartRun of RunRequest
        /// Persist normalized run result and artifacts.
        | PersistRunResult of RunResult
        /// Send a MessageFabric reply body with tags.
        | SendMessageFabricReply of body: string * tags: string list
        /// Acknowledge a MessageFabric cursor.
        | AckMessageFabricCursor of cursor: string option
        /// Compact persisted history.
        | CompactHistory

    /// Result of one state transition.
    type Decision =
        { /// Updated state.
          State: SessionState
          /// Effects to execute after persisting the state transition.
          Effects: SessionEffect list }

    /// Return the last available cursor from a batch.
    let lastCursor messages =
        messages
        |> List.choose _.Cursor
        |> List.tryLast

    /// Create a short non-secret reply body for a run result.
    let resultReplyBody (result: RunResult) =
        let (RunId runId) = result.RunId
        $"run {runId} {result.Outcome}; manifest={result.ArtifactManifestPath}"

    /// Create tags for a run result reply.
    let resultTags (result: RunResult) =
        let outcomeTag =
            match result.Outcome with
            | Completed -> "completed"
            | Failed -> "failed"
            | TimedOut -> "timed-out"
            | Cancelled -> "cancelled"

        [ "run"; outcomeTag ]

    /// Apply a command to session state and return the next state plus requested effects.
    let decide (state: SessionState) (command: SessionCommand) =
        match state.Status, command with
        | Idle, Tick ->
            { State = { state with Status = PollingInbox }
              Effects = [ PollInbox ] }

        | PollingInbox, InboxBatchReceived []
        | Idle, InboxBatchReceived [] ->
            { State = { state with Status = Idle }
              Effects = [] }

        | PollingInbox, InboxBatchReceived messages
        | Idle, InboxBatchReceived messages ->
            { State =
                { state with
                    Status = PreparingPrompt
                    LastCursor = lastCursor messages |> Option.orElse state.LastCursor }
              Effects =
                [ PersistMessageBatch messages
                  PreparePrompt messages ] }

        | PreparingPrompt, PromptPrepared request ->
            { State =
                { state with
                    Status = RunningEngine
                    ActiveRun = Some request.RunId }
              Effects = [ StartRun request ] }

        | RunningEngine, EngineRunCompleted result ->
            { State =
                { state with
                    Status = ReplyingViaMessageFabric
                    ActiveRun = None }
              Effects =
                [ PersistRunResult result
                  SendMessageFabricReply(resultReplyBody result, resultTags result) ] }

        | RunningEngine, EngineRunFailed result ->
            { State =
                { state with
                    Status = ReplyingViaMessageFabric
                    ActiveRun = None }
              Effects =
                [ PersistRunResult result
                  SendMessageFabricReply(resultReplyBody result, resultTags result) ] }

        | ReplyingViaMessageFabric, ReplySent ->
            { State = { state with Status = AckingInbox }
              Effects = [ AckMessageFabricCursor state.LastCursor ] }

        | AckingInbox, InboxAcked cursor ->
            { State =
                { state with
                    Status = Idle
                    LastCursor = cursor |> Option.orElse state.LastCursor }
              Effects = [] }

        | Idle, CompactCompleted summaryPath
        | Compacting, CompactCompleted summaryPath ->
            { State =
                { state with
                    Status = Idle
                    CurrentSummaryPath = summaryPath |> Option.orElse state.CurrentSummaryPath }
              Effects = [] }

        | Idle, _ ->
            { State = state
              Effects = [] }

        | _, Tick ->
            { State = state
              Effects = [] }

        | _, _ ->
            { State = state
              Effects = [] }
