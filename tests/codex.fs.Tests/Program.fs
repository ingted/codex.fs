module CodexFs.Tests.Program

open System
open System.Diagnostics
open System.Net
open System.Net.Http
open System.Net.NetworkInformation
open System.Net.Sockets
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open CodexFs.Compaction
open CodexFs.Domain
open CodexFs.ProcessRunner
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

let assertParseOk name argv =
    match CodexFs.Cli.Cli.tryParse argv with
    | Ok() -> ()
    | Error message -> failwith $"Assertion failed: {name}; parse failed: {message}"

let assertParseErrorContains name expected argv =
    match CodexFs.Cli.Cli.tryParse argv with
    | Ok() -> failwith $"Assertion failed: {name}; expected parse error"
    | Error message -> assertContains name expected message

let isUsableNonLoopbackIpv4 (address: IPAddress) =
    address.AddressFamily = AddressFamily.InterNetwork
    && not (IPAddress.IsLoopback address)
    && not (address.ToString().StartsWith("0.", StringComparison.Ordinal))
    && not (address.ToString().StartsWith("169.254.", StringComparison.Ordinal))

let findPreferredNonLoopbackIpv4 () =
    NetworkInterface.GetAllNetworkInterfaces()
    |> Array.filter (fun network -> network.OperationalStatus = OperationalStatus.Up)
    |> Array.collect (fun network -> network.GetIPProperties().UnicastAddresses |> Seq.toArray)
    |> Array.filter (fun addressInfo -> isUsableNonLoopbackIpv4 addressInfo.Address)
    |> Array.sortBy (fun addressInfo ->
        if addressInfo.DuplicateAddressDetectionState = DuplicateAddressDetectionState.Preferred then 0 else 1)
    |> Array.tryHead
    |> Option.map _.Address

let reserveTcpPort () =
    use listener = new TcpListener(IPAddress.Any, 0)
    listener.Start()
    let endpoint = listener.LocalEndpoint :?> IPEndPoint
    let port = endpoint.Port
    listener.Stop()
    port

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

let hostRuntimeLoad = CodexFs.HostConfig.loadFromMap (hostSettings |> Map.add "ptcs.fabricMode" "package-owned")

let hostRuntime =
    match CodexFs.Host.HostRuntime.tryCreateFromLoadResult hostRuntimeLoad with
    | Ok runtime -> runtime
    | Error issues -> failwith $"expected host runtime config; issues={issues}"

let hostRuntimeStartedUtc = DateTimeOffset.Parse("2026-07-04T13:08:00Z")

let runningHostRuntime =
    CodexFs.Host.HostRuntime.startInProcessMessageFabric hostRuntimeStartedUtc hostRuntime

let runtimeHealth = CodexFs.Host.HostRuntime.health runningHostRuntime

assertEqual "host runtime running" CodexFs.Host.HostRuntime.Running runtimeHealth.Status
assertEqual "host runtime default engine" "agy" runtimeHealth.DefaultEngine
assertTrue "host runtime codex enabled" (runtimeHealth.EnabledEngines |> List.contains "codex")
assertTrue "host runtime agy enabled" (runtimeHealth.EnabledEngines |> List.contains "agy")
assertEqual "host runtime advertise" "http://192.168.10.20:8788" runtimeHealth.ControlAdvertiseUri
assertEqual "host runtime fabric mode" "package-owned" runtimeHealth.PtcsFabricMode
assertEqual "host runtime participant prefix" "agent.codexfs" runtimeHealth.PtcsSessionParticipantPrefix
assertEqual "host runtime inbox limit" 25 runtimeHealth.PtcsDefaultInboxLimit
assertEqual "host runtime has fabric" true runtimeHealth.HasMessageFabric
assertContains "host runtime fabric type" "PulseTrade.Comm.Spa.CommSpaMessageFabric" (runtimeHealth.MessageFabricType |> Option.defaultValue "")
assertEqual "host runtime override keys" [ "codex" ] runtimeHealth.EngineOverrideKeys
assertTrue "host runtime redacted diagnostics" (runtimeHealth.RedactedDiagnostics |> List.exists _.WasRedacted)

let hostRuntimeSummary = CodexFs.Host.HostRuntime.healthSummary runningHostRuntime

assertContains "host runtime summary status" "status=running" hostRuntimeSummary
assertContains "host runtime summary fabric" "ptcsFabricMode=package-owned" hostRuntimeSummary
assertContains "host runtime summary type" "messageFabricType=PulseTrade.Comm.Spa.CommSpaMessageFabric" hostRuntimeSummary
assertContains "host runtime summary redacted" "[REDACTED]" hostRuntimeSummary
assertTrue "host runtime summary no raw token" (not (hostRuntimeSummary.Contains(fakeGithubToken, StringComparison.Ordinal)))

let callerOwnedFabric = MessageFabricBinding.createInProcessFabric ()
let callerOwnedRuntime =
    CodexFs.Host.HostRuntime.startWithMessageFabric
        (DateTimeOffset.Parse("2026-07-04T13:09:00Z"))
        callerOwnedFabric
        hostRuntime

assertEqual "host runtime caller-owned running" CodexFs.Host.HostRuntime.Running callerOwnedRuntime.Status
assertTrue "host runtime caller-owned fabric identity" (Object.ReferenceEquals(callerOwnedFabric, callerOwnedRuntime.MessageFabric.Value))

let stoppedHostRuntime = CodexFs.Host.HostRuntime.stop runningHostRuntime
assertEqual "host runtime stopped" CodexFs.Host.HostRuntime.Stopped stoppedHostRuntime.Status
assertEqual "host runtime stopped fabric cleared" true stoppedHostRuntime.MessageFabric.IsNone

printfn "TC-HOST-002 host runtime/health passed"

let hostControlAddress =
    findPreferredNonLoopbackIpv4 ()
    |> Option.defaultWith (fun () -> failwith "TC-HOST-003 requires a non-loopback IPv4 address for advertised URI verification.")

let hostControlPort = reserveTcpPort ()
let hostControlAdvertiseUri = $"http://{hostControlAddress}:{hostControlPort}"

let hostControlSettings =
    hostSettings
    |> Map.add "control.bindAddress" (hostControlAddress.ToString())
    |> Map.add "control.port" (string hostControlPort)
    |> Map.add "control.advertiseUri" hostControlAdvertiseUri
    |> Map.add "control.allowLoopbackOnly" "false"
    |> Map.add "ptcs.fabricMode" "package-owned"

let hostControlLoad = CodexFs.HostConfig.loadFromMap hostControlSettings
assertTrue "host control config loaded" hostControlLoad.Config.IsSome

let hostControlRuntime =
    match CodexFs.Host.HostRuntime.tryCreateFromLoadResult hostControlLoad with
    | Ok runtime -> runtime
    | Error issues -> failwith $"expected host control runtime config; issues={issues}"

let hostControlStartedUtc = DateTimeOffset.Parse("2026-07-04T13:18:00Z")

let hostControlServer =
    match runTask (CodexFs.Host.HostControl.tryStartAsync hostControlStartedUtc CancellationToken.None hostControlRuntime) with
    | Ok server -> server
    | Error issues -> failwith $"expected host control server; issues={issues}"

assertEqual "host control bind address" (hostControlAddress.ToString()) hostControlServer.Contract.BindAddress
assertEqual "host control advertise uri" hostControlAdvertiseUri hostControlServer.Contract.AdvertiseUri
assertContains "host control health route" CodexFs.Host.HostControl.Routes.Health hostControlServer.Contract.HealthUri
assertEqual "host control openapi uri" $"{hostControlAdvertiseUri}/openapi/v1.json" hostControlServer.Contract.OpenApiJsonUri
assertEqual "host control swagger ui uri" $"{hostControlAdvertiseUri}/docs/index.html" hostControlServer.Contract.SwaggerUiUri
assertEqual "host control generate openapi" true hostControlServer.Contract.GenerateOpenApi
assertEqual "host control expose swagger ui" true hostControlServer.Contract.ExposeSwaggerUi
assertTrue "host control not localhost" (not (hostControlServer.Contract.HealthUri.Contains("localhost", StringComparison.OrdinalIgnoreCase)))
assertTrue "host control not 127" (not (hostControlServer.Contract.HealthUri.Contains("127.", StringComparison.Ordinal)))
assertTrue
    "host control endpoint docs"
    (hostControlServer.Contract.Endpoints
     |> List.exists (fun endpoint ->
         endpoint.Name = CodexFs.Host.HostControl.EndpointNames.Health
         && endpoint.SuccessExample.Body.Contains("advertiseUri", StringComparison.Ordinal)
         && endpoint.FailureExample.Body.Contains("invalid-control-endpoint", StringComparison.Ordinal)))

let mutable stoppedControlRuntime = None

let hostControlRootPageText, hostControlResponseText, hostControlOpenApiText, hostControlSwaggerText, cliHostStatusText, cliSendResponseText, cliStatusText, cliAttachText, cliDrainText, cliAfterDrainStatusText =
    try
        use handler = new HttpClientHandler(UseProxy = false)
        use client = new HttpClient(handler, true)
        client.Timeout <- TimeSpan.FromSeconds 10.0

        let rootResponse = runTask (client.GetAsync(hostControlServer.Contract.AdvertiseUri + "/"))
        let rootBody = runTask (rootResponse.Content.ReadAsStringAsync())
        let response = runTask (client.GetAsync(hostControlServer.Contract.HealthUri))
        let body = runTask (response.Content.ReadAsStringAsync())
        let openApiResponse = runTask (client.GetAsync(hostControlServer.Contract.OpenApiJsonUri))
        let openApiBody = runTask (openApiResponse.Content.ReadAsStringAsync())
        let swaggerResponse = runTask (client.GetAsync(hostControlServer.Contract.SwaggerUiUri))
        let swaggerBody = runTask (swaggerResponse.Content.ReadAsStringAsync())
        let cliHostStatusResult =
            runTask
                (CodexFs.Cli.CliHttp.getHostStatusAsync
                    client
                    CancellationToken.None
                    { Host = hostControlServer.Contract.AdvertiseUri })

        let cli002RunSuffix = Guid.NewGuid().ToString("N")
        let cli002SessionId = $"cli002.{cli002RunSuffix}"
        let cli002Prompt = "CLI-002 prompt through host and PTCS MessageFabric"

        let cliSendResult =
            runTask
                (CodexFs.Cli.CliHttp.sendSessionMessageAsync
                    client
                    CancellationToken.None
                    { Host = hostControlServer.Contract.AdvertiseUri
                      SessionId = cli002SessionId
                      Prompt = cli002Prompt })

        let cli002Target: CodexFs.Cli.Cli.SessionTargetOptions =
            { Host = hostControlServer.Contract.AdvertiseUri
              SessionId = cli002SessionId }

        let cliStatusResult = runTask (CodexFs.Cli.CliHttp.getSessionStatusAsync client CancellationToken.None cli002Target)
        let cliAttachResult = runTask (CodexFs.Cli.CliHttp.attachSessionAsync client CancellationToken.None cli002Target)
        let cliDrainResult = runTask (CodexFs.Cli.CliHttp.drainSessionAsync client CancellationToken.None cli002Target)
        let cliAfterDrainStatusResult = runTask (CodexFs.Cli.CliHttp.getSessionStatusAsync client CancellationToken.None cli002Target)

        assertEqual "host root http status" HttpStatusCode.OK rootResponse.StatusCode
        assertEqual "host control http status" HttpStatusCode.OK response.StatusCode
        assertEqual "host openapi http status" HttpStatusCode.OK openApiResponse.StatusCode
        assertEqual "host swagger ui http status" HttpStatusCode.OK swaggerResponse.StatusCode
        assertEqual "cli host status status" 200 cliHostStatusResult.StatusCode
        assertTrue "cli host status success" cliHostStatusResult.IsSuccess
        assertEqual "cli send status" 202 cliSendResult.StatusCode
        assertTrue "cli send success" cliSendResult.IsSuccess
        assertEqual "cli status status" 200 cliStatusResult.StatusCode
        assertTrue "cli status success" cliStatusResult.IsSuccess
        assertEqual "cli attach status" 200 cliAttachResult.StatusCode
        assertTrue "cli attach success" cliAttachResult.IsSuccess
        assertEqual "cli drain status" 200 cliDrainResult.StatusCode
        assertTrue "cli drain success" cliDrainResult.IsSuccess
        assertEqual "cli after drain status" 200 cliAfterDrainStatusResult.StatusCode
        assertTrue "cli after drain success" cliAfterDrainStatusResult.IsSuccess

        rootBody, body, openApiBody, swaggerBody, cliHostStatusResult.Body, cliSendResult.Body, cliStatusResult.Body, cliAttachResult.Body, cliDrainResult.Body, cliAfterDrainStatusResult.Body
    finally
        stoppedControlRuntime <- Some(runTask (CodexFs.Host.HostControl.stopAsync CancellationToken.None hostControlServer))

assertTrue "host control stopped" stoppedControlRuntime.IsSome
assertEqual "host control stopped status" CodexFs.Host.HostRuntime.Stopped stoppedControlRuntime.Value.Status

let hostControlJson = JsonDocument.Parse(hostControlResponseText)
let hostControlRoot = hostControlJson.RootElement

assertEqual "host control response status" "running" (hostControlRoot.GetProperty("status").GetString())
assertEqual "host control response advertise uri" hostControlAdvertiseUri (hostControlRoot.GetProperty("advertiseUri").GetString())
assertEqual "host control response health uri" hostControlServer.Contract.HealthUri (hostControlRoot.GetProperty("healthUri").GetString())
assertEqual "host control response bind address" (hostControlAddress.ToString()) (hostControlRoot.GetProperty("bindAddress").GetString())
assertEqual "host control response port" hostControlPort (hostControlRoot.GetProperty("port").GetInt32())
assertEqual "host control response loopback false" false (hostControlRoot.GetProperty("allowLoopbackOnly").GetBoolean())
assertEqual "host control response fabric" true (hostControlRoot.GetProperty("hasMessageFabric").GetBoolean())
assertTrue "host control response no raw token" (not (hostControlResponseText.Contains(fakeGithubToken, StringComparison.Ordinal)))
hostControlJson.Dispose()

printfn "TC-HOST-003 endpoint contract passed"

assertContains "host root title" "codex.fs host" hostControlRootPageText
assertContains "host root health link" hostControlServer.Contract.HealthUri hostControlRootPageText
assertContains "host root openapi link" hostControlServer.Contract.OpenApiJsonUri hostControlRootPageText
assertContains "host root swagger link" hostControlServer.Contract.SwaggerUiUri hostControlRootPageText
assertTrue "host root no raw token" (not (hostControlRootPageText.Contains(fakeGithubToken, StringComparison.Ordinal)))

let cliHostStatusJson = JsonDocument.Parse(cliHostStatusText)
let cliHostStatusRoot = cliHostStatusJson.RootElement

assertEqual "cli host status response status" "running" (cliHostStatusRoot.GetProperty("status").GetString())
assertEqual "cli host status response advertise" hostControlAdvertiseUri (cliHostStatusRoot.GetProperty("advertiseUri").GetString())
assertEqual "cli host status response health" hostControlServer.Contract.HealthUri (cliHostStatusRoot.GetProperty("healthUri").GetString())
assertTrue "cli host status response fabric" (cliHostStatusRoot.GetProperty("hasMessageFabric").GetBoolean())
assertTrue "cli host status no raw token" (not (cliHostStatusText.Contains(fakeGithubToken, StringComparison.Ordinal)))
cliHostStatusJson.Dispose()

let hostOpenApiJson = JsonDocument.Parse(hostControlOpenApiText)
let hostOpenApiRoot = hostOpenApiJson.RootElement
let mutable hostHealthPath = Unchecked.defaultof<JsonElement>

assertTrue "host openapi version" (hostOpenApiRoot.GetProperty("openapi").GetString().StartsWith("3.", StringComparison.Ordinal))
assertTrue "host openapi has health path" (hostOpenApiRoot.GetProperty("paths").TryGetProperty(CodexFs.Host.HostControl.Routes.Health, &hostHealthPath))
assertTrue "host swagger ui html" (hostControlSwaggerText.Contains("SwaggerUIBundle", StringComparison.Ordinal) || hostControlSwaggerText.Contains("swagger-ui", StringComparison.OrdinalIgnoreCase))
assertTrue "host swagger ui no raw token" (not (hostControlSwaggerText.Contains(fakeGithubToken, StringComparison.Ordinal)))
hostOpenApiJson.Dispose()

printfn "TC-DOC-003 OpenAPI available passed"

let assertInboxJson (name: string) (expectedStatus: string) (minCount: int) (body: string) =
    use document = JsonDocument.Parse(body)
    let root = document.RootElement
    assertEqual $"{name} status" expectedStatus (root.GetProperty("status").GetString())
    assertTrue $"{name} pending count" (root.GetProperty("pendingCount").GetInt32() >= minCount)
    assertContains $"{name} transcript" "CLI-002 prompt through host and PTCS MessageFabric" (root.GetProperty("transcript").GetString())

let cliSendJson = JsonDocument.Parse(cliSendResponseText)
let cliSendRoot = cliSendJson.RootElement

assertEqual "cli send response status" "accepted" (cliSendRoot.GetProperty("status").GetString())
assertEqual "cli send response sender" "user.codexfs.cli" (cliSendRoot.GetProperty("fromParticipantId").GetString())
assertTrue "cli send response message id" (not (String.IsNullOrWhiteSpace(cliSendRoot.GetProperty("messageId").GetString())))
cliSendJson.Dispose()

assertInboxJson "cli status" "ok" 1 cliStatusText

printfn "TC-CLI-002 CLI send through MessageFabric passed"

assertInboxJson "cli attach" "ok" 1 cliAttachText
assertInboxJson "cli drain" "drained" 1 cliDrainText

let cliAfterDrainJson = JsonDocument.Parse(cliAfterDrainStatusText)
let cliAfterDrainRoot = cliAfterDrainJson.RootElement

assertEqual "cli after drain response status" "ok" (cliAfterDrainRoot.GetProperty("status").GetString())
assertEqual "cli after drain pending" 0 (cliAfterDrainRoot.GetProperty("pendingCount").GetInt32())
cliAfterDrainJson.Dispose()

printfn "TC-CLI-003 attach/drain/status passed"

let cliHelp = CodexFs.Cli.Cli.helpText ()

assertTrue "cli program empty help" (CodexFs.Cli.Program.isRootHelp [||])
assertTrue "cli program long help" (CodexFs.Cli.Program.isRootHelp [| "--help" |])
assertTrue "cli program short help" (CodexFs.Cli.Program.isRootHelp [| "-h" |])
assertTrue "cli program word help" (CodexFs.Cli.Program.isRootHelp [| "help" |])
assertTrue "cli program command not root help" (not (CodexFs.Cli.Program.isRootHelp [| "session"; "send" |]))
assertContains "cli help session" "session <options>" cliHelp
assertContains "cli help run" "run <options>" cliHelp
assertContains "cli help host" "host <options>" cliHelp
assertContains "cli help engine" "engine <options>" cliHelp
assertContains "cli examples header" "Examples:" cliHelp
assertContains "cli program name" "USAGE: codex.fs" cliHelp
assertContains "cli host example" "codex.fs host status --host http://192.168.10.20:8788" cliHelp
assertContains "cli send example" "codex.fs session send --session sess-001 --prompt @prompt.md" cliHelp

assertParseOk "cli host status" [| "host"; "status"; "--host"; "http://192.168.10.20:8788" |]
assertParseOk "cli session create" [| "session"; "create"; "--engine"; "agy"; "--host"; "http://192.168.10.20:8788" |]
assertParseOk "cli session send" [| "session"; "send"; "--session"; "sess-001"; "--prompt"; "@prompt.md"; "--host"; "http://192.168.10.20:8788" |]
assertParseOk "cli session attach" [| "session"; "attach"; "--session"; "sess-001"; "--host"; "http://192.168.10.20:8788" |]
assertParseOk "cli session drain" [| "session"; "drain"; "--session"; "sess-001"; "--host"; "http://192.168.10.20:8788" |]
assertParseOk "cli run status" [| "run"; "status"; "--run"; "run-001"; "--host"; "http://192.168.10.20:8788" |]
assertParseOk "cli run artifacts" [| "run"; "artifacts"; "--run"; "run-001"; "--host"; "http://192.168.10.20:8788" |]
assertParseOk "cli engine probe" [| "engine"; "probe"; "--engine"; "agy"; "--executable"; "agy" |]
assertParseErrorContains "cli invalid arg" "unrecognized argument" [| "session"; "send"; "--bogus" |]

match CodexFs.Cli.Cli.tryParseHostStatus [| "host"; "status"; "--host"; "http://192.168.10.20:8788" |] with
| Ok(Some options) -> assertEqual "cli host status parse host" "http://192.168.10.20:8788" options.Host
| Ok None -> failwith "Assertion failed: cli host status parse returned None"
| Error message -> failwith $"Assertion failed: cli host status parse failed: {message}"

let resolvedDirectPrompt =
    CodexFs.Cli.Cli.tryResolvePromptText (fun path -> failwith $"unexpected prompt file read: {path}") "direct prompt text"

match resolvedDirectPrompt with
| Ok text -> assertEqual "cli direct prompt resolve" "direct prompt text" text
| Error message -> failwith $"Assertion failed: cli direct prompt resolve failed: {message}"

let resolvedFilePrompt =
    CodexFs.Cli.Cli.tryResolvePromptText
        (fun path ->
            assertEqual "cli prompt file path" "prompt.md" path
            "CLI-004 prompt from @file")
        "@prompt.md"

match resolvedFilePrompt with
| Ok text -> assertEqual "cli file prompt resolve" "CLI-004 prompt from @file" text
| Error message -> failwith $"Assertion failed: cli file prompt resolve failed: {message}"

match CodexFs.Cli.Cli.tryResolvePromptText (fun _ -> "") "@" with
| Ok text -> failwith $"Assertion failed: cli blank prompt file path should fail; actual={text}"
| Error message -> assertContains "cli blank prompt file path error" "Prompt file path after @" message

printfn "TC-CLI-001 Argu parser/help passed"
printfn "TC-CLI-004 host status and @file prompt resolver passed"

let hostToolHelp = CodexFs.HostTool.HostTool.helpText ()

assertTrue "host tool empty help" (CodexFs.HostTool.HostTool.isRootHelp [||])
assertTrue "host tool long help" (CodexFs.HostTool.HostTool.isRootHelp [| "--help" |])
assertTrue "host tool short help" (CodexFs.HostTool.HostTool.isRootHelp [| "-h" |])
assertTrue "host tool word help" (CodexFs.HostTool.HostTool.isRootHelp [| "help" |])
assertTrue "host tool command not root help" (not (CodexFs.HostTool.HostTool.isRootHelp [| "start" |]))
assertContains "host tool help status" "status <options>" hostToolHelp
assertContains "host tool help start" "start <options>" hostToolHelp
assertContains "host tool examples" "codex.fs.host start --run-seconds 5" hostToolHelp

let hostToolPort = reserveTcpPort ()
let hostToolAdvertiseUri = $"http://{hostControlAddress}:{hostToolPort}"

let hostToolCommonArgs =
    [| "--setting"
       $"control.bindAddress={hostControlAddress}"
       "--setting"
       $"control.port={hostToolPort}"
       "--setting"
       $"control.advertiseUri={hostToolAdvertiseUri}"
       "--setting"
       "control.allowLoopbackOnly=false"
       "--setting"
       "ptcs.fabricMode=package-owned" |]

let hostToolStatusOptions =
    match CodexFs.HostTool.HostTool.tryParseAction (Array.append [| "status" |] hostToolCommonArgs) with
    | Ok(Some(CodexFs.HostTool.HostTool.HostStatus options)) -> options
    | Ok action -> failwith $"expected host tool status action; actual={action}"
    | Error message -> failwith $"expected host tool status parse; error={message}"

let hostToolStatusText =
    match CodexFs.HostTool.HostTool.statusText hostToolStatusOptions with
    | Ok text -> text
    | Error message -> failwith $"expected host tool status text; error={message}"

assertContains "host tool status created" "status=created" hostToolStatusText
assertContains "host tool status advertise" $"controlAdvertiseUri={hostToolAdvertiseUri}" hostToolStatusText
assertTrue "host tool status not localhost" (not (hostToolStatusText.Contains("localhost", StringComparison.OrdinalIgnoreCase)))

let hostToolStartArgs =
    [| yield "start"
       yield "--run-seconds"
       yield "0"
       yield! hostToolCommonArgs |]

let hostToolStartOptions =
    match CodexFs.HostTool.HostTool.tryParseAction hostToolStartArgs with
    | Ok(Some(CodexFs.HostTool.HostTool.HostStart options)) -> options
    | Ok action -> failwith $"expected host tool start action; actual={action}"
    | Error message -> failwith $"expected host tool start parse; error={message}"

let hostToolStartText =
    match runTask (CodexFs.HostTool.HostTool.startTextAsync (DateTimeOffset.Parse("2026-07-04T14:55:00Z")) hostToolStartOptions CancellationToken.None) with
    | Ok text -> text
    | Error message -> failwith $"expected host tool bounded start; error={message}"

assertContains "host tool start running" "status=running" hostToolStartText
assertContains "host tool start bind" $"bindUri=http://{hostControlAddress}:{hostToolPort}" hostToolStartText
assertContains "host tool start health" $"{hostToolAdvertiseUri}/api/codexfs/host/health" hostToolStartText
assertContains "host tool start stopped" "status=stopped" hostToolStartText
assertTrue "host tool start no localhost" (not (hostToolStartText.Contains("localhost", StringComparison.OrdinalIgnoreCase)))
assertTrue "host tool start no 127" (not (hostToolStartText.Contains("127.", StringComparison.Ordinal)))

printfn "TC-REL-003 host tool start/status passed"

let ptcsFabric = MessageFabricBinding.createInProcessFabric ()
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

let e2e003RunSuffix = Guid.NewGuid().ToString("N")
let e2e003GroupId = $"group.e2e003.{e2e003RunSuffix}"
let e2e003Alpha = MessageFabricBinding.defaultBinding $"agent.e2e003.alpha.{e2e003RunSuffix}"
let e2e003Beta = { MessageFabricBinding.defaultBinding $"agent.e2e003.beta.{e2e003RunSuffix}" with GroupId = Some e2e003GroupId; IncludeGroups = true }

let e2e003Registration participantId =
    { MessageFabricBinding.defaultRegistration with
        DisplayName = Some participantId
        Kind = Some "agent"
        Labels = Some [ "codex.fs"; "e2e003"; "session-worker" ] }

runTask (MessageFabricBinding.registerParticipantAsync ptcsFabric e2e003Alpha (e2e003Registration e2e003Alpha.ParticipantId))
|> ignore

runTask (MessageFabricBinding.registerParticipantAsync ptcsFabric e2e003Beta (e2e003Registration e2e003Beta.ParticipantId))
|> ignore

let e2e003GroupView =
    runTask
        (MessageFabricBinding.upsertGroupAsync
            ptcsFabric
            e2e003GroupId
            [ e2e003Alpha.ParticipantId; e2e003Beta.ParticipantId ]
            [ "codex.fs"; "e2e003"; "multi-agent" ])

assertTrue "e2e003 group contains alpha" (e2e003GroupView.ParticipantIds |> List.contains e2e003Alpha.ParticipantId)
assertTrue "e2e003 group contains beta" (e2e003GroupView.ParticipantIds |> List.contains e2e003Beta.ParticipantId)

let e2e003TaskBody = "E2E-003 alpha asks beta to inspect artifact manifest reference"

let e2e003GroupMessage =
    runTask
        (MessageFabricBinding.sendAsync
            ptcsFabric
            { FromParticipantId = e2e003Alpha.ParticipantId
              Scope = PulseTrade.Comm.Spa.MessageFabricScope.Group e2e003GroupId
              Body = e2e003TaskBody
              Tags = [ "codex.fs"; "e2e003"; "task" ]
              CorrelationId = Some $"e2e003-task-{e2e003RunSuffix}"
              CreatedAtUtc = None })

let e2e003BetaBatch = runTask (MessageFabricBinding.pollInboxAsync ptcsFabric e2e003Beta None)
assertTrue "e2e003 beta received group task" (e2e003BetaBatch.Messages |> List.exists (fun message -> message.MessageId = e2e003GroupMessage.MessageId))

let e2e003BetaRefs = MessageFabricBinding.batchToMessageRefs e2e003BetaBatch
assertTrue "e2e003 beta ref has group" (e2e003BetaRefs |> List.exists (fun message -> message.GroupId = Some e2e003GroupId))

let e2e003ReplyBody = "E2E-003 beta reviewed manifest reference and replies to alpha"

let e2e003Reply =
    runTask
        (MessageFabricBinding.sendDirectReplyAsync
            ptcsFabric
            e2e003Beta
            e2e003Alpha.ParticipantId
            e2e003ReplyBody
            [ "codex.fs"; "e2e003"; "reply" ]
            (Some $"e2e003-reply-{e2e003RunSuffix}"))

let e2e003AlphaBatch = runTask (MessageFabricBinding.pollInboxAsync ptcsFabric e2e003Alpha None)
assertTrue "e2e003 alpha received beta reply" (e2e003AlphaBatch.Messages |> List.exists (fun message -> message.MessageId = e2e003Reply.MessageId))
assertTrue "e2e003 alpha reply body" (e2e003AlphaBatch.Messages |> List.exists (fun message -> message.Body = e2e003ReplyBody))

let e2e003Ack = runTask (MessageFabricBinding.ackInboxAsync ptcsFabric e2e003Alpha e2e003AlphaBatch.NextCursor)
assertEqual "e2e003 alpha ack" "ok" e2e003Ack.Status

printfn "TC-E2E-003 multi-agent MessageFabric group passed"

let durableRunSuffix = Guid.NewGuid().ToString("N")
let durable = DurableMessageFabricBinding.createVolatileDurableFabric ()
let durableCaller = MessageFabricBinding.defaultBinding $"user.ptcs003.{durableRunSuffix}"
let durableWorker = MessageFabricBinding.defaultBinding $"agent.ptcs003.{durableRunSuffix}"

let durableProof = runTask (DurableMessageFabricBinding.volatileProviderProofAsync durable CancellationToken.None)
assertEqual "durable proof mode" PulseTrade.Comm.Spa.DurableIngressMode.Volatile durableProof.Mode
assertEqual "durable proof crash durable" false durableProof.IsCrashDurable
assertEqual "durable proof retry" false durableProof.SupportsDeliveryRetry
assertEqual "durable proof sharded satisfied" false durableProof.SatisfiesShardedDeliveryProvider
assertTrue "durable proof provider missing" (durableProof.MissingRequirements |> List.contains "provider-specific-sharding-delivery-runtime")

let durableCallerRegistration =
    runTask
        (DurableMessageFabricBinding.registerParticipantAsync
            durable
            durableCaller
            { MessageFabricBinding.defaultRegistration with
                Kind = Some "user"
                Labels = Some [ "codex.fs"; "ptcs003"; "caller" ] })

let durableWorkerRegistration =
    runTask
        (DurableMessageFabricBinding.registerParticipantAsync
            durable
            durableWorker
            { MessageFabricBinding.defaultRegistration with
                Labels = Some [ "codex.fs"; "ptcs003"; "session-worker" ] })

assertEqual "durable caller registered" durableCaller.ParticipantId durableCallerRegistration.Value.Participant.ParticipantId
assertEqual "durable worker registered" durableWorker.ParticipantId durableWorkerRegistration.Value.Participant.ParticipantId

let durableTaskBody = "PTCS-003 durable task asks worker to append this prompt after current run"
let durableTaskId = $"task.ptcs003.{durableRunSuffix}"

let durableAccepted =
    runTask
        (DurableMessageFabricBinding.submitAgentTaskAsync
            durable
            { AgentTaskId = durableTaskId
              ParentRequestId = None
              FromParticipantId = durableCaller.ParticipantId
              ToParticipantId = durableWorker.ParticipantId
              Intent = "session-worker.prompt-handoff"
              Body = durableTaskBody
              ContentType = Some "text/markdown"
              Tags = [ "codex.fs"; "ptcs003"; "durable-handoff" ]
              ReplyToParticipantId = Some durableCaller.ParticipantId
              CorrelationId = Some $"ptcs003-correlation-{durableRunSuffix}"
              OperationId = Some $"ptcs003-operation-{durableRunSuffix}"
              IdempotencyKey = Some $"ptcs003-idempotency-{durableRunSuffix}"
              EntityId = Some durableWorker.ParticipantId
              VaultProfileId = None
              ResultMaxBytes = Some 65536L
              CreatedAtUtc = None
              DeadlineAtUtc = Some(DateTimeOffset.UtcNow.AddMinutes 5.0) })

assertEqual "durable accepted request id" $"message-fabric:agent-task:{durableTaskId}" durableAccepted.Accepted.RequestId
assertEqual "durable accepted ticket" (Some durableAccepted.Accepted.RequestId) durableAccepted.Accepted.TicketId
assertContains "durable result handle" durableTaskId durableAccepted.ResultQueryHandle
assertContains "durable envelope schema" "ptc.comm.spa.message-fabric.agent-task.v1" durableAccepted.Message.Body
assertContains "durable envelope body" durableTaskBody durableAccepted.Message.Body

let durableTicketStatus =
    match durableAccepted.Accepted.TicketId with
    | Some ticketId -> runTask (DurableMessageFabricBinding.queryTicketAsync durable ticketId CancellationToken.None)
    | None -> failwith "expected PTCS durable agent task ticket"

assertEqual "durable ticket status request" durableAccepted.Accepted.RequestId durableTicketStatus.RequestId
assertEqual "durable ticket status kind" PulseTrade.Comm.Spa.DurableTaskStatusKind.Accepted durableTicketStatus.Status

let durableInbox = runTask (DurableMessageFabricBinding.pollInboxAsync durable durableWorker None)
assertTrue "durable inbox contains task" (durableInbox.Messages |> List.exists (fun message -> message.MessageId = durableAccepted.Message.MessageId))
assertTrue "durable inbox body contains task" (durableInbox.Messages |> List.exists (fun message -> message.Body.Contains(durableTaskBody, StringComparison.Ordinal)))

let durableRefs = MessageFabricBinding.batchToMessageRefs durableInbox
assertTrue "durable ref direct target" (durableRefs |> List.exists (fun message -> message.ToParticipantId = Some durableWorker.ParticipantId))

let durableAck = runTask (DurableMessageFabricBinding.ackInboxAsync durable durableWorker durableInbox.NextCursor)
assertEqual "durable ack status" "ok" durableAck.Value.Status
assertEqual "durable ack ticket completed" PulseTrade.Comm.Spa.DurableTaskStatusKind.Completed durableAck.DeliveryStatus.Status

printfn "TC-PTCS-003 durable handoff passed"

let startControlledSleepProcess () =
    let psi = ProcessStartInfo()
    psi.FileName <- "powershell.exe"
    psi.UseShellExecute <- false
    psi.CreateNoWindow <- true
    psi.ArgumentList.Add "-NoProfile"
    psi.ArgumentList.Add "-Command"
    psi.ArgumentList.Add "Start-Sleep -Seconds 60"

    let proc = Process.Start psi

    if isNull proc then
        failwith "Expected controlled sleep process to start."

    proc

let mutable controlledProcess: Process option = None

try
    let proc = startControlledSleepProcess ()
    controlledProcess <- Some proc
    Thread.Sleep 250
    proc.Refresh()

    let lease =
        { ProcessId = proc.Id
          ProcessName = proc.ProcessName
          StartedUtc = DateTimeOffset(proc.StartTime.ToUniversalTime(), TimeSpan.Zero)
          Marker = "ops001-controlled-fixture" }

    let recovery = runTask (recoverLeasedProcessAsync defaultOrphanRecoveryOptions lease)
    assertEqual "orphan recovery outcome" Terminated recovery.Outcome
    assertTrue "orphan recovery running" recovery.WasRunning
    assertTrue "orphan recovery matched" recovery.WasMatched
    assertTrue "orphan recovery terminated" recovery.WasTerminated
    proc.Refresh()
    assertTrue "controlled process exited" proc.HasExited
finally
    match controlledProcess with
    | Some proc ->
        try
            if not proc.HasExited then
                proc.Kill(entireProcessTree = true)
        with
        | _ -> ()

        proc.Dispose()
    | None -> ()

printfn "TC-OPS-001 orphan process recovery passed"
