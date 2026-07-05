namespace CodexFs

open System
open System.Text.Json
open CodexFs.Artifacts
open CodexFs.Domain
open CodexFs.PromptAssembly

/// Reusable prompt-loop planning helpers shared by host and future actors.
module RuntimePromptLoop =

    /// Input for deterministic prompt planning.
    type RuntimePromptInput =
        { /// Session identity.
          SessionId: SessionId
          /// Run identity.
          RunId: RunId
          /// Participant id owned by the worker/session.
          ParticipantId: string
          /// Engine family selected for this turn.
          Engine: EngineKind
          /// Concrete engine surface id.
          SurfaceId: string option
          /// Engine working directory.
          WorkingDirectory: string
          /// Messages consumed by this turn.
          Messages: PromptMessage list
          /// Prompt assembly policy.
          Policy: PromptAssemblyPolicy }

    /// Deterministic prompt-side plan for one runtime turn.
    type RuntimePromptPlan =
        { /// Assembled markdown prompt.
          Prompt: PromptAssemblyResult
          /// JSON Lines capture of consumed MessageFabric messages.
          MessageBatchJsonl: string }

    /// Input for Agy print-mode execution planning.
    type AgyPrintExecutionInput =
        { /// Prompt-side plan produced before writing the prompt artifact.
          PromptPlan: RuntimePromptPlan
          /// Session identity.
          SessionId: SessionId
          /// Run identity.
          RunId: RunId
          /// Agy executable path or command.
          ExecutablePath: string
          /// Engine working directory.
          WorkingDirectory: string
          /// Stored prompt artifact path recorded in the run request.
          PromptPath: string
          /// Artifact directory for this run.
          ArtifactDirectory: string
          /// Engine timeout.
          Timeout: TimeSpan
          /// Additional directories exposed to the engine.
          AdditionalDirectories: string list
          /// Render Agy permission auto-approval for bounded Foreman/tool execution.
          DangerouslySkipPermissions: bool
          /// Non-secret metadata attached to the run request.
          Metadata: Map<string, string> }

    /// Execution-side plan for one Agy print-mode turn.
    type RuntimeExecutionPlan =
        { /// Normalized run request.
          Request: RunRequest
          /// Stable JSON representation of the run request.
          RequestJson: string
          /// Rendered engine command.
          RenderedCommand: Engine.RenderedCommand
          /// Stable JSON representation of the rendered command.
          RenderedCommandJson: string
          /// Process runner command derived from the rendered command.
          ProcessCommand: ProcessRunner.ProcessCommand }

    /// Reply intent emitted by runtime after process output is persisted.
    type RuntimeReplyIntent =
        { /// Direct target participant.
          TargetParticipantId: string
          /// Redacted reply body suitable for MessageFabric.
          Body: string
          /// Non-secret MessageFabric tags.
          Tags: string list
          /// Correlation id for the reply.
          CorrelationId: string option }

    /// MessageFabric reply evidence written before acking the consumed cursor.
    type RuntimeReplyEvidence =
        { /// PTCS reply message id.
          MessageId: string
          /// PTCS reply body.
          Body: string }

    /// Ready-to-ack boundary captured after reply evidence and before inbox ack.
    type RuntimeReadyToAckBoundary =
        { /// Session identity.
          SessionId: SessionId
          /// Run identity.
          RunId: RunId
          /// Cursor selected for ack after all prior evidence is durable.
          SelectedCursor: string option
          /// Message ids consumed by this turn.
          ConsumedMessageIds: string list
          /// Reply evidence returned by MessageFabric.
          Reply: RuntimeReplyEvidence
          /// Artifact manifest path relative to artifact root.
          ArtifactManifestPath: string
          /// Final message path relative to artifact root.
          FinalMessagePath: string
          /// Run note path relative to artifact root.
          RunNotePath: string
          /// True when boundary is written before acking MessageFabric.
          PersistedBeforeAck: bool }

    /// Input for redacted human-readable run note generation.
    type RuntimeRunNoteInput =
        { /// Session identity.
          SessionId: SessionId
          /// Run identity.
          RunId: RunId
          /// Engine family used by the run.
          Engine: EngineKind
          /// Concrete engine surface id.
          SurfaceId: string option
          /// Terminal run outcome.
          Outcome: RunOutcome
          /// Manifest path relative to artifact root.
          ManifestPath: string
          /// Final message path relative to artifact root.
          FinalMessagePath: string
          /// Consumed PTCS message ids.
          ConsumedMessageIds: string list
          /// Redacted one-line final/failure summary.
          Summary: string
          /// UTC run start.
          StartedUtc: DateTimeOffset
          /// UTC run completion.
          CompletedUtc: DateTimeOffset option }

    let jsonOptions =
        JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true)

    let json value =
        JsonSerializer.Serialize(value, jsonOptions)

    /// Create a run id using a UTC timestamp plus a short random suffix.
    let newRunId (now: DateTimeOffset) =
        let suffix = Guid.NewGuid().ToString("N").Substring(0, 8)
        RunId($"run-{now:yyyyMMddHHmmssfff}-{suffix}")

    /// Render an engine kind as stable text for artifacts and replies.
    let engineText engine =
        match engine with
        | Codex -> "codex"
        | Agy -> "agy"
        | Custom name -> $"custom:{name}"

    /// Render a normalized run outcome as stable text.
    let outcomeText outcome =
        match outcome with
        | Completed -> "completed"
        | Failed -> "failed"
        | TimedOut -> "timed-out"
        | Cancelled -> "cancelled"

    /// Map process runner outcome to the normalized runtime outcome.
    let processOutcome (result: ProcessRunner.ProcessRunResult) =
        match result.Outcome, result.ExitCode with
        | ProcessRunner.Exited, Some 0 -> Completed
        | ProcessRunner.TimedOut, _ -> TimedOut
        | ProcessRunner.Cancelled, _ -> Cancelled
        | _ -> Failed

    /// Serialize consumed MessageFabric messages as JSON Lines.
    let messageBatchJsonl (messages: PromptMessage list) =
        messages
        |> List.map (fun message ->
            json
                {| messageId = message.Ref.MessageId
                   fromParticipantId = message.Ref.FromParticipantId
                   body = message.Body
                   tags = message.Tags |})
        |> String.concat Environment.NewLine
        |> fun text -> if String.IsNullOrEmpty text then text else text + Environment.NewLine

    /// Plan prompt text and message-batch evidence for one runtime turn.
    let planPrompt (input: RuntimePromptInput) =
        let prompt =
            PromptAssembly.assemble
                { SessionId = input.SessionId
                  RunId = input.RunId
                  ParticipantId = input.ParticipantId
                  Engine = input.Engine
                  SurfaceId = input.SurfaceId
                  WorkingDirectory = input.WorkingDirectory
                  Messages = input.Messages
                  Policy = input.Policy }

        { Prompt = prompt
          MessageBatchJsonl = messageBatchJsonl input.Messages }

    /// Serialize an artifact reference for manifest JSON output.
    let artifactRefDto (ref: ArtifactRef) =
        {| kind = ref.Kind.ToString()
           path = ref.Path
           sha256 = ref.Sha256
           size = ref.Size
           createdUtc = ref.CreatedUtc |}

    /// Serialize an artifact manifest as JSON.
    let manifestText (manifest: ArtifactManifest) =
        let (RunId runId) = manifest.RunId
        let (SessionId sessionId) = manifest.SessionId

        json
            {| runId = runId
               sessionId = sessionId
               engine = engineText manifest.Engine
               surfaceId = manifest.SurfaceId
               startedUtc = manifest.StartedUtc
               completedUtc = manifest.CompletedUtc
               outcome = outcomeText manifest.Outcome
               artifacts = manifest.Artifacts |> List.map artifactRefDto |}

    /// Serialize a normalized run result as JSON.
    let runResultText (result: RunResult) =
        let (RunId runId) = result.RunId

        json
            {| runId = runId
               outcome = outcomeText result.Outcome
               exitCode = result.ExitCode
               startedUtc = result.StartedUtc
               completedUtc = result.CompletedUtc
               artifactManifestPath = result.ArtifactManifestPath
               finalMessagePath = result.FinalMessagePath |}

    /// Serialize a normalized run request as JSON.
    let requestText (request: RunRequest) =
        let (RunId runId) = request.RunId
        let (SessionId sessionId) = request.SessionId

        json
            {| runId = runId
               sessionId = sessionId
               engine = engineText request.Engine
               surfaceId = request.SurfaceId
               workingDirectory = request.WorkingDirectory
               promptPath = request.PromptPath
               artifactDirectory = request.ArtifactDirectory
               timeout = request.Timeout.ToString()
               additionalDirectories = request.AdditionalDirectories
               ptcsMessageIds = request.PtcsMessages |> List.map _.MessageId
               metadata = request.Metadata |}

    /// Serialize rendered command metadata as JSON.
    let renderedCommandText (rendered: Engine.RenderedCommand) =
        json
            {| fileName = rendered.FileName
               arguments = rendered.Arguments
               workingDirectory = rendered.WorkingDirectory
               redactedDisplay = rendered.RedactedDisplay |}

    /// Build an Agy print-mode command for the assembled prompt.
    let buildAgyPrintCommand executablePath workingDirectory timeout dangerouslySkipPermissions promptMarkdown =
        let agyArgs =
            { Engine.Agy.V1_0.Print.emptyArgs with
                Print = true
                PromptText = Some promptMarkdown
                PrintTimeout = Some timeout
                DangerouslySkipPermissions = dangerouslySkipPermissions }

        Engine.Agy.V1_0.Print.renderCommand executablePath workingDirectory agyArgs

    /// Convert a rendered command to a process runner command.
    let processCommand (rendered: Engine.RenderedCommand) : ProcessRunner.ProcessCommand =
        { FileName = rendered.FileName
          Arguments = rendered.Arguments
          WorkingDirectory = Some rendered.WorkingDirectory
          Environment = rendered.Environment }

    /// Plan normalized request and rendered argv for an Agy print-mode run.
    let planAgyPrintExecution (input: AgyPrintExecutionInput) =
        let request =
            { RunId = input.RunId
              SessionId = input.SessionId
              Engine = Agy
              SurfaceId = Some Engine.Agy.V1_0.Print.SurfaceId
              WorkingDirectory = input.WorkingDirectory
              PromptPath = input.PromptPath
              ArtifactDirectory = input.ArtifactDirectory
              Timeout = input.Timeout
              AdditionalDirectories = input.AdditionalDirectories
              PtcsMessages = input.PromptPlan.Prompt.MessageRefs
              PtcsTask = None
              Metadata = input.Metadata }

        let rendered =
            buildAgyPrintCommand
                input.ExecutablePath
                input.WorkingDirectory
                input.Timeout
                input.DangerouslySkipPermissions
                input.PromptPlan.Prompt.Markdown

        { Request = request
          RequestJson = requestText request
          RenderedCommand = rendered
          RenderedCommandJson = renderedCommandText rendered
          ProcessCommand = processCommand rendered }

    let truncate chars (text: string) =
        let safeText =
            if isNull text then
                String.Empty
            else
                text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", " ").Trim()

        if safeText.Length <= chars then
            safeText
        else
            safeText.Substring(0, chars) + "..."

    /// Produce a redacted one-line process output summary.
    let redactedSummary stdout stderr =
        let candidate =
            if String.IsNullOrWhiteSpace stdout then
                stderr
            else
                stdout

        Redaction.redactHighRisk candidate
        |> fun result -> truncate 240 result.Text

    /// Build the redacted run note used by Web, CLI and compaction previews.
    let runNoteText (input: RuntimeRunNoteInput) =
        let (SessionId sessionIdText) = input.SessionId
        let (RunId runIdText) = input.RunId

        let consumed =
            match input.ConsumedMessageIds with
            | [] -> "- none"
            | ids -> ids |> List.map (fun id -> "- " + id) |> String.concat Environment.NewLine

        let surfaceIdText = input.SurfaceId |> Option.defaultValue ""

        let completedUtcText =
            input.CompletedUtc
            |> Option.map (fun value -> value.ToString("O"))
            |> Option.defaultValue ""

        [ "# codex.fs run note"
          ""
          "## Run"
          ""
          $"- sessionId: {sessionIdText}"
          $"- runId: {runIdText}"
          $"- engine: {engineText input.Engine}"
          $"- surfaceId: {surfaceIdText}"
          $"- outcome: {outcomeText input.Outcome}"
          $"- startedUtc: {input.StartedUtc:O}"
          $"- completedUtc: {completedUtcText}"
          ""
          "## Summary"
          ""
          input.Summary
          ""
          "## Consumed PTCS Messages"
          ""
          consumed
          ""
          "## Artifact Refs"
          ""
          $"- manifest: {input.ManifestPath}"
          $"- final: {input.FinalMessagePath}"
          ""
          "Raw prompt, stdout and stderr artifacts may be private/local and are not repeated in this note." ]
        |> String.concat Environment.NewLine
        |> fun text -> text + Environment.NewLine

    /// Build the redacted reply intent for MessageFabric.
    let replyIntent runId outcome manifestRelativePath finalPath notePath targetParticipantId stdout stderr =
        let (RunId runIdText) = runId

        let body =
            $"run {runIdText} {outcomeText outcome}; manifest={manifestRelativePath}; final={finalPath}; note={notePath}; summary={redactedSummary stdout stderr}"

        { TargetParticipantId = targetParticipantId
          Body = body
          Tags = [ "codex.fs"; "e2e"; "run"; outcomeText outcome ]
          CorrelationId = Some runIdText }

    /// Serialize ready-to-ack boundary evidence as JSON.
    let readyToAckBoundaryText (boundary: RuntimeReadyToAckBoundary) =
        let (SessionId sessionIdText) = boundary.SessionId
        let (RunId runIdText) = boundary.RunId

        json
            {| phase = "ready-to-ack"
               sessionId = sessionIdText
               runId = runIdText
               selectedCursor = boundary.SelectedCursor
               consumedMessageIds = boundary.ConsumedMessageIds
               replyMessageId = boundary.Reply.MessageId
               replyBody = boundary.Reply.Body
               artifactManifestPath = boundary.ArtifactManifestPath
               finalMessagePath = boundary.FinalMessagePath
               runNotePath = boundary.RunNotePath
               persistedBeforeAck = boundary.PersistedBeforeAck |}
