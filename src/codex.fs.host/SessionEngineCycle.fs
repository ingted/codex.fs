namespace CodexFs.Host

open System
open System.IO
open System.Text
open System.Threading
open System.Threading.Tasks
open CodexFs
open CodexFs.Artifacts
open CodexFs.Domain
open CodexFs.PromptAssembly
open CodexFs.Ptcs
open PulseTrade.Comm.Spa

/// Bounded single-cycle host workflow from PTCS inbox to engine run to PTCS reply.
module SessionEngineCycle =

    /// Caller options for one bounded session cycle.
    type SingleCycleOptions =
        { /// Target session id.
          SessionId: string
          /// Engine family override; defaults to host config.
          Engine: EngineKind option
          /// Executable path/command override; defaults to `agy` or `codex`.
          ExecutablePath: string option
          /// Working directory used by the engine process.
          WorkingDirectory: string option
          /// Artifact root override; defaults to host config.
          ArtifactRoot: string option
          /// Engine timeout override; defaults to host config.
          Timeout: TimeSpan option
          /// Optional instruction prepended to the assembled prompt.
          SystemInstruction: string option
          /// Additional directories exposed to the engine.
          AdditionalDirectories: string list }

    /// Result returned by one bounded session cycle.
    type SingleCycleResult =
        { /// Stable status text: `empty`, `completed`, `failed`, `timed-out`, or `cancelled`.
          Status: string
          /// Target session id.
          SessionId: string
          /// Session participant id derived from host config.
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

    let defaultExecutable engine =
        match engine with
        | Codex -> "codex"
        | Agy -> "agy"
        | Custom name -> name

    let configuredExecutable (config: HostConfig.HostConfig) engine =
        config.EngineExecutableOverrides
        |> Map.tryFind engine
        |> Option.defaultValue (defaultExecutable engine)

    let effectiveOptions (runtime: HostRuntime.HostRuntime) options =
        let engine = options.Engine |> Option.defaultValue runtime.Config.DefaultEngine
        let executablePath = options.ExecutablePath |> Option.defaultValue (configuredExecutable runtime.Config engine)
        let workingDirectory = options.WorkingDirectory |> Option.defaultValue (Directory.GetCurrentDirectory())
        let artifactRoot = options.ArtifactRoot |> Option.defaultValue runtime.Config.ArtifactRoot
        let timeout = options.Timeout |> Option.defaultValue runtime.Config.DefaultTimeout
        engine, executablePath, Path.GetFullPath workingDirectory, Path.GetFullPath artifactRoot, timeout

    let safeTags tags =
        if isNull (box tags) then [] else tags

    let envelopeToPromptMessage (message: MessageFabricEnvelope) =
        { Ref = MessageFabricBinding.envelopeToMessageRef message
          Body = message.Body
          ReceivedUtc = None
          Tags = safeTags message.Tags
          Metadata = Map.ofList [ "source", "ptcs-messagefabric" ] }

    let ensureParticipantAsync fabric participantId displayName =
        task {
            let binding = MessageFabricBinding.defaultBinding participantId
            let registration =
                { MessageFabricBinding.defaultRegistration with
                    DisplayName = Some displayName }

            let! _ = MessageFabricBinding.registerParticipantAsync fabric binding registration
            return ()
        }

    let replyBinding (runtime: HostRuntime.HostRuntime) (sessionBinding: MessageFabricBinding.SessionBinding) =
        { sessionBinding with
            ReplyParticipantId = runtime.Config.Ptcs.ReplyParticipantId }

    let emptyResult sessionId participantId =
        { Status = "empty"
          SessionId = sessionId
          SessionParticipantId = participantId
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

    /// Run one bounded session cycle. It polls once, runs the selected engine for pending messages, replies, then acknowledges.
    let runSingleCycleAsync (runtime: HostRuntime.HostRuntime) (options: SingleCycleOptions) (cancellationToken: CancellationToken) =
        task {
            match runtime.MessageFabric with
            | None -> return invalidOp "Host MessageFabric is not initialized."
            | Some fabric ->
                if String.IsNullOrWhiteSpace options.SessionId then
                    invalidArg (nameof options.SessionId) "Session id is required."

                let engine, executablePath, workingDirectory, artifactRoot, timeout = effectiveOptions runtime options

                if engine <> Agy then
                    invalidOp $"E2E single-cycle runner currently supports agy only; requested {RuntimePromptLoop.engineText engine}."

                let sessionBinding = HostControl.sessionBinding runtime.Config options.SessionId
                do! ensureParticipantAsync fabric sessionBinding.ParticipantId options.SessionId

                match runtime.Config.Ptcs.ReplyParticipantId with
                | Some replyParticipantId when replyParticipantId <> sessionBinding.ParticipantId ->
                    do! ensureParticipantAsync fabric replyParticipantId "codex.fs host"
                | _ -> ()

                let! batch = MessageFabricBinding.pollInboxAsync fabric sessionBinding None

                if batch.Messages.IsEmpty then
                    return emptyResult options.SessionId sessionBinding.ParticipantId
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
                              ParticipantId = sessionBinding.ParticipantId
                              Engine = engine
                              SurfaceId = Some Engine.Agy.V1_0.Print.SurfaceId
                              WorkingDirectory = workingDirectory
                              Messages = promptMessages
                              Policy =
                                { PromptAssembly.defaultPolicy with
                                    SystemInstruction = options.SystemInstruction
                                    MaxMessageBodyChars = Some 8000
                                    AdditionalContext = [ "e2e", "single-cycle verifier path" ] } }

                    let promptArtifact =
                        FileArtifactStore.writeText storeConfig sessionId runId PromptMarkdown "prompt.md" promptPlan.Prompt.Markdown startedUtc

                    let ptcsBatchArtifact =
                        FileArtifactStore.writeText storeConfig sessionId runId PtcsMessageBatchJsonl "ptcs-messages.jsonl" promptPlan.MessageBatchJsonl startedUtc

                    let executionPlan =
                        RuntimePromptLoop.planAgyPrintExecution
                            { PromptPlan = promptPlan
                              SessionId = sessionId
                              RunId = runId
                              ExecutablePath = executablePath
                              WorkingDirectory = workingDirectory
                              PromptPath = promptArtifact.Reference.Path
                              ArtifactDirectory = FileArtifactStore.runDirectory storeConfig sessionId runId
                              Timeout = timeout
                              AdditionalDirectories = options.AdditionalDirectories
                              Metadata = Map.ofList [ "cycle", "single"; "sessionParticipantId", sessionBinding.ParticipantId ] }

                    let requestArtifact =
                        FileArtifactStore.writeText storeConfig sessionId runId RequestJson "request.json" executionPlan.RequestJson startedUtc

                    let renderedArtifact =
                        FileArtifactStore.writeText storeConfig sessionId runId RenderedArgvJson "rendered-argv.json" executionPlan.RenderedCommandJson startedUtc

                    let! processResult =
                        ProcessRunner.runAsync
                            { Timeout = timeout
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
                            (replyBinding runtime sessionBinding)
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

                    let! ack = MessageFabricBinding.ackInboxAsync fabric sessionBinding batch.NextCursor

                    return
                        { Status = RuntimePromptLoop.outcomeText outcome
                          SessionId = options.SessionId
                          SessionParticipantId = sessionBinding.ParticipantId
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
