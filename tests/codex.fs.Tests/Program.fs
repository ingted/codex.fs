module CodexFs.Tests.Program

open System
open System.Threading
open System.Threading.Tasks
open CodexFs.Compaction
open CodexFs.Domain
open CodexFs.PromptAssembly
open CodexFs.Ptcs

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

let runTask (task: Task<'T>) =
    task.GetAwaiter().GetResult()

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

assertEqual "ptcs package id" "PulseTrade.Comm.Spa" PtcsReference.pulseTradeCommSpaPackageId
assertEqual "ptcs version" "0.2.5-beta71" PtcsReference.pulseTradeCommSpaVersion
assertEqual "ptcs exact range" "[0.2.5-beta71]" PtcsReference.pulseTradeCommSpaVersionRange
assertEqual "ptcs aligned argu range" "[10.1.301]" PtcsReference.fAkkaArguVersionRange
assertContains "message fabric type" "PulseTrade.Comm.Spa.CommSpaMessageFabric" PtcsReference.messageFabricType.FullName
assertContains "actor fabric options type" "PulseTrade.Comm.Spa.CommSpaActorFabricOptions" PtcsReference.actorFabricOptionsType.FullName

printfn "TC-PTCS-001 PTCS restore/reference passed"

let fakeGithubToken = "ghp_" + String.replicate 24 "a"

let hostSettings =
    Map.ofList
        [ "artifact.root", ".codex.fs/host-artifacts"
          "engine.default", "agy"
          "engine.enabled", "codex, agy"
          "engine.codex.executable", $"codex.exe --token {fakeGithubToken}"
          "timeout.default", "00:05:00"
          "message.maxPendingPerTurn", "25"
          "control.protocol", "http"
          "control.bindAddress", "192.168.10.20"
          "control.port", "8788"
          "control.advertiseUri", "http://192.168.10.20:8788"
          "control.allowLoopbackOnly", "false"
          "apiDocs.generateXmlDocs", "true"
          "apiDocs.generateOpenApi", "true"
          "apiDocs.exposeSwaggerUi", "true"
          "apiDocs.swaggerRoutePrefix", "docs"
          "apiDocs.includeExamples", "true"
          "ptcs.fabricMode", "caller-owned-cluster"
          "ptcs.sessionParticipantPrefix", "agent.codexfs"
          "ptcs.replyParticipantId", "agent.codexfs.host"
          "ptcs.durableAgentTasks", "false"
          "ptcs.defaultInboxLimit", "25"
          "compaction.maxSummaryChars", "1200"
          "compaction.recentEntryCount", "3"
          "compaction.maxEntryTextChars", "240"
          "redaction.enableHighRiskRules", "true" ]

let hostLoad = CodexFs.HostConfig.loadFromMap hostSettings
let hostErrors = hostLoad.Issues |> List.filter (fun issue -> issue.Severity = CodexFs.HostConfig.IssueError)
assertEqual "host config error count" 0 hostErrors.Length
assertTrue "host config loaded" hostLoad.Config.IsSome

let hostConfig =
    hostLoad.Config |> Option.defaultWith (fun () -> failwith "expected host config")

assertEqual "host artifact root" ".codex.fs/host-artifacts" hostConfig.ArtifactRoot
assertEqual "host default engine" Agy hostConfig.DefaultEngine
assertTrue "host codex enabled" (hostConfig.EnabledEngines |> List.contains Codex)
assertTrue "host agy enabled" (hostConfig.EnabledEngines |> List.contains Agy)
assertEqual "host codex override" $"codex.exe --token {fakeGithubToken}" hostConfig.EngineExecutableOverrides[Codex]
assertEqual "host timeout" (TimeSpan.FromMinutes 5.0) hostConfig.DefaultTimeout
assertEqual "host pending limit" 25 hostConfig.MaxPendingMessagesPerTurn
assertEqual "host bind address" "192.168.10.20" hostConfig.ControlEndpoint.BindAddress
assertEqual "host advertise uri" "http://192.168.10.20:8788" hostConfig.ControlEndpoint.AdvertiseUri
assertEqual "host loopback false" false hostConfig.ControlEndpoint.AllowLoopbackOnly
assertEqual "host swagger prefix" (Some "docs") hostConfig.ApiDocs.SwaggerRoutePrefix
assertEqual "host fabric mode" "caller-owned-cluster" hostConfig.Ptcs.FabricMode
assertEqual "host reply participant" (Some "agent.codexfs.host") hostConfig.Ptcs.ReplyParticipantId
assertEqual "host inbox limit" 25 hostConfig.Ptcs.DefaultInboxLimit
assertEqual "host compact max" (Some 1200) hostConfig.Compaction.MaxSummaryChars
assertEqual "host compact recent" 3 hostConfig.Compaction.RecentEntryCount
assertEqual "host compact entry" (Some 240) hostConfig.Compaction.MaxEntryTextChars

let codexExecutableDiagnostic =
    hostLoad.Diagnostics |> List.find (fun diagnostic -> diagnostic.Key = "engine.codex.executable")

assertTrue "host diagnostic redacted" codexExecutableDiagnostic.WasRedacted
assertContains "host diagnostic replacement" "[REDACTED]" codexExecutableDiagnostic.Value
assertTrue "host diagnostic no raw token" (not (codexExecutableDiagnostic.Value.Contains(fakeGithubToken, StringComparison.Ordinal)))

let productionLoopbackLoad =
    CodexFs.HostConfig.loadFromMap
        (Map.ofList
            [ "control.allowLoopbackOnly", "false"
              "control.bindAddress", "127.0.0.1"
              "control.advertiseUri", "http://localhost:8788" ])

assertTrue "production loopback rejected" productionLoopbackLoad.Config.IsNone
assertTrue
    "production loopback issue"
    (productionLoopbackLoad.Issues
     |> List.exists (fun issue -> issue.Key = "control.advertiseuri" && issue.Severity = CodexFs.HostConfig.IssueError))

printfn "TC-HOST-001 config parse/redaction passed"

let ptcsFabric = MessageFabricBinding.createLocalFabric ()
let ptcsRunSuffix = Guid.NewGuid().ToString("N")
let ptcsAgentId = $"agent.ptcs002.{ptcsRunSuffix}"
let ptcsUserId = $"user.ptcs002.{ptcsRunSuffix}"
let ptcsGroupId = $"group.ptcs002.{ptcsRunSuffix}"
let agentBinding = MessageFabricBinding.defaultBinding ptcsAgentId
let userBinding = MessageFabricBinding.defaultBinding ptcsUserId
let groupBinding = { agentBinding with GroupId = Some ptcsGroupId; IncludeGroups = true }

let agentRegistration =
    runTask (MessageFabricBinding.registerParticipantAsync ptcsFabric agentBinding MessageFabricBinding.defaultRegistration)

let userRegistration =
    runTask
        (MessageFabricBinding.registerParticipantAsync
            ptcsFabric
            userBinding
            { MessageFabricBinding.defaultRegistration with
                DisplayName = Some "PTCS002 User"
                Kind = Some "user"
                Labels = Some [ "codex.fs"; "test" ] })

assertEqual "agent registered" ptcsAgentId agentRegistration.Participant.ParticipantId
assertEqual "user registered" ptcsUserId userRegistration.Participant.ParticipantId

let directEnvelope =
    runTask
        (MessageFabricBinding.sendAsync
            ptcsFabric
            { FromParticipantId = userBinding.ParticipantId
              Scope = PulseTrade.Comm.Spa.MessageFabricScope.Direct agentBinding.ParticipantId
              Body = "direct hello from ptcs002"
              Tags = [ "ptcs002"; "direct" ]
              CorrelationId = Some $"ptcs002-direct-1-{ptcsRunSuffix}"
              CreatedAtUtc = None })

assertEqual "direct body" "direct hello from ptcs002" directEnvelope.Body

let directBatch = runTask (MessageFabricBinding.pollInboxAsync ptcsFabric agentBinding None)
assertEqual "direct poll count" 1 directBatch.Messages.Length
assertEqual "direct poll id" directEnvelope.MessageId directBatch.Messages[0].MessageId

let directRefs = MessageFabricBinding.batchToMessageRefs directBatch
assertEqual "direct ref id" directEnvelope.MessageId directRefs[0].MessageId
assertEqual "direct ref cursor" (Some directEnvelope.MessageId) directRefs[0].Cursor
assertEqual "direct ref to" (Some agentBinding.ParticipantId) directRefs[0].ToParticipantId

let ackResult = runTask (MessageFabricBinding.ackInboxAsync ptcsFabric agentBinding directBatch.NextCursor)
assertEqual "ack status" "ok" ackResult.Status
assertEqual "ack cursor" (Some directEnvelope.MessageId) ackResult.Cursor

let afterAckBatch = runTask (MessageFabricBinding.pollInboxAsync ptcsFabric agentBinding None)
assertEqual "after ack empty" 0 afterAckBatch.Messages.Length

let waitEnvelope =
    runTask
        (MessageFabricBinding.sendDirectReplyAsync
            ptcsFabric
            userBinding
            agentBinding.ParticipantId
            "wait-drain hello from ptcs002"
            [ "ptcs002"; "wait" ]
            (Some $"ptcs002-direct-2-{ptcsRunSuffix}"))

let waitBatch =
    runTask
        (MessageFabricBinding.waitInboxAsync
            ptcsFabric
            agentBinding
            None
            (TimeSpan.FromSeconds 1.0)
            (TimeSpan.FromMilliseconds 10.0)
            (Some CancellationToken.None))

assertEqual "wait count" 1 waitBatch.Messages.Length
assertEqual "wait id" waitEnvelope.MessageId waitBatch.Messages[0].MessageId

let drainedBatch = runTask (MessageFabricBinding.drainInboxAsync ptcsFabric agentBinding None)
assertEqual "drain count" 1 drainedBatch.Messages.Length
assertEqual "drain id" waitEnvelope.MessageId drainedBatch.Messages[0].MessageId

let afterDrainBatch = runTask (MessageFabricBinding.pollInboxAsync ptcsFabric agentBinding None)
assertEqual "after drain empty" 0 afterDrainBatch.Messages.Length

let groupView =
    runTask (MessageFabricBinding.tryUpsertConfiguredGroupAsync ptcsFabric groupBinding)
    |> Option.defaultWith (fun () -> failwith "expected configured group")

assertEqual "group id" ptcsGroupId groupView.GroupId
assertTrue "group contains agent" (groupView.ParticipantIds |> List.contains agentBinding.ParticipantId)

let groupEnvelope =
    runTask
        (MessageFabricBinding.sendAsync
            ptcsFabric
            { FromParticipantId = userBinding.ParticipantId
              Scope = PulseTrade.Comm.Spa.MessageFabricScope.Group ptcsGroupId
              Body = "group hello from ptcs002"
              Tags = [ "ptcs002"; "group" ]
              CorrelationId = Some $"ptcs002-group-1-{ptcsRunSuffix}"
              CreatedAtUtc = None })

let groupBatch = runTask (MessageFabricBinding.pollInboxAsync ptcsFabric groupBinding None)
assertTrue "group poll contains message" (groupBatch.Messages |> List.exists (fun message -> message.MessageId = groupEnvelope.MessageId))

printfn "TC-PTCS-002 MessageFabric binding passed"
