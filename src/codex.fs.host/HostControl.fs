namespace CodexFs.Host

open System
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open CodexFs
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting

/// HTTP control plane contract and minimal Kestrel host for codex.fs.host.
module HostControl =

    /// Stable host control routes.
    module Routes =

        /// Health endpoint used by CLI/Web/admin callers to inspect non-secret host status.
        [<Literal>]
        let Health = "/api/codexfs/host/health"

    /// Stable endpoint names for OpenAPI metadata and CLI documentation.
    module EndpointNames =

        /// Endpoint name for `GET /api/codexfs/host/health`.
        [<Literal>]
        let Health = "CodexFsHostHealth"

    /// Non-secret example attached to an HTTP control endpoint definition.
    type HostControlExample =
        { /// Short example name.
          Name: string
          /// What the example demonstrates.
          Description: string
          /// JSON body or error payload shown in SDK/OpenAPI documentation.
          Body: string }

    /// One HTTP control endpoint definition used as code-side documentation metadata.
    type HostControlEndpointDefinition =
        { /// HTTP method, for example `GET`.
          Method: string
          /// Absolute route path on the host.
          Route: string
          /// Stable endpoint name.
          Name: string
          /// Operator-facing endpoint summary.
          Summary: string
          /// Successful response example.
          SuccessExample: HostControlExample
          /// Meaningful failure response example.
          FailureExample: HostControlExample }

    /// Effective HTTP control contract derived from host configuration.
    type HostControlContract =
        { /// Control protocol. MVP supports `http`.
          Protocol: string
          /// Address used by Kestrel to bind locally.
          BindAddress: string
          /// TCP port used by Kestrel.
          Port: int
          /// Concrete Kestrel URL passed to the host builder.
          BindUri: string
          /// URI published to CLI clients and other nodes.
          AdvertiseUri: string
          /// Full advertised health URI.
          HealthUri: string
          /// True only for explicit single-node development loopback profiles.
          AllowLoopbackOnly: bool
          /// Endpoint definitions and examples for SDK/OpenAPI generation.
          Endpoints: HostControlEndpointDefinition list }

    /// Redacted diagnostic value returned by the health endpoint.
    type HostControlDiagnosticResponse =
        { /// Normalized setting key.
          Key: string
          /// Redacted setting value.
          Value: string
          /// True when the original value matched a high-risk redaction rule.
          WasRedacted: bool }

    /// JSON response returned by `GET /api/codexfs/host/health`.
    type HostControlHealthResponse =
        { /// Runtime lifecycle status.
          Status: string
          /// Default engine name.
          DefaultEngine: string
          /// Enabled engine names.
          EnabledEngines: string list
          /// Artifact root path from config.
          ArtifactRoot: string
          /// Control protocol.
          ControlProtocol: string
          /// Local bind address.
          BindAddress: string
          /// TCP bind port.
          Port: int
          /// Advertised base URI used by CLI clients and other nodes.
          AdvertiseUri: string
          /// Advertised health endpoint URI.
          HealthUri: string
          /// True when loopback-only control addresses are explicitly allowed.
          AllowLoopbackOnly: bool
          /// PTCS fabric mode selected by config.
          PtcsFabricMode: string
          /// Prefix used for PTCS session participants.
          PtcsSessionParticipantPrefix: string
          /// Default MessageFabric inbox limit.
          PtcsDefaultInboxLimit: int
          /// True when durable PTCS agent task handoff is enabled.
          DurableAgentTasks: bool
          /// True when a PTCS MessageFabric instance is present.
          HasMessageFabric: bool
          /// Concrete PTCS MessageFabric type name, or blank when absent.
          MessageFabricType: string
          /// Runtime start timestamp in round-trip UTC format, or blank when absent.
          StartedUtc: string
          /// Engine families with executable overrides; values are intentionally omitted.
          EngineOverrideKeys: string list
          /// Non-secret operator warnings.
          Warnings: string list
          /// Redacted config diagnostics.
          Diagnostics: HostControlDiagnosticResponse list }

    /// Running HTTP control server. The application is exposed so callers can attach package-owned hosting policy if required.
    type HostControlServer =
        { /// Runtime used to answer endpoint requests.
          Runtime: HostRuntime.HostRuntime
          /// Effective endpoint contract.
          Contract: HostControlContract
          /// Kestrel-backed ASP.NET Core application.
          Application: WebApplication
          /// UTC timestamp used when the host was started.
          StartedUtc: DateTimeOffset }

    /// Error response shape used by future control endpoints.
    type HostControlErrorResponse =
        { /// Stable machine-readable error code.
          Code: string
          /// Non-secret human-readable message.
          Message: string }

    let healthSuccessExample =
        { Name = "running-host"
          Description = "A running host that advertises a LAN/routable control URI."
          Body =
            """{"status":"running","controlProtocol":"http","advertiseUri":"http://192.168.10.20:8788","healthUri":"http://192.168.10.20:8788/api/codexfs/host/health","hasMessageFabric":true}""" }

    let healthFailureExample =
        { Name = "production-loopback-rejected"
          Description = "A clustered profile cannot advertise localhost or 127.0.0.1."
          Body =
            """{"code":"invalid-control-endpoint","message":"Cluster/production host config must advertise and bind a routable non-loopback address."}""" }

    /// Endpoint definitions that act as the canonical code-side docs metadata for the HTTP control plane.
    let endpointDefinitions =
        [ { Method = "GET"
            Route = Routes.Health
            Name = EndpointNames.Health
            Summary = "Return non-secret codex.fs host health and network profile metadata."
            SuccessExample = healthSuccessExample
            FailureExample = healthFailureExample } ]

    /// Resolve the configured bind port, falling back to the advertised URI port when no explicit port is configured.
    let resolvePort (control: HostConfig.HostControlEndpointConfig) =
        match control.Port with
        | Some port -> port
        | None ->
            let uri = Uri(control.AdvertiseUri)
            uri.Port

    /// Format a host literal for use inside an HTTP URL.
    let formatHostForUrl (host: string) =
        let value = if isNull host then String.Empty else host.Trim()

        if value.Contains(":", StringComparison.Ordinal) && not (value.StartsWith("[", StringComparison.Ordinal)) then
            $"[{value}]"
        else
            value

    /// Append a route to an advertised base URI without losing the configured scheme/host/port.
    let combineAdvertisedRoute (advertiseUri: string) (route: string) =
        let baseUri = advertiseUri.TrimEnd('/')
        let routeText = if route.StartsWith("/", StringComparison.Ordinal) then route else "/" + route
        baseUri + routeText

    /// Build the effective HTTP control contract from a validated host configuration.
    let buildContract (config: HostConfig.HostConfig) =
        let control = config.ControlEndpoint
        let port = resolvePort control
        let protocol = control.Protocol.Trim().ToLowerInvariant()
        let bindAddress = control.BindAddress.Trim()
        let bindUri = $"{protocol}://{formatHostForUrl bindAddress}:{port}"
        let advertiseUri = control.AdvertiseUri.TrimEnd('/')

        { Protocol = protocol
          BindAddress = bindAddress
          Port = port
          BindUri = bindUri
          AdvertiseUri = advertiseUri
          HealthUri = combineAdvertisedRoute advertiseUri Routes.Health
          AllowLoopbackOnly = control.AllowLoopbackOnly
          Endpoints = endpointDefinitions }

    /// Convert runtime health into an option-free JSON DTO for stable CLI/Web consumption.
    let healthResponse (contract: HostControlContract) (runtime: HostRuntime.HostRuntime) : HostControlHealthResponse =
        let health = HostRuntime.health runtime

        { Status = HostRuntime.formatStatus health.Status
          DefaultEngine = health.DefaultEngine
          EnabledEngines = health.EnabledEngines
          ArtifactRoot = health.ArtifactRoot
          ControlProtocol = health.ControlProtocol
          BindAddress = contract.BindAddress
          Port = contract.Port
          AdvertiseUri = contract.AdvertiseUri
          HealthUri = contract.HealthUri
          AllowLoopbackOnly = health.ControlAllowLoopbackOnly
          PtcsFabricMode = health.PtcsFabricMode
          PtcsSessionParticipantPrefix = health.PtcsSessionParticipantPrefix
          PtcsDefaultInboxLimit = health.PtcsDefaultInboxLimit
          DurableAgentTasks = health.DurableAgentTasks
          HasMessageFabric = health.HasMessageFabric
          MessageFabricType = health.MessageFabricType |> Option.defaultValue String.Empty
          StartedUtc = health.StartedUtc |> Option.map (fun value -> value.ToUniversalTime().ToString("O")) |> Option.defaultValue String.Empty
          EngineOverrideKeys = health.EngineOverrideKeys
          Warnings = health.Warnings
          Diagnostics =
            health.RedactedDiagnostics
            |> List.map (fun diagnostic ->
                { Key = diagnostic.Key
                  Value = diagnostic.Value
                  WasRedacted = diagnostic.WasRedacted }) }

    /// Attach the current HTTP control endpoints to an ASP.NET Core application.
    let mapEndpoints (application: WebApplication) (contract: HostControlContract) (runtime: HostRuntime.HostRuntime) =
        let healthHandler =
            Func<IResult>(fun () -> Results.Json(healthResponse contract runtime))

        application
            .MapGet(Routes.Health, healthHandler)
            .WithName(EndpointNames.Health)
            .Produces<HostControlHealthResponse>(StatusCodes.Status200OK)
        |> ignore

        application

    /// Start a real HTTP control endpoint using the configured bind address and advertised URI.
    let tryStartAsync (startedUtc: DateTimeOffset) (cancellationToken: CancellationToken) (runtime: HostRuntime.HostRuntime) =
        task {
            let validationErrors =
                HostConfig.validate runtime.Config
                |> List.filter (fun issue -> issue.Severity = HostConfig.IssueError)

            if not validationErrors.IsEmpty then
                return Error validationErrors
            else
                let runtime =
                    match runtime.MessageFabric with
                    | Some _ -> runtime
                    | None -> HostRuntime.startInProcessMessageFabric startedUtc runtime

                let contract = buildContract runtime.Config
                let builder = WebApplication.CreateBuilder([||])

                builder.Services.ConfigureHttpJsonOptions(fun options -> options.SerializerOptions.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase)
                |> ignore

                builder.WebHost.UseUrls(contract.BindUri) |> ignore

                let application = builder.Build()
                mapEndpoints application contract runtime |> ignore

                do! application.StartAsync(cancellationToken)

                return
                    Ok
                        { Runtime = runtime
                          Contract = contract
                          Application = application
                          StartedUtc = startedUtc }
        }

    /// Stop and dispose a running HTTP control server, returning the stopped runtime state.
    let stopAsync (cancellationToken: CancellationToken) server =
        task {
            do! server.Application.StopAsync(cancellationToken)
            do! server.Application.DisposeAsync().AsTask()
            return HostRuntime.stop server.Runtime
        }
