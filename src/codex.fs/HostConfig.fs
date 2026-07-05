namespace CodexFs

open System
open System.Globalization
open CodexFs.Domain

/// Host configuration model, parsing and redacted diagnostics.
module HostConfig =

    /// Redaction settings used by host diagnostics.
    type RedactionPolicy =
        { /// Apply built-in high-risk token-like redaction rules to diagnostics.
          EnableHighRiskRules: bool }

    /// HTTP control endpoint configuration.
    type HostControlEndpointConfig =
        { /// Control endpoint protocol. MVP supports `http`.
          Protocol: string
          /// Local address used by the host process to bind the control endpoint.
          BindAddress: string
          /// Optional fixed port. `None` lets the future runtime choose a port.
          Port: int option
          /// URI advertised to CLI clients and other nodes.
          AdvertiseUri: string
          /// Permit loopback bind/advertise addresses for single-node development.
          AllowLoopbackOnly: bool }

    /// Product PTCS WebSharper shell configuration.
    type HostWebShellConfig =
        { /// `control-only` keeps browser chat outside this host; `ptcs-webshell` starts PTCS classic `/chat`.
          Profile: string
          /// Local address used by the PTCS WebSharper shell listener.
          BindAddress: string
          /// Optional fixed web shell port.
          Port: int option
          /// URI advertised to browser users and other nodes.
          AdvertiseUri: string
          /// Permit loopback bind/advertise addresses for single-node development.
          AllowLoopbackOnly: bool
          /// PTCS actor fabric mode for the web shell, for example `auto-local` or `disabled`.
          ActorFabric: string
          /// Optional PCSL root for the PTCS hub state used by the web shell.
          PcslRoot: string option }

    /// Swagger/OpenAPI documentation settings for future host HTTP endpoints.
    type ApiDocsConfig =
        { /// Generate XML documentation for public API members.
          GenerateXmlDocs: bool
          /// Generate an OpenAPI document for HTTP control endpoints.
          GenerateOpenApi: bool
          /// Expose Swagger UI from the host process.
          ExposeSwaggerUi: bool
          /// Optional Swagger route prefix.
          SwaggerRoutePrefix: string option
          /// Include non-secret examples in generated documentation.
          IncludeExamples: bool }

    /// PTCS fabric settings consumed by the host runtime.
    type PtcsHostConfig =
        { /// PTCS fabric mode, for example `package-owned` or `caller-owned-cluster`.
          FabricMode: string
          /// Prefix used when deriving session participant ids.
          SessionParticipantPrefix: string
          /// Optional participant id used by host replies.
          ReplyParticipantId: string option
          /// Enable PTCS durable agent task handoff when a durable profile exists.
          DurableAgentTasks: bool
          /// Default MessageFabric inbox limit.
          DefaultInboxLimit: int }

    /// Effective host configuration.
    type HostConfig =
        { /// Directory root for run artifacts.
          ArtifactRoot: string
          /// Default engine used by new sessions.
          DefaultEngine: EngineKind
          /// Engine families allowed by this host.
          EnabledEngines: EngineKind list
          /// Optional executable path/command overrides per engine family.
          EngineExecutableOverrides: Map<EngineKind, string>
          /// Default engine execution timeout.
          DefaultTimeout: TimeSpan
          /// Maximum pending MessageFabric messages consumed per turn.
          MaxPendingMessagesPerTurn: int
          /// Local compaction policy.
          Compaction: Compaction.CompactionPolicy
          /// Redaction policy for diagnostics.
          Redaction: RedactionPolicy
          /// Control endpoint settings.
          ControlEndpoint: HostControlEndpointConfig
          /// Product PTCS WebSharper shell settings.
          WebShell: HostWebShellConfig
          /// API documentation settings.
          ApiDocs: ApiDocsConfig
          /// PTCS integration settings.
          Ptcs: PtcsHostConfig }

    /// Severity of one host config loading issue.
    type HostConfigIssueSeverity =
        /// Non-fatal issue.
        | IssueWarning
        /// Fatal issue; config must not be used.
        | IssueError

    /// Validation or parsing issue produced while loading host configuration.
    type HostConfigIssue =
        { /// Setting key associated with the issue.
          Key: string
          /// Issue severity.
          Severity: HostConfigIssueSeverity
          /// Human-readable non-secret issue message.
          Message: string }

    /// Redacted diagnostic value for one input setting.
    type HostConfigDiagnostic =
        { /// Normalized setting key.
          Key: string
          /// Redacted setting value.
          Value: string
          /// True when at least one high-risk value was redacted.
          WasRedacted: bool }

    /// Result of loading host configuration.
    type HostConfigLoadResult =
        { /// Effective configuration when no fatal issue exists.
          Config: HostConfig option
          /// Parsing and validation issues.
          Issues: HostConfigIssue list
          /// Redacted diagnostics for supplied settings.
          Diagnostics: HostConfigDiagnostic list }

    /// Default HTTP control endpoint for local development.
    let defaultControlEndpoint =
        { Protocol = "http"
          BindAddress = "127.0.0.1"
          Port = Some 8788
          AdvertiseUri = "http://127.0.0.1:8788"
          AllowLoopbackOnly = true }

    /// Default product web shell settings. The shell is disabled until explicitly requested.
    let defaultWebShell =
        { Profile = "control-only"
          BindAddress = "127.0.0.1"
          Port = Some 8897
          AdvertiseUri = "http://127.0.0.1:8897"
          AllowLoopbackOnly = true
          ActorFabric = "auto-local"
          PcslRoot = Some ".codex.fs/ptcs-webshell/pcsl" }

    /// Default API documentation settings.
    let defaultApiDocs =
        { GenerateXmlDocs = true
          GenerateOpenApi = true
          ExposeSwaggerUi = false
          SwaggerRoutePrefix = Some "swagger"
          IncludeExamples = true }

    /// Default PTCS settings for the host.
    let defaultPtcs =
        { FabricMode = "package-owned"
          SessionParticipantPrefix = "agent.codexfs"
          ReplyParticipantId = None
          DurableAgentTasks = false
          DefaultInboxLimit = 20 }

    /// Default host configuration used when no setting overrides are supplied.
    let defaults =
        { ArtifactRoot = ".codex.fs/artifacts"
          DefaultEngine = Agy
          EnabledEngines = [ Codex; Agy ]
          EngineExecutableOverrides = Map.empty
          DefaultTimeout = TimeSpan.FromMinutes 20.0
          MaxPendingMessagesPerTurn = 20
          Compaction = Compaction.defaultPolicy
          Redaction = { EnableHighRiskRules = true }
          ControlEndpoint = defaultControlEndpoint
          WebShell = defaultWebShell
          ApiDocs = defaultApiDocs
          Ptcs = defaultPtcs }

    /// Stable keys accepted by `loadFromMap`.
    let knownSettingKeys =
        [ "artifact.root"
          "engine.default"
          "engine.enabled"
          "engine.codex.executable"
          "engine.agy.executable"
          "timeout.default"
          "message.maxpendingperturn"
          "control.protocol"
          "control.bindaddress"
          "control.port"
          "control.advertiseuri"
          "control.allowloopbackonly"
          "web.profile"
          "web.bindaddress"
          "web.port"
          "web.advertiseuri"
          "web.allowloopbackonly"
          "web.actorfabric"
          "web.pcslroot"
          "apidocs.generatexmldocs"
          "apidocs.generateopenapi"
          "apidocs.exposeswaggerui"
          "apidocs.swaggerrouteprefix"
          "apidocs.includeexamples"
          "ptcs.fabricmode"
          "ptcs.sessionparticipantprefix"
          "ptcs.replyparticipantid"
          "ptcs.durableagenttasks"
          "ptcs.defaultinboxlimit"
          "compaction.maxsummarychars"
          "compaction.recententrycount"
          "compaction.maxentrytextchars"
          "redaction.enablehighriskrules" ]
        |> Set.ofList

    /// Normalize a configuration key for case-insensitive lookup.
    let normalizeKey (key: string) =
        if isNull key then
            String.Empty
        else
            key.Trim().ToLowerInvariant()

    /// Normalize a settings map for `loadFromMap`.
    let normalizeSettings (settings: Map<string, string>) =
        settings
        |> Map.toList
        |> List.map (fun (key, value) -> normalizeKey key, value)
        |> Map.ofList

    /// Parse an engine kind from config text.
    let parseEngineKind (text: string) =
        match text.Trim().ToLowerInvariant() with
        | "codex" -> Ok Codex
        | "agy" -> Ok Agy
        | value when value.StartsWith("custom:", StringComparison.Ordinal) && value.Length > "custom:".Length ->
            Ok(Custom(value.Substring("custom:".Length)))
        | value -> Error $"Unsupported engine kind: {value}"

    /// Render an engine kind for diagnostics.
    let formatEngineKind engine =
        match engine with
        | Codex -> "codex"
        | Agy -> "agy"
        | Custom name -> $"custom:{name}"

    /// Parse comma/semicolon separated engine kinds.
    let parseEngineList (text: string) =
        let values =
            text.Split([| ','; ';' |], StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries)
            |> Array.toList

        let folder (engines, errors) value =
            match parseEngineKind value with
            | Ok engine -> engine :: engines, errors
            | Error message -> engines, message :: errors

        let engines, errors = values |> List.fold folder ([], [])

        if errors.IsEmpty && not engines.IsEmpty then
            Ok(List.rev engines)
        elif errors.IsEmpty then
            Error "Engine list must contain at least one engine."
        else
            Error(String.concat "; " (List.rev errors))

    /// Parse a boolean setting.
    let parseBool (text: string) =
        match text.Trim().ToLowerInvariant() with
        | "true"
        | "1"
        | "yes"
        | "y" -> Ok true
        | "false"
        | "0"
        | "no"
        | "n" -> Ok false
        | value -> Error $"Unsupported boolean value: {value}"

    /// Parse a positive integer setting.
    let parseInt (text: string) =
        match Int32.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture) with
        | true, value -> Ok value
        | false, _ -> Error $"Unsupported integer value: {text}"

    /// Parse a `TimeSpan` setting using invariant culture.
    let parseTimeSpan (text: string) =
        match TimeSpan.TryParse(text.Trim(), CultureInfo.InvariantCulture) with
        | true, value -> Ok value
        | false, _ -> Error $"Unsupported timespan value: {text}"

    /// Convert an optional blank string to `None`.
    let optionalNonBlank text =
        if String.IsNullOrWhiteSpace text then None else Some text

    /// Return true when a host or bind address is loopback-only.
    let isLoopbackAddress (address: string) =
        let value = if isNull address then String.Empty else address.Trim()

        value.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || value.Equals("::1", StringComparison.Ordinal)
        || value.StartsWith("127.", StringComparison.Ordinal)

    /// Return true when a URI parses and its host is loopback-only.
    let tryLoopbackUri (uriText: string) =
        match Uri.TryCreate(uriText, UriKind.Absolute) with
        | true, uri -> Ok(isLoopbackAddress uri.Host)
        | false, _ -> Error $"Invalid advertise URI: {uriText}"

    /// Redact one setting value for diagnostics.
    let redactDiagnosticValue value =
        let result = Redaction.redactHighRisk value
        result.Text, not result.Hits.IsEmpty

    /// Build redacted diagnostics for supplied raw settings.
    let diagnosticsFromSettings settings =
        settings
        |> normalizeSettings
        |> Map.toList
        |> List.map (fun (key, value) ->
            let redacted, wasRedacted = redactDiagnosticValue value

            { Key = key
              Value = redacted
              WasRedacted = wasRedacted })

    /// Add one issue to an issue list.
    let addIssue key severity message issues =
        { Key = key
          Severity = severity
          Message = message }
        :: issues

    /// Apply one optional setting parser.
    let applySetting key parser applyValue settings current issues =
        match settings |> Map.tryFind key with
        | Some rawValue ->
            match parser rawValue with
            | Ok value -> applyValue current value, issues
            | Error message -> current, addIssue key IssueError message issues
        | None -> current, issues

    /// Apply one optional string setting.
    let applyStringSetting key applyValue settings current issues =
        applySetting key Ok applyValue settings current issues

    /// Build engine executable overrides from known engine keys.
    let engineExecutableOverrides settings =
        [ Codex, "engine.codex.executable"
          Agy, "engine.agy.executable" ]
        |> List.choose (fun (engine, key) ->
            settings
            |> Map.tryFind key
            |> Option.bind optionalNonBlank
            |> Option.map (fun value -> engine, value))
        |> Map.ofList

    /// Validate an effective host configuration.
    let validate config =
        let issues = []

        let issues =
            if String.IsNullOrWhiteSpace config.ArtifactRoot then
                addIssue "artifact.root" IssueError "Artifact root must not be blank." issues
            else
                issues

        let issues =
            if config.EnabledEngines |> List.contains config.DefaultEngine then
                issues
            else
                addIssue "engine.default" IssueError "Default engine must be included in enabled engines." issues

        let issues =
            if config.DefaultTimeout > TimeSpan.Zero then
                issues
            else
                addIssue "timeout.default" IssueError "Default timeout must be greater than zero." issues

        let issues =
            if config.MaxPendingMessagesPerTurn > 0 && config.MaxPendingMessagesPerTurn <= 1000 then
                issues
            else
                addIssue "message.maxpendingperturn" IssueError "Max pending messages per turn must be between 1 and 1000." issues

        let issues =
            if config.ControlEndpoint.Protocol.Equals("http", StringComparison.OrdinalIgnoreCase) then
                issues
            else
                addIssue "control.protocol" IssueError "MVP host control endpoint only supports http." issues

        let issues =
            match config.ControlEndpoint.Port with
            | Some port when port < 1 || port > 65535 -> addIssue "control.port" IssueError "Control endpoint port must be between 1 and 65535." issues
            | _ -> issues

        let issues =
            if String.IsNullOrWhiteSpace config.ControlEndpoint.BindAddress then
                addIssue "control.bindaddress" IssueError "Control endpoint bind address must not be blank." issues
            else
                issues

        let issues =
            match tryLoopbackUri config.ControlEndpoint.AdvertiseUri with
            | Ok advertiseLoopback ->
                if config.ControlEndpoint.AllowLoopbackOnly then
                    issues
                elif isLoopbackAddress config.ControlEndpoint.BindAddress || advertiseLoopback then
                    addIssue "control.advertiseuri" IssueError "Cluster/production host config must advertise and bind a routable non-loopback address." issues
                else
                    issues
            | Error message -> addIssue "control.advertiseuri" IssueError message issues

        let webProfile = config.WebShell.Profile.Trim().ToLowerInvariant()

        let issues =
            match webProfile with
            | "control-only"
            | "ptcs-webshell" -> issues
            | _ -> addIssue "web.profile" IssueError "Web profile must be control-only or ptcs-webshell." issues

        let issues =
            match config.WebShell.Port with
            | Some port when port < 1 || port > 65535 -> addIssue "web.port" IssueError "Web shell port must be between 1 and 65535." issues
            | _ -> issues

        let issues =
            if String.IsNullOrWhiteSpace config.WebShell.BindAddress then
                addIssue "web.bindaddress" IssueError "Web shell bind address must not be blank." issues
            else
                issues

        let issues =
            match tryLoopbackUri config.WebShell.AdvertiseUri with
            | Ok advertiseLoopback ->
                if config.WebShell.AllowLoopbackOnly then
                    issues
                elif isLoopbackAddress config.WebShell.BindAddress || advertiseLoopback then
                    addIssue "web.advertiseuri" IssueError "PTCS web shell must advertise and bind a routable non-loopback address unless web.allowLoopbackOnly=true." issues
                else
                    issues
            | Error message -> addIssue "web.advertiseuri" IssueError message issues

        let webActorFabric = config.WebShell.ActorFabric.Trim().ToLowerInvariant()

        let issues =
            match webActorFabric with
            | "auto-local"
            | "disabled" -> issues
            | _ -> addIssue "web.actorfabric" IssueError "Web shell actor fabric must be auto-local or disabled." issues

        let issues =
            match config.WebShell.PcslRoot with
            | Some value when String.IsNullOrWhiteSpace value -> addIssue "web.pcslroot" IssueError "Web shell PCSL root must not be blank when supplied." issues
            | _ -> issues

        let issues =
            if String.IsNullOrWhiteSpace config.Ptcs.FabricMode then
                addIssue "ptcs.fabricmode" IssueError "PTCS fabric mode must not be blank." issues
            else
                issues

        let issues =
            if String.IsNullOrWhiteSpace config.Ptcs.SessionParticipantPrefix then
                addIssue "ptcs.sessionparticipantprefix" IssueError "PTCS session participant prefix must not be blank." issues
            else
                issues

        let issues =
            if config.Ptcs.DefaultInboxLimit > 0 && config.Ptcs.DefaultInboxLimit <= 1000 then
                issues
            else
                addIssue "ptcs.defaultinboxlimit" IssueError "PTCS default inbox limit must be between 1 and 1000." issues

        List.rev issues

    /// Load host configuration from normalized string settings.
    let loadFromMap (settings: Map<string, string>) =
        let normalized = normalizeSettings settings
        let diagnostics = diagnosticsFromSettings settings

        let unknownIssues =
            normalized
            |> Map.toList
            |> List.choose (fun (key, _) ->
                if knownSettingKeys |> Set.contains key then
                    None
                else
                    Some
                        { Key = key
                          Severity = IssueWarning
                          Message = $"Unknown host config setting: {key}" })

        let control0 = defaults.ControlEndpoint

        let control1, issues0 =
            (control0, [])
            ||> applyStringSetting "control.protocol" (fun current value -> { current with Protocol = value })
                    normalized

        let control2, issues1 =
            (control1, issues0)
            ||> applyStringSetting "control.bindaddress" (fun current value -> { current with BindAddress = value })
                    normalized

        let control3, issues2 =
            (control2, issues1)
            ||> applySetting "control.port" parseInt (fun current value -> { current with Port = Some value }) normalized

        let control4, issues3 =
            (control3, issues2)
            ||> applyStringSetting "control.advertiseuri" (fun current value -> { current with AdvertiseUri = value })
                    normalized

        let control, issues4 =
            (control4, issues3)
            ||> applySetting "control.allowloopbackonly" parseBool (fun current value -> { current with AllowLoopbackOnly = value }) normalized

        let api0 = defaults.ApiDocs

        let web0 = defaults.WebShell

        let web1, issues5 =
            (web0, issues4)
            ||> applyStringSetting "web.profile" (fun current value -> { current with Profile = value })
                    normalized

        let web2, issues6 =
            (web1, issues5)
            ||> applyStringSetting "web.bindaddress" (fun current value -> { current with BindAddress = value })
                    normalized

        let web3, issues7 =
            (web2, issues6)
            ||> applySetting "web.port" parseInt (fun current value -> { current with Port = Some value }) normalized

        let web4, issues8 =
            (web3, issues7)
            ||> applyStringSetting "web.advertiseuri" (fun current value -> { current with AdvertiseUri = value })
                    normalized

        let web5, issues9 =
            (web4, issues8)
            ||> applySetting "web.allowloopbackonly" parseBool (fun current value -> { current with AllowLoopbackOnly = value }) normalized

        let web6, issues10 =
            (web5, issues9)
            ||> applyStringSetting "web.actorfabric" (fun current value -> { current with ActorFabric = value })
                    normalized

        let web, issues10b =
            (web6, issues10)
            ||> applyStringSetting "web.pcslroot" (fun current value -> { current with PcslRoot = optionalNonBlank value })
                    normalized

        let api1, issues11 =
            (api0, issues10b)
            ||> applySetting "apidocs.generatexmldocs" parseBool (fun current value -> { current with GenerateXmlDocs = value }) normalized

        let api2, issues12 =
            (api1, issues11)
            ||> applySetting "apidocs.generateopenapi" parseBool (fun current value -> { current with GenerateOpenApi = value }) normalized

        let api3, issues13 =
            (api2, issues12)
            ||> applySetting "apidocs.exposeswaggerui" parseBool (fun current value -> { current with ExposeSwaggerUi = value }) normalized

        let api4, issues14 =
            (api3, issues13)
            ||> applyStringSetting "apidocs.swaggerrouteprefix" (fun current value -> { current with SwaggerRoutePrefix = optionalNonBlank value })
                    normalized

        let apiDocs, issues15 =
            (api4, issues14)
            ||> applySetting "apidocs.includeexamples" parseBool (fun current value -> { current with IncludeExamples = value }) normalized

        let ptcs0 = defaults.Ptcs

        let ptcs1, issues16 =
            (ptcs0, issues15)
            ||> applyStringSetting "ptcs.fabricmode" (fun current value -> { current with FabricMode = value }) normalized

        let ptcs2, issues17 =
            (ptcs1, issues16)
            ||> applyStringSetting
                    "ptcs.sessionparticipantprefix"
                    (fun current value -> { current with SessionParticipantPrefix = value })
                    normalized

        let ptcs3, issues18 =
            (ptcs2, issues17)
            ||> applyStringSetting "ptcs.replyparticipantid" (fun current value -> { current with ReplyParticipantId = optionalNonBlank value })
                    normalized

        let ptcs4, issues19 =
            (ptcs3, issues18)
            ||> applySetting "ptcs.durableagenttasks" parseBool (fun current value -> { current with DurableAgentTasks = value }) normalized

        let ptcs, issues20 =
            (ptcs4, issues19)
            ||> applySetting "ptcs.defaultinboxlimit" parseInt (fun current value -> { current with DefaultInboxLimit = value }) normalized

        let compaction0 = defaults.Compaction

        let compaction1, issues21 =
            (compaction0, issues20)
            ||> applySetting "compaction.maxsummarychars" parseInt (fun current value -> { current with MaxSummaryChars = Some value }) normalized

        let compaction2, issues22 =
            (compaction1, issues21)
            ||> applySetting "compaction.recententrycount" parseInt (fun current value -> { current with RecentEntryCount = value }) normalized

        let compaction, issues23 =
            (compaction2, issues22)
            ||> applySetting "compaction.maxentrytextchars" parseInt (fun current value -> { current with MaxEntryTextChars = Some value }) normalized

        let redaction, issues24 =
            ({ EnableHighRiskRules = defaults.Redaction.EnableHighRiskRules }, issues23)
            ||> applySetting
                    "redaction.enablehighriskrules"
                    parseBool
                    (fun current value -> { current with EnableHighRiskRules = value })
                    normalized

        let config0 =
            { defaults with
                ControlEndpoint = control
                WebShell = web
                ApiDocs = apiDocs
                Ptcs = ptcs
                Compaction = compaction
                Redaction = redaction
                EngineExecutableOverrides = engineExecutableOverrides normalized }

        let config1, issues25 =
            (config0, issues24)
            ||> applyStringSetting "artifact.root" (fun current value -> { current with ArtifactRoot = value }) normalized

        let config2, issues26 =
            (config1, issues25)
            ||> applySetting "engine.default" parseEngineKind (fun current value -> { current with DefaultEngine = value }) normalized

        let config3, issues27 =
            (config2, issues26)
            ||> applySetting "engine.enabled" parseEngineList (fun current value -> { current with EnabledEngines = value }) normalized

        let config4, issues28 =
            (config3, issues27)
            ||> applySetting "timeout.default" parseTimeSpan (fun current value -> { current with DefaultTimeout = value }) normalized

        let config, issues29 =
            (config4, issues28)
            ||> applySetting "message.maxpendingperturn" parseInt (fun current value -> { current with MaxPendingMessagesPerTurn = value }) normalized

        let issues = unknownIssues @ (List.rev issues29) @ validate config
        let fatal = issues |> List.exists (fun issue -> issue.Severity = IssueError)

        { Config = if fatal then None else Some config
          Issues = issues
          Diagnostics = diagnostics }
