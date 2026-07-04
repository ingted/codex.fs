namespace CodexFs

open System
open System.Text
open CodexFs.Domain

/// Pure local history compaction helpers.
module Compaction =

    /// History entry kind used by the rule-based compactor.
    type CompactionEntryKind =
        /// Conversation message or message-derived note.
        | Message
        /// Accepted design or implementation decision.
        | Decision
        /// Active blocker or unresolved dependency.
        | Blocker
        /// Open task that still needs future action.
        | OpenItem
        /// Engine run summary or result note.
        | Run
        /// Artifact reference or artifact-derived note.
        | Artifact
        /// General context note.
        | Note

    /// One persisted history entry supplied by the host.
    type CompactionEntry =
        { /// Stable entry identity in the session history.
          EntryId: string
          /// Entry category.
          Kind: CompactionEntryKind
          /// Entry body text.
          Text: string
          /// PTCS messages referenced by this history entry.
          MessageRefs: PtcsMessageRef list
          /// Engine run ids referenced by this history entry.
          RunRefs: RunId list
          /// Artifact paths or stable artifact references.
          ArtifactRefs: string list
          /// Non-secret tags associated with this history entry.
          Tags: string list
          /// Entry timestamp, when known.
          CreatedUtc: DateTimeOffset option }

    /// Deterministic local compaction policy.
    type CompactionPolicy =
        { /// Optional maximum summary size in characters.
          MaxSummaryChars: int option
          /// Number of recent non-critical entries to keep.
          RecentEntryCount: int
          /// Optional maximum text body length per rendered entry.
          MaxEntryTextChars: int option
          /// Entry kinds that must be retained even when old.
          PreserveKinds: Set<CompactionEntryKind> }

    /// Reason why an entry was not included in the compacted summary.
    type DroppedEntryReason =
        /// Entry was neither retention-sensitive nor recent.
        | NotRecent
        /// Entry was recent but did not fit the configured summary budget.
        | Budget

    /// Dropped history entry metadata.
    type DroppedEntry =
        { /// Dropped entry id.
          EntryId: string
          /// Drop reason.
          Reason: DroppedEntryReason }

    /// Result of one local compaction operation.
    type CompactionResult =
        { /// Markdown summary to append into future prompts.
          SummaryMarkdown: string
          /// Entry ids retained in the summary.
          PreservedEntryIds: string list
          /// Entries omitted from the summary.
          DroppedEntries: DroppedEntry list
          /// PTCS message refs retained in the summary.
          PreservedMessageRefs: PtcsMessageRef list
          /// Run ids retained in the summary.
          PreservedRunRefs: RunId list
          /// Artifact refs retained in the summary.
          PreservedArtifactRefs: string list
          /// True when mandatory retained content exceeds `MaxSummaryChars`.
          OverBudget: bool }

    /// Default retention-sensitive entry kinds.
    let defaultPreserveKinds =
        set [ Decision; Blocker; OpenItem; Run; Artifact ]

    /// Default deterministic local compaction policy.
    let defaultPolicy =
        { MaxSummaryChars = None
          RecentEntryCount = 8
          MaxEntryTextChars = Some 4000
          PreserveKinds = defaultPreserveKinds }

    /// Render an entry kind as stable text.
    let entryKindText kind =
        match kind with
        | Message -> "message"
        | Decision -> "decision"
        | Blocker -> "blocker"
        | OpenItem -> "open-item"
        | Run -> "run"
        | Artifact -> "artifact"
        | Note -> "note"

    /// Render a dropped-entry reason as stable text.
    let droppedReasonText reason =
        match reason with
        | NotRecent -> "not-recent"
        | Budget -> "budget"

    /// Convert a run id to stable text.
    let runIdText (RunId value) = value

    /// Return true when an entry must survive compaction.
    let isRetentionSensitive (policy: CompactionPolicy) (entry: CompactionEntry) =
        policy.PreserveKinds.Contains entry.Kind
        || not entry.MessageRefs.IsEmpty
        || not entry.RunRefs.IsEmpty
        || not entry.ArtifactRefs.IsEmpty

    /// Return entry ids for the tail entries retained as recent context.
    let recentEntryIds recentEntryCount (entries: CompactionEntry list) =
        if recentEntryCount <= 0 then
            Set.empty
        else
            entries
            |> List.rev
            |> List.truncate recentEntryCount
            |> List.map _.EntryId
            |> Set.ofList

    /// Validate a compaction policy before use.
    let validatePolicy policy =
        policy.MaxSummaryChars
        |> Option.iter (fun value ->
            if value <= 0 then
                invalidArg "policy" "MaxSummaryChars must be positive when supplied.")

        if policy.RecentEntryCount < 0 then
            invalidArg "policy" "RecentEntryCount cannot be negative."

        policy.MaxEntryTextChars
        |> Option.iter (fun value ->
            if value < 0 then
                invalidArg "policy" "MaxEntryTextChars cannot be negative.")

    /// Render one retained history entry.
    let renderEntry (policy: CompactionPolicy) (entry: CompactionEntry) =
        let builder = StringBuilder()
        builder.AppendLine($"### {entryKindText entry.Kind}: {PromptAssembly.inlineText entry.EntryId}") |> ignore

        entry.CreatedUtc
        |> Option.iter (fun createdUtc -> builder.AppendLine($"- createdUtc: {createdUtc:O}") |> ignore)

        if not entry.Tags.IsEmpty then
            let tagsText = entry.Tags |> List.map PromptAssembly.inlineText |> String.concat ", "
            builder.AppendLine($"- tags: {tagsText}") |> ignore

        if not entry.MessageRefs.IsEmpty then
            let messageIds =
                entry.MessageRefs
                |> List.map (fun message -> PromptAssembly.inlineText message.MessageId)
                |> String.concat ", "

            builder.AppendLine($"- ptcsMessageIds: {messageIds}") |> ignore

        if not entry.RunRefs.IsEmpty then
            let runIds = entry.RunRefs |> List.map runIdText |> List.map PromptAssembly.inlineText |> String.concat ", "
            builder.AppendLine($"- runIds: {runIds}") |> ignore

        if not entry.ArtifactRefs.IsEmpty then
            let artifactRefs = entry.ArtifactRefs |> List.map PromptAssembly.inlineText |> String.concat ", "
            builder.AppendLine($"- artifactRefs: {artifactRefs}") |> ignore

        builder.AppendLine() |> ignore
        builder.AppendLine(PromptAssembly.markdownFence (Some "text") (PromptAssembly.truncateText policy.MaxEntryTextChars entry.Text)) |> ignore
        builder.ToString().TrimEnd()

    /// Build a deterministic compacted markdown summary.
    let compact (policy: CompactionPolicy) (entries: CompactionEntry list) =
        validatePolicy policy
        let recentIds = recentEntryIds policy.RecentEntryCount entries
        let builder = StringBuilder()
        let mutable preservedEntries: CompactionEntry list = []
        let mutable droppedEntries: DroppedEntry list = []

        let appendLine (text: string) =
            builder.AppendLine(text) |> ignore

        appendLine "# codex.fs compacted history"
        appendLine ""
        appendLine "## Policy"
        let maxSummaryCharsText = policy.MaxSummaryChars |> Option.map string |> Option.defaultValue "(none)"
        let maxEntryTextCharsText = policy.MaxEntryTextChars |> Option.map string |> Option.defaultValue "(none)"
        appendLine $"- maxSummaryChars: {maxSummaryCharsText}"
        appendLine $"- recentEntryCount: {policy.RecentEntryCount}"
        appendLine $"- maxEntryTextChars: {maxEntryTextCharsText}"
        appendLine ""
        appendLine "## Retained entries"

        for entry in entries do
            let sensitive = isRetentionSensitive policy entry
            let recent = recentIds.Contains entry.EntryId
            let rendered = renderEntry policy entry
            let projectedLength = builder.Length + rendered.Length + 2

            if sensitive || recent then
                match policy.MaxSummaryChars with
                | Some limit when not sensitive && projectedLength > limit ->
                    droppedEntries <- { EntryId = entry.EntryId; Reason = Budget } :: droppedEntries
                | _ ->
                    appendLine rendered
                    appendLine ""
                    preservedEntries <- entry :: preservedEntries
            else
                droppedEntries <- { EntryId = entry.EntryId; Reason = NotRecent } :: droppedEntries

        if preservedEntries.IsEmpty then
            appendLine "(empty)"
            appendLine ""

        let preservedEntriesInOrder = preservedEntries |> List.rev
        let droppedEntriesInOrder = droppedEntries |> List.rev

        appendLine "## Dropped entries"

        if droppedEntriesInOrder.IsEmpty then
            appendLine "(none)"
        else
            droppedEntriesInOrder
            |> List.iter (fun dropped -> appendLine $"- {PromptAssembly.inlineText dropped.EntryId}: {droppedReasonText dropped.Reason}")

        let overBudget =
            match policy.MaxSummaryChars with
            | Some limit -> builder.Length > limit
            | None -> false

        let distinctBy key values =
            values |> Seq.distinctBy key |> Seq.toList

        { SummaryMarkdown = builder.ToString().TrimEnd() + "\n"
          PreservedEntryIds = preservedEntriesInOrder |> List.map _.EntryId
          DroppedEntries = droppedEntriesInOrder
          PreservedMessageRefs =
            preservedEntriesInOrder
            |> List.collect _.MessageRefs
            |> distinctBy _.MessageId
          PreservedRunRefs =
            preservedEntriesInOrder
            |> List.collect _.RunRefs
            |> distinctBy runIdText
          PreservedArtifactRefs =
            preservedEntriesInOrder
            |> List.collect _.ArtifactRefs
            |> distinctBy id
          OverBudget = overBudget }
