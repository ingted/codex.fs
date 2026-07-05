namespace CodexFs.Host

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open CodexFs
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
        hub

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
                          StartedUtc = startedUtc }
        }

    /// Stop and dispose the PTCS web shell, returning stopped runtime state.
    let stopAsync (_cancellationToken: CancellationToken) server =
        task {
            server.Server.Stop()
            return HostRuntime.stop server.Runtime
        }
