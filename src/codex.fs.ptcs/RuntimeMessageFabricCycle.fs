namespace CodexFs.Ptcs

open System
open System.IO
open System.Text
open System.Threading
open CodexFs
open CodexFs.Artifacts
open CodexFs.Domain
open CodexFs.PromptAssembly
open PulseTrade.Comm.Spa

/// Real PTCS MessageFabric interpreter for one bounded runtime cycle.
module RuntimeMessageFabricCycle =

    /// Caller options for one bounded PTCS runtime cycle.
    type RuntimeCycleOptions =
        { /// Target session id used for artifact paths and prompt identity.
          SessionId: string
          /// PTCS participant id whose inbox is consumed by this cycle.
          SessionParticipantId: string
          /// Optional PTCS participant id used when sending the reply.
          ReplyParticipantId: string option
          /// Engine family selected for this cycle.
          Engine: EngineKind
          /// Executable path or command used by the engine process.
          ExecutablePath: string
          /// Working directory used by the engine process.
          WorkingDirectory: string
          /// Artifact root for private run evidence.
          ArtifactRoot: string
          /// Engine timeout.
          Timeout: TimeSpan
          /// Optional instruction prepended to the assembled prompt.
          SystemInstruction: string option
          /// Additional directories exposed to the engine.
          AdditionalDirectories: string list
          /// Maximum messages returned by one inbox poll.
          InboxLimit: int }

    /// Result returned by one bounded PTCS runtime cycle.
    type RuntimeCycleResult =
        { /// Stable status text: `empty`, `completed`, `failed`, `timed-out`, or `cancelled`.
          Status: string
          /// Target session id.
          SessionId: string
          /// PTCS participant id whose inbox was consumed.
          SessionParticipantId: string
          /// Run id when an engine run was started.
          RunId: string
          /// Number of messages consumed into the prompt.
          ConsumedMessageCount: int
          /// Cursor acknowledged after artifact persistence and reply.
          AckCursor: string
          /// Engine run outcome when an engine run was started.
          Outcome: string
          /// Process exit code text, or blank when unavailable.
          ExitCode: string
          /// Artifact manifest path relative to artifact root.
          ArtifactManifestPath: string
          /// Persistence boundary artifact written after reply evidence and before inbox ack.
          PersistenceBoundaryPath: string
          /// Final message artifact path relative to artifact root.
          FinalMessagePath: string
          /// PTCS reply message id.
          ReplyMessageId: string
          /// Reply body sent through MessageFabric.
          ReplyBody: string }

    /// Default executable name for a supported engine kind.
    let defaultExecutable engine =
        match engine with
        | Codex -> "codex"
        | Agy -> "agy"
        | Custom name -> name

    let safeTags tags =
        if isNull (box tags) then [] else tags

    let envelopeToPromptMessage (message: MessageFabricEnvelope) =
        { Ref = MessageFabricBinding.envelopeToMessageRef message
          Body = message.Body
          ReceivedUtc = None
          Tags = safeTags message.Tags
          Metadata = Map.ofList [ "source", "ptcs-messagefabric" ] }

    let participantRegistration displayName kind labels =
        { MessageFabricBinding.defaultRegistration with
            DisplayName = Some displayName
            Kind = Some kind
            Labels = Some labels }

    let ensureParticipantAsync fabric participantId displayName kind labels =
        task {
            let binding = MessageFabricBinding.defaultBinding participantId
            let! _ = MessageFabricBinding.registerParticipantAsync fabric binding (participantRegistration displayName kind labels)
            return ()
        }

    let sessionBinding (options: RuntimeCycleOptions) =
        { MessageFabricBinding.defaultBinding options.SessionParticipantId with
            ReplyParticipantId = options.ReplyParticipantId
            InboxLimit = options.InboxLimit }

    let emptyResult (options: RuntimeCycleOptions) =
        { Status = "empty"
          SessionId = options.SessionId
          SessionParticipantId = options.SessionParticipantId
          RunId = String.Empty
          ConsumedMessageCount = 0
          AckCursor = String.Empty
          Outcome = String.Empty
          ExitCode = String.Empty
          ArtifactManifestPath = String.Empty
          PersistenceBoundaryPath = String.Empty
          FinalMessagePath = String.Empty
          ReplyMessageId = String.Empty
          ReplyBody = String.Empty }

    let validateOptions (options: RuntimeCycleOptions) =
        if String.IsNullOrWhiteSpace options.SessionId then
            invalidArg (nameof options.SessionId) "Session id is required."

        if String.IsNullOrWhiteSpace options.SessionParticipantId then
            invalidArg (nameof options.SessionParticipantId) "Session participant id is required."

        if String.IsNullOrWhiteSpace options.ExecutablePath then
            invalidArg (nameof options.ExecutablePath) "Executable path is required."

        if String.IsNullOrWhiteSpace options.WorkingDirectory then
            invalidArg (nameof options.WorkingDirectory) "Working directory is required."

        if String.IsNullOrWhiteSpace options.ArtifactRoot then
            invalidArg (nameof options.ArtifactRoot) "Artifact root is required."

        if options.Timeout <= TimeSpan.Zero then
            invalidArg (nameof options.Timeout) "Timeout must be greater than zero."

        if options.Engine <> Agy then
            invalidOp $"PTCS runtime cycle currently supports agy only; requested {RuntimePromptLoop.engineText options.Engine}."

    /// Run one bounded PTCS runtime cycle. It polls once, runs the selected engine for pending messages, replies, then acknowledges.
    let runSingleCycleAsync (fabric: CommSpaMessageFabric) (options: RuntimeCycleOptions) (cancellationToken: CancellationToken) =
        task {
            if isNull (box fabric) then
                nullArg (nameof fabric)

            validateOptions options

            let workingDirectory = Path.GetFullPath options.WorkingDirectory
            let artifactRoot = Path.GetFullPath options.ArtifactRoot
            let binding = sessionBinding options

            do!
                ensureParticipantAsync
                    fabric
                    options.SessionParticipantId
                    options.SessionId
                    "agent"
                    [ "codex.fs"; "session"; "runtime-cycle" ]

            match options.ReplyParticipantId with
            | Some replyParticipantId when replyParticipantId <> options.SessionParticipantId ->
                do!
                    ensureParticipantAsync
                        fabric
                        replyParticipantId
                        "codex.fs runtime"
                        "agent"
                        [ "codex.fs"; "runtime"; "reply" ]
            | _ -> ()

            let! batch = MessageFabricBinding.pollInboxAsync fabric binding None

            if batch.Messages.IsEmpty then
                return emptyResult options
            else
                let startedUtc = DateTimeOffset.UtcNow
                let runId = RuntimePromptLoop.newRunId startedUtc
                let storeConfig = { FileArtifactStore.ArtifactRoot = artifactRoot }
                let sessionId = SessionId options.SessionId
                let promptMessages = batch.Messages |> List.map envelopeToPromptMessage
                let (RunId runIdText) = runId

                let promptPlan =
                    RuntimePromptLoop.planPrompt
                        { SessionId = sessionId
                          RunId = runId
                          ParticipantId = options.SessionParticipantId
                          Engine = options.Engine
                          SurfaceId = Some Engine.Agy.V1_0.Print.SurfaceId
                          WorkingDirectory = workingDirectory
                          Messages = promptMessages
                          Policy =
                            { PromptAssembly.defaultPolicy with
                                SystemInstruction = options.SystemInstruction
                                MaxMessageBodyChars = Some 8000
                                AdditionalContext =
                                    [ "cycle", "ptcs-runtime-cycle"
                                      "adapter", "actor-or-host" ] } }

                let promptArtifact =
                    FileArtifactStore.writeText storeConfig sessionId runId PromptMarkdown "prompt.md" promptPlan.Prompt.Markdown startedUtc

                let ptcsBatchArtifact =
                    FileArtifactStore.writeText storeConfig sessionId runId PtcsMessageBatchJsonl "ptcs-messages.jsonl" promptPlan.MessageBatchJsonl startedUtc

                let executionPlan =
                    RuntimePromptLoop.planAgyPrintExecution
                        { PromptPlan = promptPlan
                          SessionId = sessionId
                          RunId = runId
                          ExecutablePath = options.ExecutablePath
                          WorkingDirectory = workingDirectory
                          PromptPath = promptArtifact.Reference.Path
                          ArtifactDirectory = FileArtifactStore.runDirectory storeConfig sessionId runId
                          Timeout = options.Timeout
                          AdditionalDirectories = options.AdditionalDirectories
                          Metadata =
                            Map.ofList
                                [ "cycle", "single";
                                  "adapter", "ptcs-runtime-messagefabric";
                                  "sessionParticipantId", options.SessionParticipantId ] }

                let requestArtifact =
                    FileArtifactStore.writeText storeConfig sessionId runId RequestJson "request.json" executionPlan.RequestJson startedUtc

                let renderedArtifact =
                    FileArtifactStore.writeText storeConfig sessionId runId RenderedArgvJson "rendered-argv.json" executionPlan.RenderedCommandJson startedUtc

                let! processResult =
                    ProcessRunner.runAsync
                        { Timeout = options.Timeout
                          KillGracePeriod = TimeSpan.FromSeconds 5.0 }
                        executionPlan.ProcessCommand
                        cancellationToken

                let outcome = RuntimePromptLoop.processOutcome processResult

                let outputMapping =
                    Engine.Agy.V1_0.Print.mapOutputArtifacts
                        storeConfig
                        executionPlan.Request
                        { Stdout = processResult.Stdout
                          Stderr = processResult.Stderr
                          StartedUtc = processResult.StartedUtc
                          CompletedUtc = Some processResult.CompletedUtc
                          Outcome = outcome }

                let manifestRelativePath =
                    Path.Combine("sessions", options.SessionId, "runs", runIdText, "manifest.json")

                let runResult =
                    { RunId = runId
                      Outcome = outcome
                      ExitCode = processResult.ExitCode
                      StartedUtc = processResult.StartedUtc
                      CompletedUtc = Some processResult.CompletedUtc
                      ArtifactManifestPath = manifestRelativePath
                      FinalMessagePath = outputMapping.FinalMessage |> Option.map _.Reference.Path }

                let resultArtifact =
                    FileArtifactStore.writeText storeConfig sessionId runId ResultJson "result.json" (RuntimePromptLoop.runResultText runResult) processResult.CompletedUtc

                let manifest =
                    { outputMapping.Manifest with
                        Artifacts =
                            [ promptArtifact.Reference
                              ptcsBatchArtifact.Reference
                              requestArtifact.Reference
                              renderedArtifact.Reference
                              outputMapping.Stdout.Reference
                              outputMapping.Stderr.Reference
                              match outputMapping.FinalMessage with
                              | Some finalMessage -> finalMessage.Reference
                              | None -> ()
                              resultArtifact.Reference ] }

                let manifestPath = Path.Combine(FileArtifactStore.artifactRoot storeConfig, manifestRelativePath)
                let manifestDirectory = Path.GetDirectoryName manifestPath

                if String.IsNullOrWhiteSpace manifestDirectory then
                    invalidOp "Artifact manifest directory could not be resolved."

                Directory.CreateDirectory manifestDirectory |> ignore
                File.WriteAllText(manifestPath, RuntimePromptLoop.manifestText manifest, UTF8Encoding(false, true))

                let finalPath = runResult.FinalMessagePath |> Option.defaultValue String.Empty
                let targetParticipantId = batch.Messages.Head.FromParticipantId

                let replyIntent =
                    RuntimePromptLoop.replyIntent runId outcome manifestRelativePath finalPath targetParticipantId processResult.Stdout processResult.Stderr

                let! reply =
                    MessageFabricBinding.sendDirectReplyAsync
                        fabric
                        binding
                        replyIntent.TargetParticipantId
                        replyIntent.Body
                        replyIntent.Tags
                        replyIntent.CorrelationId

                let boundaryArtifact =
                    FileArtifactStore.writeText
                        storeConfig
                        sessionId
                        runId
                        SessionBoundaryJson
                        "session-boundary.json"
                        (RuntimePromptLoop.readyToAckBoundaryText
                            { SessionId = sessionId
                              RunId = runId
                              SelectedCursor = batch.NextCursor
                              ConsumedMessageIds = promptMessages |> List.map _.Ref.MessageId
                              Reply =
                                { MessageId = reply.MessageId
                                  Body = reply.Body }
                              ArtifactManifestPath = manifestRelativePath
                              FinalMessagePath = finalPath
                              PersistedBeforeAck = true })
                        DateTimeOffset.UtcNow

                let! ack = MessageFabricBinding.ackInboxAsync fabric binding batch.NextCursor

                return
                    { Status = RuntimePromptLoop.outcomeText outcome
                      SessionId = options.SessionId
                      SessionParticipantId = options.SessionParticipantId
                      RunId = runIdText
                      ConsumedMessageCount = batch.Messages.Length
                      AckCursor = ack.Cursor |> Option.defaultValue String.Empty
                      Outcome = RuntimePromptLoop.outcomeText outcome
                      ExitCode = processResult.ExitCode |> Option.map string |> Option.defaultValue String.Empty
                      ArtifactManifestPath = manifestRelativePath
                      PersistenceBoundaryPath = boundaryArtifact.Reference.Path
                      FinalMessagePath = finalPath
                      ReplyMessageId = reply.MessageId
                      ReplyBody = replyIntent.Body }
        }
