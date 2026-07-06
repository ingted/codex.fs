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
          /// Optional executable overrides keyed by engine family.
          EngineExecutableOverrides: Map<EngineKind, string>
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
          /// Render Agy permission auto-approval for this bounded run.
          AgyDangerouslySkipPermissions: bool
          /// Optional Codex model override used when no incoming intent tag supplies one.
          CodexModel: string option
          /// Render Codex approval/sandbox bypass for bounded Foreman/tool execution.
          CodexDangerouslyBypassApprovalsAndSandbox: bool
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
          /// Redacted run note artifact path relative to artifact root.
          RunNotePath: string
          /// PTCS reply message id.
          ReplyMessageId: string
          /// Reply body sent through MessageFabric.
          ReplyBody: string }

    /// Default executable name for a supported engine kind.
    let defaultExecutable engine =
        match engine with
        | Codex ->
            if OperatingSystem.IsWindows() then
                let appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                let npmNativeCodex =
                    Path.Combine(
                        appData,
                        "npm",
                        "node_modules",
                        "@openai",
                        "codex",
                        "node_modules",
                        "@openai",
                        "codex-win32-x64",
                        "vendor",
                        "x86_64-pc-windows-msvc",
                        "bin",
                        "codex.exe")

                if File.Exists npmNativeCodex then npmNativeCodex else "codex"
            else
                "codex"
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
          RunNotePath = String.Empty
          ReplyMessageId = String.Empty
          ReplyBody = String.Empty }

    let emptyAckResult options ackCursor =
        { emptyResult options with
            AckCursor = ackCursor }

    /// Effective engine settings selected for one concrete batch.
    type EffectiveRuntimeSelection =
        { Engine: EngineKind
          SurfaceId: string
          ExecutablePath: string
          CodexModel: string option
          CodexDangerouslyBypassApprovalsAndSandbox: bool }

    /// Engine output artifacts normalized across supported surfaces.
    type RuntimeOutputMapping =
        { Stdout: FileArtifactStore.StoredArtifact
          Stderr: FileArtifactStore.StoredArtifact
          EventJsonl: FileArtifactStore.StoredArtifact option
          FinalMessage: FileArtifactStore.StoredArtifact option
          Manifest: ArtifactManifest
          FinalMessageText: string option }

    let surfaceIdFor engine =
        match engine with
        | Codex -> Engine.Codex.V0_142.Exec.SurfaceId
        | Agy -> Engine.Agy.V1_0.Print.SurfaceId
        | Custom name -> $"custom:{name}"

    let tryParseEngineKind (text: string) =
        if String.IsNullOrWhiteSpace text then
            None
        else
            match text.Trim().ToLowerInvariant() with
            | "codex" -> Some Codex
            | "agy" -> Some Agy
            | value when value.StartsWith("custom:", StringComparison.Ordinal) ->
                let name = value.Substring("custom:".Length).Trim()

                if String.IsNullOrWhiteSpace name then None else Some(Custom name)
            | _ -> None

    let tagValue (prefix: string) (tag: string) =
        if isNull tag || not (tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) then
            None
        else
            let value = tag.Substring(prefix.Length).Trim()

            if String.IsNullOrWhiteSpace value then None else Some value

    let latestTagValue prefix (messages: PromptMessage list) =
        messages
        |> List.rev
        |> List.tryPick (fun message -> message.Tags |> List.tryPick (tagValue prefix))

    let nonDefaultModel (value: string option) =
        value
        |> Option.bind (fun text ->
            let trimmed = text.Trim()

            if String.IsNullOrWhiteSpace trimmed || trimmed.Equals("default", StringComparison.OrdinalIgnoreCase) then
                None
            else
                Some trimmed)

    let executableFor (options: RuntimeCycleOptions) engine =
        options.EngineExecutableOverrides
        |> Map.tryFind engine
        |> Option.orElseWith (fun () -> if engine = options.Engine then Some options.ExecutablePath else None)
        |> Option.defaultValue (defaultExecutable engine)

    let effectiveRuntimeSelection (options: RuntimeCycleOptions) (messages: PromptMessage list) =
        let engine =
            latestTagValue "engine:" messages
            |> Option.bind tryParseEngineKind
            |> Option.defaultValue options.Engine

        let approvalNever =
            latestTagValue "approval:" messages
            |> Option.exists (fun value -> value.Equals("never", StringComparison.OrdinalIgnoreCase))

        { Engine = engine
          SurfaceId = surfaceIdFor engine
          ExecutablePath = executableFor options engine
          CodexModel = latestTagValue "model:" messages |> Option.orElse options.CodexModel |> nonDefaultModel
          CodexDangerouslyBypassApprovalsAndSandbox = options.CodexDangerouslyBypassApprovalsAndSandbox || approvalNever }

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

        ()

    let isSelfAuthoredMessage (options: RuntimeCycleOptions) (message: MessageFabricEnvelope) =
        message.FromParticipantId.Equals(options.SessionParticipantId, StringComparison.OrdinalIgnoreCase)
        || (options.ReplyParticipantId
            |> Option.exists (fun participantId -> message.FromParticipantId.Equals(participantId, StringComparison.OrdinalIgnoreCase)))

    let readCodexFinalMessage (executionPlan: RuntimePromptLoop.RuntimeExecutionPlan) =
        executionPlan.Request.Metadata
        |> Map.tryFind "codex.outputLastMessagePath"
        |> Option.bind (fun path ->
            if File.Exists path then
                let text = File.ReadAllText(path, UTF8Encoding(false, true))

                if String.IsNullOrWhiteSpace text then None else Some text
            else
                None)

    let readStoredArtifactText (artifact: FileArtifactStore.StoredArtifact) =
        if File.Exists artifact.AbsolutePath then
            let text = File.ReadAllText(artifact.AbsolutePath, UTF8Encoding(false, true))

            if String.IsNullOrWhiteSpace text then None else Some text
        else
            None

    let mapProcessOutputArtifacts
        (storeConfig: FileArtifactStore.FileArtifactStoreConfig)
        (executionPlan: RuntimePromptLoop.RuntimeExecutionPlan)
        (processResult: ProcessRunner.ProcessRunResult)
        outcome
        : RuntimeOutputMapping =
        match executionPlan.Request.Engine with
        | Agy ->
            let capture: Engine.Agy.V1_0.Print.OutputCapture =
                { Stdout = processResult.Stdout
                  Stderr = processResult.Stderr
                  StartedUtc = processResult.StartedUtc
                  CompletedUtc = Some processResult.CompletedUtc
                  Outcome = outcome }

            let mapping =
                Engine.Agy.V1_0.Print.mapOutputArtifacts
                    storeConfig
                    executionPlan.Request
                    capture

            let finalMessageText =
                mapping.FinalMessage
                |> Option.bind readStoredArtifactText

            { Stdout = mapping.Stdout
              Stderr = mapping.Stderr
              EventJsonl = None
              FinalMessage = mapping.FinalMessage
              Manifest = mapping.Manifest
              FinalMessageText = finalMessageText }
        | Codex ->
            let finalMessageText = readCodexFinalMessage executionPlan

            let capture: Engine.Codex.V0_142.Exec.OutputCapture =
                { Stdout = processResult.Stdout
                  Stderr = processResult.Stderr
                  EventJsonl = None
                  FinalMessage = finalMessageText
                  StartedUtc = processResult.StartedUtc
                  CompletedUtc = Some processResult.CompletedUtc
                  Outcome = outcome }

            let mapping =
                Engine.Codex.V0_142.Exec.mapOutputArtifacts
                    storeConfig
                    executionPlan.Request
                    capture

            { Stdout = mapping.Stdout
              Stderr = mapping.Stderr
              EventJsonl = mapping.EventJsonl
              FinalMessage = mapping.FinalMessage
              Manifest = mapping.Manifest
              FinalMessageText = finalMessageText }
        | Custom name ->
            invalidOp $"PTCS runtime cycle does not support custom engine execution yet: {name}."

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
                let processableMessages =
                    batch.Messages
                    |> List.filter (isSelfAuthoredMessage options >> not)

                if processableMessages.IsEmpty then
                    let! ack = MessageFabricBinding.ackInboxAsync fabric binding batch.NextCursor
                    return emptyAckResult options (ack.Cursor |> Option.defaultValue String.Empty)
                else
                    let startedUtc = DateTimeOffset.UtcNow
                    let runId = RuntimePromptLoop.newRunId startedUtc
                    let storeConfig = { FileArtifactStore.ArtifactRoot = artifactRoot }
                    let sessionId = SessionId options.SessionId
                    let promptMessages = processableMessages |> List.map envelopeToPromptMessage
                    let effective = effectiveRuntimeSelection options promptMessages
                    let (RunId runIdText) = runId

                    let promptPlan =
                        RuntimePromptLoop.planPrompt
                            { SessionId = sessionId
                              RunId = runId
                              ParticipantId = options.SessionParticipantId
                              Engine = effective.Engine
                              SurfaceId = Some effective.SurfaceId
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

                    let baseMetadata =
                        Map.ofList
                            [ "cycle", "single"
                              "adapter", "ptcs-runtime-messagefabric"
                              "sessionParticipantId", options.SessionParticipantId
                              "effectiveEngine", RuntimePromptLoop.engineText effective.Engine
                              "effectiveSurfaceId", effective.SurfaceId ]

                    let artifactDirectory = FileArtifactStore.runDirectory storeConfig sessionId runId

                    let executionPlan =
                        match effective.Engine with
                        | Agy ->
                            RuntimePromptLoop.planAgyPrintExecution
                                { PromptPlan = promptPlan
                                  SessionId = sessionId
                                  RunId = runId
                                  ExecutablePath = effective.ExecutablePath
                                  WorkingDirectory = workingDirectory
                                  PromptPath = promptArtifact.Reference.Path
                                  ArtifactDirectory = artifactDirectory
                                  Timeout = options.Timeout
                                  AdditionalDirectories = options.AdditionalDirectories
                                  DangerouslySkipPermissions = options.AgyDangerouslySkipPermissions
                                  Metadata = baseMetadata }
                        | Codex ->
                            RuntimePromptLoop.planCodexExecExecution
                                { PromptPlan = promptPlan
                                  SessionId = sessionId
                                  RunId = runId
                                  ExecutablePath = effective.ExecutablePath
                                  WorkingDirectory = workingDirectory
                                  PromptPath = promptArtifact.Reference.Path
                                  ArtifactDirectory = artifactDirectory
                                  Timeout = options.Timeout
                                  AdditionalDirectories = options.AdditionalDirectories
                                  Model = effective.CodexModel
                                  DangerouslyBypassApprovalsAndSandbox = effective.CodexDangerouslyBypassApprovalsAndSandbox
                                  Metadata = baseMetadata }
                        | Custom name ->
                            invalidOp $"PTCS runtime cycle does not support custom engine execution yet: {name}."

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

                    let outputMapping = mapProcessOutputArtifacts storeConfig executionPlan processResult outcome

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

                    let finalPath = runResult.FinalMessagePath |> Option.defaultValue String.Empty

                    let noteArtifact =
                        FileArtifactStore.writeText
                            storeConfig
                            sessionId
                            runId
                            RunNoteMarkdown
                            "note.md"
                            (RuntimePromptLoop.runNoteText
                                { SessionId = sessionId
                                  RunId = runId
                                  Engine = effective.Engine
                                  SurfaceId = Some effective.SurfaceId
                                  Outcome = outcome
                                  ManifestPath = manifestRelativePath
                                  FinalMessagePath = finalPath
                                  ConsumedMessageIds = promptMessages |> List.map _.Ref.MessageId
                                  Summary = RuntimePromptLoop.redactedReplyText outputMapping.FinalMessageText processResult.Stdout processResult.Stderr |> RuntimePromptLoop.truncate 240
                                  StartedUtc = processResult.StartedUtc
                                  CompletedUtc = Some processResult.CompletedUtc })
                            processResult.CompletedUtc

                    let manifest =
                        { outputMapping.Manifest with
                            Artifacts =
                                [ promptArtifact.Reference
                                  ptcsBatchArtifact.Reference
                                  requestArtifact.Reference
                                  renderedArtifact.Reference
                                  outputMapping.Stdout.Reference
                                  outputMapping.Stderr.Reference
                                  match outputMapping.EventJsonl with
                                  | Some eventJsonl -> eventJsonl.Reference
                                  | None -> ()
                                  match outputMapping.FinalMessage with
                                  | Some finalMessage -> finalMessage.Reference
                                  | None -> ()
                                  resultArtifact.Reference
                                  noteArtifact.Reference ] }

                    let manifestPath = Path.Combine(FileArtifactStore.artifactRoot storeConfig, manifestRelativePath)
                    let manifestDirectory = Path.GetDirectoryName manifestPath

                    if String.IsNullOrWhiteSpace manifestDirectory then
                        invalidOp "Artifact manifest directory could not be resolved."

                    Directory.CreateDirectory manifestDirectory |> ignore
                    File.WriteAllText(manifestPath, RuntimePromptLoop.manifestText manifest, UTF8Encoding(false, true))

                    let targetParticipantId = processableMessages.Head.FromParticipantId

                    let replyIntent =
                        RuntimePromptLoop.replyIntent
                            runId
                            outcome
                            manifestRelativePath
                            finalPath
                            noteArtifact.Reference.Path
                            targetParticipantId
                            processResult.Stdout
                            processResult.Stderr
                            outputMapping.FinalMessageText

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
                                  RunNotePath = noteArtifact.Reference.Path
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
                          RunNotePath = noteArtifact.Reference.Path
                          ReplyMessageId = reply.MessageId
                          ReplyBody = replyIntent.Body }
        }
