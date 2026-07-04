namespace CodexFs

open System
open System.Text
open CodexFs.Domain

/// Pure prompt assembly helpers for session workers.
module PromptAssembly =

    /// One MessageFabric message plus caller-supplied prompt body.
    type PromptMessage =
        { /// MessageFabric reference metadata.
          Ref: PtcsMessageRef
          /// Message body to include in the engine prompt.
          Body: string
          /// UTC time observed by the host, when available.
          ReceivedUtc: DateTimeOffset option
          /// Non-secret tags associated with this message.
          Tags: string list
          /// Non-secret metadata associated with this message.
          Metadata: Map<string, string> }

    /// Prompt assembly policy.
    type PromptAssemblyPolicy =
        { /// Optional system instruction block prepended to the prompt.
          SystemInstruction: string option
          /// Persisted full history path for traceability.
          HistoryPath: string option
          /// Current compacted summary path, when available.
          CurrentSummaryPath: string option
          /// Include message metadata before each body.
          IncludeMessageMetadata: bool
          /// Optional maximum body length per message.
          MaxMessageBodyChars: int option
          /// Additional named context blocks.
          AdditionalContext: (string * string) list }

    /// Input data for one prompt assembly operation.
    type PromptAssemblyInput =
        { /// Session identity.
          SessionId: SessionId
          /// Run identity.
          RunId: RunId
          /// Participant id owned by the session.
          ParticipantId: string
          /// Engine kind selected for the run.
          Engine: EngineKind
          /// Concrete engine surface id, when selected.
          SurfaceId: string option
          /// Working directory for the engine run.
          WorkingDirectory: string
          /// Messages included in this prompt, in MessageFabric batch order.
          Messages: PromptMessage list
          /// Assembly policy.
          Policy: PromptAssemblyPolicy }

    /// Result produced by prompt assembly.
    type PromptAssemblyResult =
        { /// Markdown prompt text ready to persist as an artifact.
          Markdown: string
          /// Message refs included in the prompt.
          MessageRefs: PtcsMessageRef list
          /// Last cursor included in the prompt, when available.
          LastCursor: string option }

    /// Default prompt assembly policy.
    let defaultPolicy =
        { SystemInstruction = None
          HistoryPath = None
          CurrentSummaryPath = None
          IncludeMessageMetadata = true
          MaxMessageBodyChars = None
          AdditionalContext = [] }

    /// Render an engine kind as stable text.
    let engineKindText engine =
        match engine with
        | Codex -> "codex"
        | Agy -> "agy"
        | Custom name -> $"custom:{name}"

    /// Convert optional text to a stable display value.
    let optionText value =
        value |> Option.defaultValue "(none)"

    /// Normalize one-line metadata values for markdown bullets.
    let inlineText (value: string) =
        if isNull value then
            String.Empty
        else
            value.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\\n")

    /// Find the longest run of backticks in text.
    let longestBacktickRun (text: string) =
        if String.IsNullOrEmpty text then
            0
        else
            let mutable longest = 0
            let mutable current = 0

            for ch in text do
                if ch = '`' then
                    current <- current + 1
                    longest <- max longest current
                else
                    current <- 0

            longest

    /// Build a markdown code fence that cannot be closed by the supplied content.
    let markdownFence language (content: string) =
        let safeContent = if isNull content then String.Empty else content
        let fence = String.replicate (max 3 (longestBacktickRun safeContent + 1)) "`"
        let languageSuffix =
            match language with
            | Some text when not (String.IsNullOrWhiteSpace text) -> text.Trim()
            | _ -> String.Empty

        if String.IsNullOrEmpty languageSuffix then
            $"{fence}\n{safeContent}\n{fence}"
        else
            $"{fence}{languageSuffix}\n{safeContent}\n{fence}"

    /// Truncate text according to a max length policy.
    let truncateText maxChars (text: string) =
        match maxChars with
        | Some limit when limit < 0 -> invalidArg "maxChars" "Max message body chars cannot be negative."
        | Some limit when not (isNull text) && text.Length > limit ->
            let omitted = text.Length - limit
            text.Substring(0, limit) + $"\n\n[truncated {omitted} chars]"
        | _ -> if isNull text then String.Empty else text

    /// Return the last available cursor in a prompt message batch.
    let lastCursor messages =
        messages
        |> List.choose (fun message -> message.Ref.Cursor)
        |> List.tryLast

    /// Append a markdown bullet when a value exists.
    let appendOptionalBullet (builder: StringBuilder) label value =
        value
        |> Option.iter (fun text -> builder.AppendLine($"- {label}: {inlineText text}") |> ignore)

    /// Render one prompt message into markdown.
    let renderMessage includeMetadata maxBodyChars index (message: PromptMessage) =
        let builder = StringBuilder()
        builder.AppendLine($"### Message {index + 1}") |> ignore

        if includeMetadata then
            builder.AppendLine($"- messageId: {inlineText message.Ref.MessageId}") |> ignore
            appendOptionalBullet builder "cursor" message.Ref.Cursor
            builder.AppendLine($"- from: {inlineText message.Ref.FromParticipantId}") |> ignore
            appendOptionalBullet builder "to" message.Ref.ToParticipantId
            appendOptionalBullet builder "group" message.Ref.GroupId
            appendOptionalBullet builder "correlationId" message.Ref.CorrelationId

            message.ReceivedUtc
            |> Option.iter (fun receivedUtc -> builder.AppendLine($"- receivedUtc: {receivedUtc:O}") |> ignore)

            if not message.Tags.IsEmpty then
                let tagsText = message.Tags |> List.map inlineText |> String.concat ", "
                builder.AppendLine($"- tags: {tagsText}") |> ignore

            if not message.Metadata.IsEmpty then
                builder.AppendLine("- metadata:") |> ignore

                message.Metadata
                |> Map.toList
                |> List.iter (fun (key, value) -> builder.AppendLine($"  - {inlineText key}: {inlineText value}") |> ignore)

            builder.AppendLine() |> ignore

        builder.AppendLine(markdownFence (Some "text") (truncateText maxBodyChars message.Body)) |> ignore
        builder.ToString().TrimEnd()

    /// Assemble one deterministic markdown prompt.
    let assemble (input: PromptAssemblyInput) =
        let (SessionId sessionId) = input.SessionId
        let (RunId runId) = input.RunId
        let builder = StringBuilder()

        builder.AppendLine("# codex.fs session prompt") |> ignore
        builder.AppendLine() |> ignore
        builder.AppendLine("## Run") |> ignore
        builder.AppendLine($"- sessionId: {inlineText sessionId}") |> ignore
        builder.AppendLine($"- runId: {inlineText runId}") |> ignore
        builder.AppendLine($"- participantId: {inlineText input.ParticipantId}") |> ignore
        builder.AppendLine($"- engine: {engineKindText input.Engine}") |> ignore
        builder.AppendLine($"- surfaceId: {optionText input.SurfaceId |> inlineText}") |> ignore
        builder.AppendLine($"- workingDirectory: {inlineText input.WorkingDirectory}") |> ignore
        builder.AppendLine() |> ignore

        input.Policy.SystemInstruction
        |> Option.iter (fun instruction ->
            builder.AppendLine("## System instruction") |> ignore
            builder.AppendLine(markdownFence (Some "text") instruction) |> ignore
            builder.AppendLine() |> ignore)

        builder.AppendLine("## Context references") |> ignore
        builder.AppendLine($"- historyPath: {optionText input.Policy.HistoryPath |> inlineText}") |> ignore
        builder.AppendLine($"- currentSummaryPath: {optionText input.Policy.CurrentSummaryPath |> inlineText}") |> ignore
        builder.AppendLine() |> ignore

        if not input.Policy.AdditionalContext.IsEmpty then
            builder.AppendLine("## Additional context") |> ignore

            input.Policy.AdditionalContext
            |> List.iteri (fun index (name, content) ->
                builder.AppendLine($"### Context {index + 1}: {inlineText name}") |> ignore
                builder.AppendLine(markdownFence (Some "text") content) |> ignore
                builder.AppendLine() |> ignore)

        builder.AppendLine("## Message batch") |> ignore

        if input.Messages.IsEmpty then
            builder.AppendLine("(empty)") |> ignore
        else
            input.Messages
            |> List.iteri (fun index message ->
                builder.AppendLine(renderMessage input.Policy.IncludeMessageMetadata input.Policy.MaxMessageBodyChars index message) |> ignore
                builder.AppendLine() |> ignore)

        { Markdown = builder.ToString().TrimEnd() + "\n"
          MessageRefs = input.Messages |> List.map _.Ref
          LastCursor = lastCursor input.Messages }
