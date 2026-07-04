module CodexFs.Tests.Program

open System
open CodexFs.Compaction
open CodexFs.Domain
open CodexFs.PromptAssembly

let assertTrue name condition =
    if not condition then
        failwith $"Assertion failed: {name}"

let assertEqual name expected actual =
    if not (obj.Equals(expected, actual)) then
        failwith $"Assertion failed: {name}; expected={expected}; actual={actual}"

let assertContains name (expected: string) (actual: string) =
    assertTrue name (actual.Contains(expected, StringComparison.Ordinal))

let assertBefore name (first: string) (second: string) (actual: string) =
    let firstIndex = actual.IndexOf(first, StringComparison.Ordinal)
    let secondIndex = actual.IndexOf(second, StringComparison.Ordinal)
    assertTrue $"{name}: first marker exists" (firstIndex >= 0)
    assertTrue $"{name}: second marker exists" (secondIndex >= 0)
    assertTrue $"{name}: marker order" (firstIndex < secondIndex)

let messageRef messageId cursor fromParticipantId toParticipantId groupId correlationId =
    { MessageId = messageId
      Cursor = cursor
      FromParticipantId = fromParticipantId
      ToParticipantId = toParticipantId
      GroupId = groupId
      CorrelationId = correlationId }

let promptMessage ref body receivedUtc tags metadata =
    { Ref = ref
      Body = body
      ReceivedUtc = receivedUtc
      Tags = tags
      Metadata = metadata }

let firstMessage =
    promptMessage
        (messageRef "msg-001" (Some "cursor-001") "user.alice" (Some "agent.sess002") None (Some "corr-001"))
        "first user instruction"
        (Some(DateTimeOffset.Parse("2026-07-04T12:00:00Z")))
        [ "actionable"; "ptcs" ]
        (Map.ofList [ "kind", "direct"; "source", "ptcs" ])

let secondMessage =
    promptMessage
        (messageRef "msg-002" (Some "cursor-002") "agent.vega" (Some "agent.sess002") (Some "group.dev") None)
        "message 2 with ```embedded``` fence and enough body text to require truncation at the policy boundary."
        (Some(DateTimeOffset.Parse("2026-07-04T12:00:30Z")))
        [ "agent" ]
        (Map.ofList [ "kind", "reply" ])

let input =
    { SessionId = SessionId "sess-002"
      RunId = RunId "run-002"
      ParticipantId = "agent.sess002"
      Engine = Agy
      SurfaceId = Some "agy-print-1.0"
      WorkingDirectory = "workspace/codex.fs"
      Messages = [ firstMessage; secondMessage ]
      Policy =
        { CodexFs.PromptAssembly.defaultPolicy with
            SystemInstruction = Some "follow PTCS ordering"
            HistoryPath = Some "workspace/codex.fs/.sessions/sess-002/history.md"
            CurrentSummaryPath = Some "workspace/codex.fs/.sessions/sess-002/summary.md"
            MaxMessageBodyChars = Some 72
            AdditionalContext = [ "blockers", "PTCS durable profile remains out of this unit test." ] } }

let result = assemble input
let markdown = result.Markdown

assertContains "title" "# codex.fs session prompt" markdown
assertContains "session id" "- sessionId: sess-002" markdown
assertContains "run id" "- runId: run-002" markdown
assertContains "participant" "- participantId: agent.sess002" markdown
assertContains "engine" "- engine: agy" markdown
assertContains "surface" "- surfaceId: agy-print-1.0" markdown
assertContains "working directory" "- workingDirectory: workspace/codex.fs" markdown
assertContains "system instruction" "follow PTCS ordering" markdown
assertContains "history path" "workspace/codex.fs/.sessions/sess-002/history.md" markdown
assertContains "summary path" "workspace/codex.fs/.sessions/sess-002/summary.md" markdown
assertContains "additional context" "PTCS durable profile remains out of this unit test." markdown
assertBefore "message order" "### Message 1" "### Message 2" markdown
assertContains "message id" "- messageId: msg-001" markdown
assertContains "cursor" "- cursor: cursor-001" markdown
assertContains "from" "- from: user.alice" markdown
assertContains "to" "- to: agent.sess002" markdown
assertContains "group" "- group: group.dev" markdown
assertContains "correlation" "- correlationId: corr-001" markdown
assertContains "received" "- receivedUtc: 2026-07-04T12:00:00.0000000+00:00" markdown
assertContains "tags" "- tags: actionable, ptcs" markdown
assertContains "metadata" "  - kind: direct" markdown
assertContains "first body" "first user instruction" markdown
assertContains "safe fence" "````text\nmessage 2 with ```embedded``` fence" markdown
assertContains "truncation" "[truncated" markdown
assertEqual "message ref count" 2 result.MessageRefs.Length
assertEqual "first message ref" "msg-001" result.MessageRefs[0].MessageId
assertEqual "last cursor" (Some "cursor-002") result.LastCursor

printfn "TC-SESS-002 prompt batch assembly passed"

let compactMessageRef =
    messageRef "msg-blocker-001" (Some "cursor-blocker-001") "user.owner" (Some "agent.sess003") None None

let compactEntry entryId kind text messageRefs runRefs artifactRefs tags : CompactionEntry =
    { EntryId = entryId
      Kind = kind
      Text = text
      MessageRefs = messageRefs
      RunRefs = runRefs
      ArtifactRefs = artifactRefs
      Tags = tags
      CreatedUtc = Some(DateTimeOffset.Parse("2026-07-04T12:10:00Z")) }

let compactEntries =
    [ compactEntry "noise-001" Note "old note that can be dropped" [] [] [] []
      compactEntry
          "blocker-001"
          Blocker
          "PTCS durable profile decision remains open and must survive compaction."
          [ compactMessageRef ]
          []
          []
          [ "blocker"; "ptcs" ]
      compactEntry
          "decision-001"
          Decision
          "MVP compaction is deterministic rule-based; selected-engine compaction is deferred."
          []
          [ RunId "run-compact-1" ]
          []
          [ "decision" ]
      compactEntry
          "open-001"
          OpenItem
          "Wire compacted summary into the next prompt after artifact persistence."
          []
          []
          []
          [ "open" ]
      compactEntry
          "artifact-001"
          Artifact
          "Final assistant reply is persisted as an artifact reference."
          []
          []
          [ "artifacts/run-compact-1/final.md" ]
          [ "artifact" ]
      compactEntry "recent-001" Note "latest non-critical note should survive as recent context" [] [] [] [] ]

let compactPolicy =
    { CodexFs.Compaction.defaultPolicy with
        MaxSummaryChars = Some 2500
        RecentEntryCount = 1
        MaxEntryTextChars = Some 96 }

let compactResult = compact compactPolicy compactEntries
let compactMarkdown = compactResult.SummaryMarkdown

assertContains "compact title" "# codex.fs compacted history" compactMarkdown
assertContains "blocker retained" "PTCS durable profile decision remains open" compactMarkdown
assertContains "decision retained" "MVP compaction is deterministic rule-based" compactMarkdown
assertContains "open item retained" "Wire compacted summary into the next prompt" compactMarkdown
assertContains "message id retained" "msg-blocker-001" compactMarkdown
assertContains "run id retained" "run-compact-1" compactMarkdown
assertContains "artifact retained" "artifacts/run-compact-1/final.md" compactMarkdown
assertContains "recent retained" "latest non-critical note should survive" compactMarkdown
assertTrue "noise dropped" (compactResult.DroppedEntries |> List.exists (fun dropped -> dropped.EntryId = "noise-001" && dropped.Reason = NotRecent))
assertTrue "blocker entry id preserved" (compactResult.PreservedEntryIds |> List.contains "blocker-001")
assertTrue "decision entry id preserved" (compactResult.PreservedEntryIds |> List.contains "decision-001")
assertTrue "open entry id preserved" (compactResult.PreservedEntryIds |> List.contains "open-001")
assertTrue "artifact entry id preserved" (compactResult.PreservedEntryIds |> List.contains "artifact-001")
assertTrue "recent entry id preserved" (compactResult.PreservedEntryIds |> List.contains "recent-001")
assertEqual "preserved message id" "msg-blocker-001" compactResult.PreservedMessageRefs[0].MessageId
assertEqual "preserved run id" (RunId "run-compact-1") compactResult.PreservedRunRefs[0]
assertEqual "preserved artifact" "artifacts/run-compact-1/final.md" compactResult.PreservedArtifactRefs[0]
assertEqual "over budget" false compactResult.OverBudget

printfn "TC-SESS-003 compact preserves blockers passed"
