namespace CodexFs.Host

open System
open System.Collections.Generic
open System.IO
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open Akka.Actor
open CodexFs
open CodexFs.Ptcs
open CodexFs.Web
open CodexFs.Web.Server
open PulseTrade.Comm.Spa

/// Product PTCS WebSharper shell composition for codex.fs.host.
module HostWebShell =

    /// Non-secret web shell contract returned after PTCS `/chat` starts.
    type HostWebShellContract =
        { /// Active web shell profile.
          Profile: string
          /// Local URI used by the Suave listener.
          BindUri: string
          /// URI advertised to browser users and other nodes.
          AdvertiseUri: string
          /// Advertised PTCS classic chat URI.
          ChatUri: string
          /// Advertised PTCS health URI.
          HealthUri: string
          /// AI chat extension id registered into PTCS.
          ExtensionId: string
          /// Same-origin script URLs registered by `useAIChat()`.
          ScriptUrls: string list
          /// PTCS actor fabric mode used for this shell.
          ActorFabric: string
          /// Optional PCSL root used by the shell hub.
          PcslRoot: string option
          /// True when the runtime has a MessageFabric connected to the shell hub.
          HasMessageFabric: bool
          /// Concrete MessageFabric type name.
          MessageFabricType: string option }

    /// Running PTCS web shell and the runtime connected to its hub.
    type HostWebShellServer =
        { /// Runtime started with a MessageFabric created from the same PTCS hub.
          Runtime: HostRuntime.HostRuntime
          /// Advertised non-secret shell contract.
          Contract: HostWebShellContract
          /// Running PTCS Suave server.
          Server: RunningServer
          /// PTCS ActorFabric-backed Foreman actor, when actor fabric is enabled.
          ForemanActor: IActorRef option
          /// Background supervisor that asks the Foreman actor to run bounded runtime cycles.
          ForemanLoop: Task option
          /// Cancellation source for the Foreman runtime loop.
          ForemanLoopCancellation: CancellationTokenSource option
          /// Background bridge that turns PTCS AI append-page intents into MessageFabric messages.
          AiIntentBridgeLoop: Task option
          /// Cancellation source for the AI intent bridge loop.
          AiIntentBridgeCancellation: CancellationTokenSource option
          /// UTC timestamp used when the shell was started.
          StartedUtc: DateTimeOffset }

    /// Return true when host config requests the product PTCS web shell.
    let isEnabled (config: HostConfig.HostConfig) =
        config.WebShell.Profile.Trim().Equals("ptcs-webshell", StringComparison.OrdinalIgnoreCase)

    let formatHostForUrl (host: string) =
        let value = if String.IsNullOrWhiteSpace host then "127.0.0.1" else host.Trim()

        if value.Contains(":", StringComparison.Ordinal) && not (value.StartsWith("[", StringComparison.Ordinal)) then
            "[" + value + "]"
        else
            value

    let combineAdvertisedRoute (advertiseUri: string) (route: string) =
        advertiseUri.TrimEnd('/') + "/" + route.TrimStart('/')

    let resolvePort (webShell: HostConfig.HostWebShellConfig) =
        match webShell.Port with
        | Some port -> port
        | None ->
            let uri = Uri(webShell.AdvertiseUri)
            uri.Port

    let buildContract (runtime: HostRuntime.HostRuntime) : HostWebShellContract =
        let webShell = runtime.Config.WebShell
        let port = resolvePort webShell
        let bindAddress = webShell.BindAddress.Trim()
        let bindUri = $"http://{formatHostForUrl bindAddress}:{port}"
        let advertiseUri = webShell.AdvertiseUri.TrimEnd('/')
        let health = HostRuntime.health runtime

        { Profile = webShell.Profile
          BindUri = bindUri
          AdvertiseUri = advertiseUri
          ChatUri = combineAdvertisedRoute advertiseUri "/chat"
          HealthUri = combineAdvertisedRoute advertiseUri "/healthz"
          ExtensionId = Package.extensionId
          ScriptUrls = []
          ActorFabric = webShell.ActorFabric
          PcslRoot = webShell.PcslRoot
          HasMessageFabric = health.HasMessageFabric
          MessageFabricType = health.MessageFabricType }

    let webShellDisabledIssue: HostConfig.HostConfigIssue =
        { Key = "web.profile"
          Severity = HostConfig.IssueError
          Message = "Host web profile is not ptcs-webshell." }

    let serverOptions (config: HostConfig.HostConfig) (hub: CommHub) =
        let webShell = config.WebShell
        let port = resolvePort webShell

        let options =
            { ServerOptions.minimalWithHub hub with
                Host = webShell.BindAddress.Trim()
                Port = port }

        match webShell.ActorFabric.Trim().ToLowerInvariant() with
        | "disabled" -> Server.withoutActorFabric options
        | _ -> options

    let registerDefaultForeman (hub: CommHub) =
        hub.RegisterParticipant
            { ParticipantId = "agent.codexfs.foreman"
              DisplayName = Some "codex.fs Foreman"
              Kind = Some "agent"
              Labels = Some [ "codex.fs"; "foreman"; "session-worker" ] }
        |> ignore

    [<Literal>]
    let defaultAiChatPageId = "codexfs-ai-chat"

    [<Literal>]
    let aiIntentBridgeParticipantId = "user.codexfs.web.ai-intent"

    let registerDefaultAiChatPage (hub: CommHub) =
        let page =
            { Domain.appendPage defaultAiChatPageId "codex.fs AI Chat" defaultAiChatPageId "codexfs-ai-chat" with
                Path = "/page/" + defaultAiChatPageId
                Description = "codex.fs Foreman AI chat intent surface."
                KeyPlaceholder = "\"agent.codexfs.foreman\""
                ValuePlaceholder = "Prompt; controls emit codex.fs.web.ai-intent.v1 JSON"
                DefaultKey = "\"agent.codexfs.foreman\""
                Tags = [ "codex.fs"; "ai-chat"; "agent" ] }

        let registered = hub.RegisterAppendPage page
        hub.RegisterAppendPageKeyWithDisplayName(registered.PageId, [ "agent.codexfs.foreman" ], "Foreman") |> ignore
        registered

    let registeredHub (config: HostConfig.HostConfig) =
        let hub =
            match config.WebShell.PcslRoot with
            | Some root ->
                let fullRoot = Path.GetFullPath root
                Directory.CreateDirectory fullRoot |> ignore
                CommHub.createEmptyWithPcslRoot fullRoot
            | None -> CommHub.createEmpty()

        hub.useAIChat() |> ignore
        registerDefaultForeman hub
        registerDefaultAiChatPage hub |> ignore
        hub

    let engineExecutable (config: HostConfig.HostConfig) engine =
        config.EngineExecutableOverrides
        |> Map.tryFind engine
        |> Option.filter (String.IsNullOrWhiteSpace >> not)
        |> Option.defaultValue (RuntimeMessageFabricCycle.defaultExecutable engine)

    let foremanRuntimeCommand (runtime: HostRuntime.HostRuntime) (spec: ActorFabricBinding.WorkerParticipantSpec) : ActorFabricBinding.RunRuntimeCycle =
        let config = runtime.Config
        let engine = config.DefaultEngine

        { SessionId = "foreman"
          SessionParticipantId = Some spec.ParticipantId
          ReplyParticipantId = config.Ptcs.ReplyParticipantId
          Engine = Some engine
          ExecutablePath = Some(engineExecutable config engine)
          EngineExecutableOverrides = Some config.EngineExecutableOverrides
          WorkingDirectory = Some(Directory.GetCurrentDirectory())
          ArtifactRoot = config.ArtifactRoot
          Timeout = Some config.DefaultTimeout
          SystemInstruction =
            Some
                "You are codex.fs Foreman. Reply concisely to the latest PTCS chat prompt. If the user asks you to use PowerShell or another local tool, use the available command tool and report the observed result."
          AdditionalDirectories = []
          AgyDangerouslySkipPermissions = Some true
          CodexModel = None
          CodexDangerouslyBypassApprovalsAndSandbox = Some true }

    type AiIntentTarget =
        { Mode: string
          Scope: string
          ParticipantId: string
          GroupId: string }

    type AiIntentEngine =
        { Engine: string
          Model: string
          Reasoning: string }

    type AiIntentInvocation =
        { Mode: string
          Approval: string }

    type AiIntent =
        { Target: AiIntentTarget
          Engine: AiIntentEngine option
          Invocation: AiIntentInvocation option
          Body: string
          Tags: string list }

    let textOrEmpty (value: string) =
        if isNull value then String.Empty else value.Trim()

    let tryGetProperty (name: string) (element: JsonElement) =
        let mutable value = Unchecked.defaultof<JsonElement>

        if element.ValueKind = JsonValueKind.Object && element.TryGetProperty(name, &value) then
            Some value
        else
            None

    let stringProperty name (element: JsonElement) =
        tryGetProperty name element
        |> Option.bind (fun value ->
            if value.ValueKind = JsonValueKind.String then
                value.GetString() |> textOrEmpty |> Some
            else
                None)
        |> Option.defaultValue String.Empty

    let stringArrayProperty name (element: JsonElement) =
        tryGetProperty name element
        |> Option.map (fun value ->
            if value.ValueKind = JsonValueKind.Array then
                value.EnumerateArray()
                |> Seq.choose (fun item ->
                    if item.ValueKind = JsonValueKind.String then
                        let text = item.GetString() |> textOrEmpty

                        if String.IsNullOrWhiteSpace text then None else Some text
                    else
                        None)
                |> Seq.toList
            else
                [])
        |> Option.defaultValue []

    let objectProperty name (element: JsonElement) =
        tryGetProperty name element
        |> Option.filter (fun value -> value.ValueKind = JsonValueKind.Object)

    let tryParseAiIntent (rawValue: string) =
        if String.IsNullOrWhiteSpace rawValue then
            None
        else
            try
                use document = JsonDocument.Parse rawValue
                let root = document.RootElement
                let schema = stringProperty "schema" root

                if not (schema.Equals(Package.intentSchema, StringComparison.OrdinalIgnoreCase)) then
                    None
                else
                    let body = stringProperty "body" root

                    if String.IsNullOrWhiteSpace body then
                        None
                    else
                        let targetElement =
                            tryGetProperty "target" root
                            |> Option.defaultValue Unchecked.defaultof<JsonElement>

                        let engine =
                            objectProperty "engine" root
                            |> Option.map (fun engineElement ->
                                { Engine = stringProperty "engine" engineElement
                                  Model = stringProperty "model" engineElement
                                  Reasoning = stringProperty "reasoning" engineElement })

                        let invocation =
                            objectProperty "invocation" root
                            |> Option.map (fun invocationElement ->
                                { Mode = stringProperty "mode" invocationElement
                                  Approval = stringProperty "approval" invocationElement })

                        Some
                            { Target =
                                { Mode = stringProperty "mode" targetElement
                                  Scope = stringProperty "scope" targetElement
                                  ParticipantId = stringProperty "participantId" targetElement
                                  GroupId = stringProperty "groupId" targetElement }
                              Engine = engine
                              Invocation = invocation
                              Body = body
                              Tags = stringArrayProperty "tags" root }
            with _ ->
                None

    let intentScope (intent: AiIntent) =
        let mode = intent.Target.Mode.Trim().ToLowerInvariant()
        let scope = intent.Target.Scope.Trim().ToLowerInvariant()

        match mode, scope with
        | "public", _
        | _, "public" -> Some(MessageFabricScope.Public(Some Domain.publicChannelName))
        | "group", _
        | _, "group" ->
            let groupId = textOrEmpty intent.Target.GroupId

            if String.IsNullOrWhiteSpace groupId then None else Some(MessageFabricScope.Group groupId)
        | "participant", _ ->
            let participantId = textOrEmpty intent.Target.ParticipantId

            if String.IsNullOrWhiteSpace participantId then None else Some(MessageFabricScope.Direct participantId)
        | "foreman", _
        | _, "direct" ->
            let participantId =
                intent.Target.ParticipantId
                |> textOrEmpty
                |> fun value -> if String.IsNullOrWhiteSpace value then "agent.codexfs.foreman" else value

            Some(MessageFabricScope.Direct participantId)
        | _ -> Some(MessageFabricScope.Direct "agent.codexfs.foreman")

    let optionalTag prefix value =
        let text = textOrEmpty value

        if String.IsNullOrWhiteSpace text then
            []
        else
            [ prefix + text ]

    let aiIntentRuntimeTags (intent: AiIntent) =
        [ match intent.Engine with
          | Some engine ->
              yield! optionalTag "engine:" engine.Engine
              yield! optionalTag "model:" engine.Model
              yield! optionalTag "reasoning:" engine.Reasoning
          | None -> ()
          match intent.Invocation with
          | Some invocation ->
              yield! optionalTag "invocation:" invocation.Mode
              yield! optionalTag "approval:" invocation.Approval
          | None -> () ]

    let ensureAiIntentBridgeParticipantAsync (fabric: CommSpaMessageFabric) =
        let binding = MessageFabricBinding.defaultBinding aiIntentBridgeParticipantId

        MessageFabricBinding.registerParticipantAsync
            fabric
            binding
            { MessageFabricBinding.defaultRegistration with
                DisplayName = Some "codex.fs Web AI Intent"
                Kind = Some "user"
                Labels = Some [ "codex.fs"; "ai-chat"; "bridge"; "web" ] }
        :> Task

    let aiIntentPages (hub: CommHub) : AppendPageDefinition list =
        hub.ListAppendPages().Pages
        |> List.filter (fun page -> page.Shape.Equals("codexfs-ai-chat", StringComparison.OrdinalIgnoreCase))

    let aiIntentValues (hub: CommHub) (page: AppendPageDefinition) =
        let snapshot = hub.SetsSnapshot(None, Some page.SetName, Some 500)

        snapshot.Buckets
        |> List.collect _.Values
        |> List.filter (fun value ->
            value.Tags
            |> List.exists (fun tag -> tag.Equals("shape:codexfs-ai-chat", StringComparison.OrdinalIgnoreCase)))
        |> List.sortBy (fun value -> value.CreatedAtUtc, value.ValueId)

    let runAiIntentBridgeOnceAsync (hub: CommHub) (fabric: CommSpaMessageFabric) (processedValueIds: HashSet<string>) =
        task {
            let mutable bridged = 0

            for page in aiIntentPages hub do
                for value in aiIntentValues hub page do
                    if processedValueIds.Add value.ValueId then
                        match tryParseAiIntent value.Value with
                        | None -> ()
                        | Some intent ->
                            match intentScope intent with
                            | None -> ()
                            | Some scope ->
                                let tags =
                                    [ yield! intent.Tags
                                      yield! aiIntentRuntimeTags intent
                                      "codex.fs"
                                      "ai-intent-bridge"
                                      "source:" + value.ValueId ]
                                    |> List.distinctBy _.ToLowerInvariant()

                                let! _ =
                                    MessageFabricBinding.sendAsync
                                        fabric
                                        { FromParticipantId = aiIntentBridgeParticipantId
                                          Scope = scope
                                          Body = intent.Body
                                          Tags = tags
                                          CorrelationId = Some("ai-intent:" + value.ValueId)
                                          CreatedAtUtc = Some value.CreatedAtUtc }

                                bridged <- bridged + 1

            return bridged
        }

    let startAiIntentBridgeLoop (_startedUtc: DateTimeOffset) (hub: CommHub) (fabric: CommSpaMessageFabric) =
        let cancellation = new CancellationTokenSource()
        let token = cancellation.Token
        let processedValueIds = HashSet<string>(StringComparer.Ordinal)

        let loopTask =
            task {
                do! ensureAiIntentBridgeParticipantAsync fabric

                while not token.IsCancellationRequested do
                    try
                        let! _ = runAiIntentBridgeOnceAsync hub fabric processedValueIds
                        do! Task.Delay(TimeSpan.FromSeconds 1.0, token)
                    with
                    | :? OperationCanceledException -> ()
                    | _ ->
                        do! Task.Delay(TimeSpan.FromSeconds 2.0, token)
            }

        Some(loopTask :> Task), Some cancellation

    let startForemanRuntimeLoop (startedUtc: DateTimeOffset) (runtime: HostRuntime.HostRuntime) (fabric: CommSpaMessageFabric) (actorFabric: CommSpaActorFabric option) =
        match actorFabric with
        | None -> None, None, None
        | Some actorFabric ->
            let spec = ActorFabricBinding.foremanSpec runtime.Config.Ptcs.SessionParticipantPrefix
            let actorName = ActorFabricBinding.actorNameFromParticipantId "foreman" spec.ParticipantId
            let foreman = ActorFabricBinding.spawnWorker actorFabric fabric actorName spec
            let loopCancellation = new CancellationTokenSource()
            let token = loopCancellation.Token
            let askTimeout = runtime.Config.DefaultTimeout + TimeSpan.FromSeconds 30.0

            let loopTask =
                task {
                    let registerCommand: ActorFabricBinding.EnsureParticipantRegistered =
                        { RequestedAtUtc = Some startedUtc }

                    let! _ =
                        foreman.Ask<ActorFabricBinding.WorkerParticipantRegistered>(
                            registerCommand,
                            TimeSpan.FromSeconds 10.0
                        )

                    while not token.IsCancellationRequested do
                        try
                            let command = foremanRuntimeCommand runtime spec
                            let! _ = foreman.Ask<ActorFabricBinding.RuntimeCycleCompleted>(command, askTimeout)
                            do! Task.Delay(TimeSpan.FromSeconds 1.0, token)
                        with
                        | :? OperationCanceledException -> ()
                        | _ ->
                            do! Task.Delay(TimeSpan.FromSeconds 2.0, token)
                }

            Some foreman, Some(loopTask :> Task), Some loopCancellation

    /// Start PTCS classic `/chat` using the codex.fs AI chat extension and a shared MessageFabric hub.
    let tryStartAsync (startedUtc: DateTimeOffset) (cancellationToken: CancellationToken) (runtime: HostRuntime.HostRuntime) =
        task {
            let validationErrors =
                HostConfig.validate runtime.Config
                |> List.filter (fun issue -> issue.Severity = HostConfig.IssueError)

            if not validationErrors.IsEmpty then
                return Error validationErrors
            elif not (isEnabled runtime.Config) then
                return Error [ webShellDisabledIssue ]
            else
                cancellationToken.ThrowIfCancellationRequested()

                let hub = registeredHub runtime.Config
                let fabric = CommSpaMessageFabric.create hub
                let runtime = HostRuntime.startWithMessageFabric startedUtc fabric runtime
                let options = serverOptions runtime.Config hub
                let server = Server.start options
                let foremanActor, foremanLoop, foremanLoopCancellation =
                    startForemanRuntimeLoop startedUtc runtime fabric server.ActorFabric
                let aiIntentBridgeLoop, aiIntentBridgeCancellation =
                    startAiIntentBridgeLoop startedUtc hub fabric

                let extension =
                    hub.ListClientExtensions()
                    |> List.tryFind (fun item -> item.ExtensionId = Package.extensionId)

                let contract =
                    { buildContract runtime with
                        ScriptUrls = extension |> Option.map _.ScriptUrls |> Option.defaultValue [] }

                return
                    Ok
                        { Runtime = runtime
                          Contract = contract
                          Server = server
                          ForemanActor = foremanActor
                          ForemanLoop = foremanLoop
                          ForemanLoopCancellation = foremanLoopCancellation
                          AiIntentBridgeLoop = aiIntentBridgeLoop
                          AiIntentBridgeCancellation = aiIntentBridgeCancellation
                          StartedUtc = startedUtc }
        }

    /// Stop and dispose the PTCS web shell, returning stopped runtime state.
    let stopAsync (_cancellationToken: CancellationToken) server =
        task {
            match server.ForemanLoopCancellation with
            | Some source when not source.IsCancellationRequested ->
                source.Cancel()
                source.Dispose()
            | Some source -> source.Dispose()
            | None -> ()

            match server.AiIntentBridgeCancellation with
            | Some source when not source.IsCancellationRequested ->
                source.Cancel()
                source.Dispose()
            | Some source -> source.Dispose()
            | None -> ()

            match server.ForemanLoop with
            | Some loop when not loop.IsCompleted ->
                try
                    loop.Wait(TimeSpan.FromSeconds 5.0) |> ignore
                with _ ->
                    ()
            | _ -> ()

            match server.AiIntentBridgeLoop with
            | Some loop when not loop.IsCompleted ->
                try
                    loop.Wait(TimeSpan.FromSeconds 5.0) |> ignore
                with _ ->
                    ()
            | _ -> ()

            server.Server.Stop()
            return HostRuntime.stop server.Runtime
        }
