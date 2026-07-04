namespace CodexFs.Host

open System
open System.Net
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open CodexFs
open CodexFs.Ptcs
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.OpenApi
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open PulseTrade.Comm.Spa

/// HTTP control plane contract and minimal Kestrel host for codex.fs.host.
module HostControl =

    /// Stable host control routes.
    module Routes =

        /// Human-facing landing page for operators opening the advertised host root URL.
        [<Literal>]
        let Root = "/"

        /// Health endpoint used by CLI/Web/admin callers to inspect non-secret host status.
        [<Literal>]
        let Health = "/api/codexfs/host/health"

        /// Session message send route used by codex.fs.cli.
        [<Literal>]
        let SessionMessages = "/api/codexfs/session/{sessionId}/messages"

        /// Session status route used by codex.fs.cli.
        [<Literal>]
        let SessionStatus = "/api/codexfs/session/{sessionId}/status"

        /// Session bounded attach route used by codex.fs.cli.
        [<Literal>]
        let SessionAttach = "/api/codexfs/session/{sessionId}/attach"

        /// Session drain route used by codex.fs.cli.
        [<Literal>]
        let SessionDrain = "/api/codexfs/session/{sessionId}/drain"

        /// OpenAPI document route pattern used by ASP.NET Core `MapOpenApi`.
        [<Literal>]
        let OpenApiJsonPattern = "/openapi/{documentName}.json"

        /// Concrete OpenAPI v1 JSON route.
        [<Literal>]
        let OpenApiJson = "/openapi/v1.json"

    /// Stable endpoint names for OpenAPI metadata and CLI documentation.
    module EndpointNames =

        /// Endpoint name for `GET /`.
        [<Literal>]
        let Root = "CodexFsHostRoot"

        /// Endpoint name for `GET /api/codexfs/host/health`.
        [<Literal>]
        let Health = "CodexFsHostHealth"

        /// Endpoint name for `POST /api/codexfs/session/{sessionId}/messages`.
        [<Literal>]
        let SessionMessages = "CodexFsSessionMessages"

        /// Endpoint name for `GET /api/codexfs/session/{sessionId}/status`.
        [<Literal>]
        let SessionStatus = "CodexFsSessionStatus"

        /// Endpoint name for `POST /api/codexfs/session/{sessionId}/attach`.
        [<Literal>]
        let SessionAttach = "CodexFsSessionAttach"

        /// Endpoint name for `POST /api/codexfs/session/{sessionId}/drain`.
        [<Literal>]
        let SessionDrain = "CodexFsSessionDrain"

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
          Endpoints: HostControlEndpointDefinition list
          /// True when OpenAPI JSON is mapped by the host.
          GenerateOpenApi: bool
          /// Concrete advertised OpenAPI JSON URI.
          OpenApiJsonUri: string
          /// True when Swagger UI is exposed by the host profile.
          ExposeSwaggerUi: bool
          /// Route prefix used for Swagger UI.
          SwaggerUiRoutePrefix: string
          /// Concrete advertised Swagger UI URI.
          SwaggerUiUri: string }

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
          Diagnostics: HostControlDiagnosticResponse list
          /// True when OpenAPI JSON is mapped by the host.
          GenerateOpenApi: bool
          /// Advertised OpenAPI JSON URI.
          OpenApiJsonUri: string
          /// True when Swagger UI is exposed by the host profile.
          ExposeSwaggerUi: bool
          /// Advertised Swagger UI URI.
          SwaggerUiUri: string }

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

    /// Request body accepted by `POST /api/codexfs/session/{sessionId}/messages`.
    type SessionSendRequest =
        { /// Prompt text or message body to append to the session inbox.
          Prompt: string
          /// Participant id representing the CLI/user sender; defaults are applied by CLI callers.
          FromParticipantId: string
          /// Non-secret tags to attach to the PTCS message.
          Tags: string list
          /// Optional idempotency/correlation id; use blank when absent.
          CorrelationId: string }

    /// Response body returned after the host accepts a session message into PTCS MessageFabric.
    type SessionSendResponse =
        { /// Stable status text.
          Status: string
          /// Session id from the route.
          SessionId: string
          /// PTCS participant id derived for the session.
          SessionParticipantId: string
          /// Participant id used as the message sender.
          FromParticipantId: string
          /// PTCS message id returned by MessageFabric.
          MessageId: string
          /// Inbox cursor associated with the accepted message.
          Cursor: string
          /// Correlation id echoed from the request, or blank when absent.
          CorrelationId: string
          /// Tags attached to the PTCS message.
          Tags: string list }

    /// One session inbox message returned by status/attach/drain endpoints.
    type SessionInboxMessageResponse =
        { /// PTCS message id.
          MessageId: string
          /// Inbox cursor associated with the message.
          Cursor: string
          /// Sender participant id.
          FromParticipantId: string
          /// Message body.
          Body: string
          /// Correlation id or blank when absent.
          CorrelationId: string
          /// Non-secret tags.
          Tags: string list }

    /// Response returned by session status/attach/drain endpoints.
    type SessionInboxResponse =
        { /// Stable status text.
          Status: string
          /// Session id from the route.
          SessionId: string
          /// PTCS participant id derived for the session.
          SessionParticipantId: string
          /// Number of messages in the returned batch.
          PendingCount: int
          /// Inbox cursor returned by PTCS, or blank when absent.
          NextCursor: string
          /// Messages returned by the operation.
          Messages: SessionInboxMessageResponse list
          /// Human-readable transcript text for terminal output.
          Transcript: string }

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

    let sessionSendSuccessExample =
        { Name = "message-accepted"
          Description = "A CLI prompt was accepted into the PTCS inbox owned by one session participant."
          Body =
            """{"status":"accepted","sessionId":"sess-001","sessionParticipantId":"agent.codexfs.sess-001","fromParticipantId":"user.codexfs.cli","messageId":"msg-001","cursor":"msg-001","correlationId":"cli-001","tags":["codex.fs","cli","session-send"]}""" }

    let sessionSendFailureExample =
        { Name = "blank-prompt"
          Description = "The host rejects a blank prompt before appending anything to MessageFabric."
          Body =
            """{"code":"invalid-session-message","message":"Prompt must not be blank."}""" }

    let sessionInboxSuccessExample =
        { Name = "session-inbox"
          Description = "A bounded session inbox read returned one CLI-submitted message."
          Body =
            """{"status":"ok","sessionId":"sess-001","sessionParticipantId":"agent.codexfs.sess-001","pendingCount":1,"messages":[{"messageId":"msg-001","fromParticipantId":"user.codexfs.cli","body":"hello"}],"transcript":"user.codexfs.cli: hello"}""" }

    let sessionInboxFailureExample =
        { Name = "message-fabric-unavailable"
          Description = "The host has not initialized MessageFabric."
          Body =
            """{"code":"message-fabric-unavailable","message":"Host MessageFabric is not initialized."}""" }

    let rootSuccessExample =
        { Name = "operator-landing"
          Description = "A human-facing landing page with links to health, OpenAPI JSON, and Swagger UI when available."
          Body = """<html><body><h1>codex.fs host</h1></body></html>""" }

    /// Endpoint definitions that act as the canonical code-side docs metadata for the HTTP control plane.
    let endpointDefinitions =
        [ { Method = "GET"
            Route = Routes.Root
            Name = EndpointNames.Root
            Summary = "Return a human-facing codex.fs host landing page."
            SuccessExample = rootSuccessExample
            FailureExample = healthFailureExample }
          { Method = "GET"
            Route = Routes.Health
            Name = EndpointNames.Health
            Summary = "Return non-secret codex.fs host health and network profile metadata."
            SuccessExample = healthSuccessExample
            FailureExample = healthFailureExample }
          { Method = "POST"
            Route = Routes.SessionMessages
            Name = EndpointNames.SessionMessages
            Summary = "Accept one CLI session message into PTCS MessageFabric through the host."
            SuccessExample = sessionSendSuccessExample
            FailureExample = sessionSendFailureExample }
          { Method = "GET"
            Route = Routes.SessionStatus
            Name = EndpointNames.SessionStatus
            Summary = "Poll the current session inbox without acknowledging messages."
            SuccessExample = sessionInboxSuccessExample
            FailureExample = sessionInboxFailureExample }
          { Method = "POST"
            Route = Routes.SessionAttach
            Name = EndpointNames.SessionAttach
            Summary = "Bounded wait for session inbox messages without acknowledging them."
            SuccessExample = sessionInboxSuccessExample
            FailureExample = sessionInboxFailureExample }
          { Method = "POST"
            Route = Routes.SessionDrain
            Name = EndpointNames.SessionDrain
            Summary = "Drain current session inbox messages and acknowledge the returned cursor."
            SuccessExample = sessionInboxSuccessExample
            FailureExample = sessionInboxFailureExample } ]

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

    /// Normalize a Swagger UI route prefix for ASP.NET Core middleware.
    let normalizeSwaggerRoutePrefix (routePrefix: string option) =
        match routePrefix with
        | Some value when not (String.IsNullOrWhiteSpace value) -> value.Trim().Trim('/')
        | _ -> "swagger"

    /// Convert a Swagger UI route prefix into the advertised index route.
    let swaggerUiIndexRoute routePrefix =
        if String.IsNullOrWhiteSpace routePrefix then
            "/index.html"
        else
            "/" + routePrefix.Trim('/') + "/index.html"

    /// Build the effective HTTP control contract from a validated host configuration.
    let buildContract (config: HostConfig.HostConfig) =
        let control = config.ControlEndpoint
        let port = resolvePort control
        let protocol = control.Protocol.Trim().ToLowerInvariant()
        let bindAddress = control.BindAddress.Trim()
        let bindUri = $"{protocol}://{formatHostForUrl bindAddress}:{port}"
        let advertiseUri = control.AdvertiseUri.TrimEnd('/')
        let swaggerRoutePrefix = normalizeSwaggerRoutePrefix config.ApiDocs.SwaggerRoutePrefix

        { Protocol = protocol
          BindAddress = bindAddress
          Port = port
          BindUri = bindUri
          AdvertiseUri = advertiseUri
          HealthUri = combineAdvertisedRoute advertiseUri Routes.Health
          AllowLoopbackOnly = control.AllowLoopbackOnly
          Endpoints = endpointDefinitions
          GenerateOpenApi = config.ApiDocs.GenerateOpenApi
          OpenApiJsonUri = combineAdvertisedRoute advertiseUri Routes.OpenApiJson
          ExposeSwaggerUi = config.ApiDocs.ExposeSwaggerUi
          SwaggerUiRoutePrefix = swaggerRoutePrefix
          SwaggerUiUri = combineAdvertisedRoute advertiseUri (swaggerUiIndexRoute swaggerRoutePrefix) }

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
                  WasRedacted = diagnostic.WasRedacted })
          GenerateOpenApi = contract.GenerateOpenApi
          OpenApiJsonUri = contract.OpenApiJsonUri
          ExposeSwaggerUi = contract.ExposeSwaggerUi
          SwaggerUiUri = contract.SwaggerUiUri }

    /// Build the PTCS participant id owned by a session.
    let sessionParticipantId (config: HostConfig.HostConfig) sessionId =
        config.Ptcs.SessionParticipantPrefix.TrimEnd('.') + "." + sessionId

    /// Build the advertised URI for a session message send request.
    let sessionMessagesUri (contract: HostControlContract) (sessionId: string) =
        let escapedSessionId = Uri.EscapeDataString sessionId
        combineAdvertisedRoute contract.AdvertiseUri ($"/api/codexfs/session/{escapedSessionId}/messages")

    /// Build the advertised URI for a session status request.
    let sessionStatusUri (contract: HostControlContract) (sessionId: string) =
        let escapedSessionId = Uri.EscapeDataString sessionId
        combineAdvertisedRoute contract.AdvertiseUri ($"/api/codexfs/session/{escapedSessionId}/status")

    /// Build the advertised URI for a bounded session attach request.
    let sessionAttachUri (contract: HostControlContract) (sessionId: string) =
        let escapedSessionId = Uri.EscapeDataString sessionId
        combineAdvertisedRoute contract.AdvertiseUri ($"/api/codexfs/session/{escapedSessionId}/attach")

    /// Build the advertised URI for a session drain request.
    let sessionDrainUri (contract: HostControlContract) (sessionId: string) =
        let escapedSessionId = Uri.EscapeDataString sessionId
        combineAdvertisedRoute contract.AdvertiseUri ($"/api/codexfs/session/{escapedSessionId}/drain")

    let defaultSenderParticipantId = "user.codexfs.cli"

    let normalizeTags tags =
        if isNull (box tags) || List.isEmpty tags then
            [ "codex.fs"; "cli"; "session-send" ]
        else
            tags

    let optionalNonBlank value =
        if String.IsNullOrWhiteSpace value then None else Some value

    let errorResult (statusCode: int) code message =
        Results.Json({ Code = code; Message = message }, statusCode = Nullable<int>(statusCode))

    let htmlEncode (text: string) = WebUtility.HtmlEncode(if isNull text then String.Empty else text)

    let rootPageHtml (contract: HostControlContract) =
        let docsItems =
            [ yield $"<li><a href=\"{htmlEncode contract.HealthUri}\">Host health JSON</a></li>"
              if contract.GenerateOpenApi then
                  yield $"<li><a href=\"{htmlEncode contract.OpenApiJsonUri}\">OpenAPI JSON</a></li>"
              if contract.GenerateOpenApi && contract.ExposeSwaggerUi then
                  yield $"<li><a href=\"{htmlEncode contract.SwaggerUiUri}\">Swagger UI</a></li>" ]
            |> String.concat Environment.NewLine

        $"""<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>codex.fs host</title>
  <style>
    body {{ font-family: system-ui, -apple-system, Segoe UI, sans-serif; max-width: 860px; margin: 48px auto; padding: 0 24px; line-height: 1.5; }}
    code {{ background: #f4f4f4; padding: 2px 5px; border-radius: 4px; }}
    .status {{ display: inline-block; padding: 2px 8px; border: 1px solid #7fbf7f; border-radius: 4px; color: #166116; }}
  </style>
</head>
<body>
  <h1>codex.fs host</h1>
  <p class="status">running</p>
  <p>Advertised URI: <code>{htmlEncode contract.AdvertiseUri}</code></p>
  <h2>Available endpoints</h2>
  <ul>
    {docsItems}
  </ul>
  <h2>CLI</h2>
  <p><code>codex.fs.cli host status --host {htmlEncode contract.AdvertiseUri}</code></p>
</body>
</html>"""

    let endpointDefinition name =
        endpointDefinitions
        |> List.find (fun definition -> String.Equals(definition.Name, name, StringComparison.Ordinal))

    let endpointDescription (definition: HostControlEndpointDefinition) =
        String.concat
            Environment.NewLine
            [ definition.Summary
              $"Success example: {definition.SuccessExample.Description}"
              $"Failure example: {definition.FailureExample.Description}" ]

    let withEndpointDoc name (builder: RouteHandlerBuilder) =
        let definition = endpointDefinition name

        builder
            .WithName(definition.Name)
            .WithTags("Host Control")
            .WithSummary(definition.Summary)
            .WithDescription(endpointDescription definition)

    let sessionBinding (config: HostConfig.HostConfig) sessionId =
        { MessageFabricBinding.defaultBinding (sessionParticipantId config sessionId) with
            InboxLimit = config.Ptcs.DefaultInboxLimit }

    let envelopeToInboxMessage (message: MessageFabricEnvelope) =
        { MessageId = message.MessageId
          Cursor = message.MessageId
          FromParticipantId = message.FromParticipantId
          Body = message.Body
          CorrelationId = String.Empty
          Tags = if isNull (box message.Tags) then [] else message.Tags }

    let inboxResponse status sessionId sessionParticipantId nextCursor messages =
        let responseMessages = messages |> List.map envelopeToInboxMessage
        let transcript =
            responseMessages
            |> List.map (fun message -> $"{message.FromParticipantId}: {message.Body}")
            |> String.concat Environment.NewLine

        { Status = status
          SessionId = sessionId
          SessionParticipantId = sessionParticipantId
          PendingCount = responseMessages.Length
          NextCursor = nextCursor |> Option.defaultValue String.Empty
          Messages = responseMessages
          Transcript = transcript }

    /// Append one CLI-submitted prompt into the PTCS inbox for the selected session participant.
    let sendSessionMessageAsync (runtime: HostRuntime.HostRuntime) sessionId (request: SessionSendRequest) =
        task {
            match runtime.MessageFabric with
            | None ->
                return errorResult StatusCodes.Status503ServiceUnavailable "message-fabric-unavailable" "Host MessageFabric is not initialized."
            | Some fabric ->
                if String.IsNullOrWhiteSpace sessionId then
                    return errorResult StatusCodes.Status400BadRequest "invalid-session-message" "Session id must not be blank."
                elif isNull (box request) || String.IsNullOrWhiteSpace request.Prompt then
                    return errorResult StatusCodes.Status400BadRequest "invalid-session-message" "Prompt must not be blank."
                else
                    let sessionParticipantId = sessionParticipantId runtime.Config sessionId
                    let fromParticipantId =
                        if String.IsNullOrWhiteSpace request.FromParticipantId then defaultSenderParticipantId else request.FromParticipantId

                    let tags = normalizeTags request.Tags
                    let sessionBinding = MessageFabricBinding.defaultBinding sessionParticipantId
                    let senderBinding = MessageFabricBinding.defaultBinding fromParticipantId

                    let! _ =
                        MessageFabricBinding.registerParticipantAsync
                            fabric
                            sessionBinding
                            { MessageFabricBinding.defaultRegistration with
                                DisplayName = Some sessionParticipantId
                                Kind = Some "agent"
                                Labels = Some [ "codex.fs"; "session"; "cli-target" ] }

                    let! _ =
                        MessageFabricBinding.registerParticipantAsync
                            fabric
                            senderBinding
                            { MessageFabricBinding.defaultRegistration with
                                DisplayName = Some fromParticipantId
                                Kind = Some "user"
                                Labels = Some [ "codex.fs"; "cli"; "sender" ] }

                    let correlationId = optionalNonBlank request.CorrelationId

                    let! envelope =
                        MessageFabricBinding.sendAsync
                            fabric
                            { FromParticipantId = fromParticipantId
                              Scope = MessageFabricScope.Direct sessionParticipantId
                              Body = request.Prompt
                              Tags = tags
                              CorrelationId = correlationId
                              CreatedAtUtc = None }

                    let response =
                        { Status = "accepted"
                          SessionId = sessionId
                          SessionParticipantId = sessionParticipantId
                          FromParticipantId = fromParticipantId
                          MessageId = envelope.MessageId
                          Cursor = envelope.MessageId
                          CorrelationId = correlationId |> Option.defaultValue String.Empty
                          Tags = tags }

                    return Results.Json(response, statusCode = Nullable<int>(StatusCodes.Status202Accepted))
        }

    /// Poll the current session inbox without acknowledging messages.
    let sessionStatusAsync (runtime: HostRuntime.HostRuntime) sessionId =
        task {
            match runtime.MessageFabric with
            | None ->
                return errorResult StatusCodes.Status503ServiceUnavailable "message-fabric-unavailable" "Host MessageFabric is not initialized."
            | Some fabric ->
                if String.IsNullOrWhiteSpace sessionId then
                    return errorResult StatusCodes.Status400BadRequest "invalid-session-inbox" "Session id must not be blank."
                else
                    let binding = sessionBinding runtime.Config sessionId
                    let! batch = MessageFabricBinding.pollInboxAsync fabric binding None
                    let response = inboxResponse "ok" sessionId binding.ParticipantId batch.NextCursor batch.Messages
                    return Results.Json(response)
        }

    /// Bounded wait for session inbox messages without acknowledging them.
    let sessionAttachAsync (runtime: HostRuntime.HostRuntime) sessionId =
        task {
            match runtime.MessageFabric with
            | None ->
                return errorResult StatusCodes.Status503ServiceUnavailable "message-fabric-unavailable" "Host MessageFabric is not initialized."
            | Some fabric ->
                if String.IsNullOrWhiteSpace sessionId then
                    return errorResult StatusCodes.Status400BadRequest "invalid-session-inbox" "Session id must not be blank."
                else
                    let binding = sessionBinding runtime.Config sessionId
                    let! batch =
                        MessageFabricBinding.waitInboxAsync
                            fabric
                            binding
                            None
                            (TimeSpan.FromSeconds 1.0)
                            (TimeSpan.FromMilliseconds 20.0)
                            (Some CancellationToken.None)

                    let response = inboxResponse "ok" sessionId binding.ParticipantId batch.NextCursor batch.Messages
                    return Results.Json(response)
        }

    /// Drain current session inbox messages and acknowledge the returned cursor.
    let sessionDrainAsync (runtime: HostRuntime.HostRuntime) sessionId =
        task {
            match runtime.MessageFabric with
            | None ->
                return errorResult StatusCodes.Status503ServiceUnavailable "message-fabric-unavailable" "Host MessageFabric is not initialized."
            | Some fabric ->
                if String.IsNullOrWhiteSpace sessionId then
                    return errorResult StatusCodes.Status400BadRequest "invalid-session-inbox" "Session id must not be blank."
                else
                    let binding = sessionBinding runtime.Config sessionId
                    let! batch = MessageFabricBinding.drainInboxAsync fabric binding None
                    let response = inboxResponse "drained" sessionId binding.ParticipantId batch.NextCursor batch.Messages
                    return Results.Json(response)
        }

    /// Attach the current HTTP control endpoints to an ASP.NET Core application.
    let mapEndpoints (application: WebApplication) (contract: HostControlContract) (runtime: HostRuntime.HostRuntime) =
        let rootHandler =
            Func<IResult>(fun () -> Results.Content(rootPageHtml contract, "text/html; charset=utf-8"))

        (application.MapGet(Routes.Root, rootHandler) |> withEndpointDoc EndpointNames.Root)
            .Produces(StatusCodes.Status200OK, contentType = "text/html")
        |> ignore

        let healthHandler =
            Func<IResult>(fun () -> Results.Json(healthResponse contract runtime))

        (application.MapGet(Routes.Health, healthHandler) |> withEndpointDoc EndpointNames.Health)
            .Produces<HostControlHealthResponse>(StatusCodes.Status200OK)
        |> ignore

        let sessionSendHandler =
            Func<string, SessionSendRequest, Task<IResult>>(fun sessionId request -> sendSessionMessageAsync runtime sessionId request)

        (application.MapPost(Routes.SessionMessages, sessionSendHandler) |> withEndpointDoc EndpointNames.SessionMessages)
            .Accepts<SessionSendRequest>("application/json")
            .Produces<SessionSendResponse>(StatusCodes.Status202Accepted)
            .Produces<HostControlErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<HostControlErrorResponse>(StatusCodes.Status503ServiceUnavailable)
        |> ignore

        let sessionStatusHandler =
            Func<string, Task<IResult>>(fun sessionId -> sessionStatusAsync runtime sessionId)

        (application.MapGet(Routes.SessionStatus, sessionStatusHandler) |> withEndpointDoc EndpointNames.SessionStatus)
            .Produces<SessionInboxResponse>(StatusCodes.Status200OK)
            .Produces<HostControlErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<HostControlErrorResponse>(StatusCodes.Status503ServiceUnavailable)
        |> ignore

        let sessionAttachHandler =
            Func<string, Task<IResult>>(fun sessionId -> sessionAttachAsync runtime sessionId)

        (application.MapPost(Routes.SessionAttach, sessionAttachHandler) |> withEndpointDoc EndpointNames.SessionAttach)
            .Produces<SessionInboxResponse>(StatusCodes.Status200OK)
            .Produces<HostControlErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<HostControlErrorResponse>(StatusCodes.Status503ServiceUnavailable)
        |> ignore

        let sessionDrainHandler =
            Func<string, Task<IResult>>(fun sessionId -> sessionDrainAsync runtime sessionId)

        (application.MapPost(Routes.SessionDrain, sessionDrainHandler) |> withEndpointDoc EndpointNames.SessionDrain)
            .Produces<SessionInboxResponse>(StatusCodes.Status200OK)
            .Produces<HostControlErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<HostControlErrorResponse>(StatusCodes.Status503ServiceUnavailable)
        |> ignore

        application

    /// Attach OpenAPI JSON and optional Swagger UI routes according to the active host profile.
    let mapApiDocs (application: WebApplication) (contract: HostControlContract) =
        if contract.GenerateOpenApi then
            application.MapOpenApi(Routes.OpenApiJsonPattern) |> ignore

        if contract.GenerateOpenApi && contract.ExposeSwaggerUi then
            application.UseSwaggerUI(fun options ->
                options.RoutePrefix <- contract.SwaggerUiRoutePrefix
                options.SwaggerEndpoint(Routes.OpenApiJson, "codex.fs host control v1"))
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

                if contract.GenerateOpenApi then
                    builder.Services.AddOpenApi("v1") |> ignore

                builder.Services.ConfigureHttpJsonOptions(fun options -> options.SerializerOptions.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase)
                |> ignore

                builder.WebHost.UseUrls(contract.BindUri) |> ignore

                let application = builder.Build()
                mapApiDocs application contract |> ignore
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
