namespace CodexFs.HostTool

open System
open System.Threading
open System.Threading.Tasks
open Argu
open CodexFs
open CodexFs.Host

/// Standalone dotnet tool command surface for codex.fs.host.
module HostTool =

    /// Shared arguments for host status/start commands.
    type HostToolArgument =
        /// Host config override in `key=value` format.
        | Setting of keyValue: string
        /// Bounded run duration for `start`; omit to run until Ctrl+C.
        | Run_Seconds of seconds: int

        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Setting _ -> "Host config override in key=value format."
                | Run_Seconds _ -> "Bounded run duration in seconds; omit to run until Ctrl+C."

    /// Top-level host tool commands.
    [<CliPrefix(CliPrefix.None)>]
    type HostToolCommand =
        /// Print local host config/runtime health without opening a listener.
        | Status of ParseResults<HostToolArgument>
        /// Start the HTTP control host using the supplied config.
        | Start of ParseResults<HostToolArgument>

        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Status _ -> "Print local host config/runtime health."
                | Start _ -> "Start codex.fs.host HTTP control endpoint."

    /// Parsed options shared by host tool commands.
    type HostToolOptions =
        { /// HostConfig.loadFromMap settings.
          Settings: Map<string, string>
          /// Optional bounded run duration for tests/automation.
          RunSeconds: int option }

    /// Parsed host tool action.
    type HostToolAction =
        /// Print effective local host health.
        | HostStatus of HostToolOptions
        /// Start the HTTP control host.
        | HostStart of HostToolOptions

    /// Create the compiled host tool parser.
    let argumentParser () =
        ArgumentParser.Create<HostToolCommand>(programName = "codex.fs.host")

    /// Examples shown after generated Argu help.
    let examples =
        [ "codex.fs.host status --setting control.advertiseUri=http://192.168.10.20:8788"
          "codex.fs.host start --setting control.bindAddress=192.168.10.20 --setting control.port=8788 --setting control.advertiseUri=http://192.168.10.20:8788"
          "codex.fs.host start --setting web.profile=ptcs-webshell --setting web.bindAddress=192.168.10.20 --setting web.port=8897 --setting web.advertiseUri=http://192.168.10.20:8897 --setting web.allowLoopbackOnly=false"
          "codex.fs.host start --run-seconds 5 --setting control.bindAddress=192.168.10.20 --setting control.port=8788 --setting control.advertiseUri=http://192.168.10.20:8788" ]

    /// Render generated help plus stable examples.
    let helpText () =
        let parser = argumentParser ()
        parser.PrintUsage() + Environment.NewLine + "Examples:" + Environment.NewLine + String.concat Environment.NewLine examples

    /// Return true when argv asks for root help without entering command dispatch.
    let isRootHelp (argv: string array) =
        argv.Length = 0
        || (argv.Length = 1
            && (argv[0] = "help" || argv[0] = "--help" || argv[0] = "-h" || argv[0] = "/?"))

    /// Parse a `key=value` host config override.
    let parseSetting (text: string) =
        if String.IsNullOrWhiteSpace text then
            Error "Host config setting must not be blank."
        else
            let index = text.IndexOf('=', StringComparison.Ordinal)

            if index <= 0 then
                Error $"Host config setting must use key=value format: {text}"
            else
                let key = text.Substring(0, index).Trim()
                let value = text.Substring(index + 1).Trim()

                if String.IsNullOrWhiteSpace key then
                    Error $"Host config setting key must not be blank: {text}"
                else
                    Ok(key, value)

    /// Convert Argu parse results into normalized host tool options.
    let optionsFromArguments (arguments: ParseResults<HostToolArgument>) =
        let settingResults =
            arguments.GetResults HostToolArgument.Setting
            |> List.map parseSetting

        let errors =
            settingResults
            |> List.choose (function
                | Ok _ -> None
                | Error message -> Some message)

        match errors with
        | message :: _ -> Error message
        | [] ->
            let settings =
                settingResults
                |> List.choose (function
                    | Ok pair -> Some pair
                    | Error _ -> None)
                |> Map.ofList

            match arguments.TryGetResult HostToolArgument.Run_Seconds with
            | Some seconds when seconds < 0 -> Error "--run-seconds must be zero or greater."
            | runSeconds -> Ok { Settings = settings; RunSeconds = runSeconds }

    /// Parse argv into a host tool action without throwing.
    let tryParseAction argv =
        if isRootHelp argv then
            Ok None
        else
            try
                let parsed = argumentParser().ParseCommandLine(argv)

                match parsed.TryGetResult HostToolCommand.Status, parsed.TryGetResult HostToolCommand.Start with
                | Some statusArgs, _ -> optionsFromArguments statusArgs |> Result.map (fun options -> Some(HostStatus options))
                | _, Some startArgs -> optionsFromArguments startArgs |> Result.map (fun options -> Some(HostStart options))
                | None, None -> Ok None
            with :? ArguParseException as ex ->
                Error ex.Message

    let issueSeverityText severity =
        match severity with
        | HostConfig.IssueWarning -> "warning"
        | HostConfig.IssueError -> "error"

    /// Format one non-secret host config issue.
    let formatIssue (issue: HostConfig.HostConfigIssue) =
        $"{issueSeverityText issue.Severity}:{issue.Key}:{issue.Message}"

    /// Format non-secret host config issues.
    let formatIssues issues =
        issues |> List.map formatIssue |> String.concat Environment.NewLine

    /// Load the host runtime from parsed tool settings.
    let loadRuntime options =
        let loadResult = HostConfig.loadFromMap options.Settings

        match HostRuntime.tryCreateFromLoadResult loadResult with
        | Ok runtime -> Ok runtime
        | Error issues -> Error(formatIssues issues)

    /// Render status for local config/runtime without opening a listener.
    let statusText options =
        loadRuntime options |> Result.map HostRuntime.healthSummary

    /// Render the text emitted after the HTTP control host starts.
    let startedText (server: HostControl.HostControlServer) =
        [ "status=running"
          $"bindUri={server.Contract.BindUri}"
          $"advertiseUri={server.Contract.AdvertiseUri}"
          $"healthUri={server.Contract.HealthUri}"
          $"openApiJsonUri={server.Contract.OpenApiJsonUri}"
          $"swaggerUiUri={server.Contract.SwaggerUiUri}"
          HostRuntime.healthSummary server.Runtime ]
        |> String.concat Environment.NewLine

    /// Render the text emitted after the PTCS product web shell starts.
    let webShellStartedText (server: HostWebShell.HostWebShellServer) =
        let scriptUrlsText = String.concat "," server.Contract.ScriptUrls

        [ "status=running"
          "profile=ptcs-webshell"
          $"bindUri={server.Contract.BindUri}"
          $"advertiseUri={server.Contract.AdvertiseUri}"
          $"chatUri={server.Contract.ChatUri}"
          $"healthUri={server.Contract.HealthUri}"
          $"extensionId={server.Contract.ExtensionId}"
          $"scriptUrls={scriptUrlsText}"
          HostRuntime.healthSummary server.Runtime ]
        |> String.concat Environment.NewLine

    let stopServerTextAsync server =
        task {
            try
                let! stoppedRuntime = HostControl.stopAsync CancellationToken.None server
                return $"status={HostRuntime.formatStatus stoppedRuntime.Status}"
            with ex ->
                let redacted = Redaction.redactHighRisk ex.Message
                return $"status=stop-failed{Environment.NewLine}error={redacted.Text}"
        }

    let stopWebShellTextAsync server =
        task {
            try
                let! stoppedRuntime = HostWebShell.stopAsync CancellationToken.None server
                return $"status={HostRuntime.formatStatus stoppedRuntime.Status}"
            with ex ->
                let redacted = Redaction.redactHighRisk ex.Message
                return $"status=stop-failed{Environment.NewLine}error={redacted.Text}"
        }

    let waitForRunDurationAsync runSeconds (cancellationToken: CancellationToken) =
        task {
            match runSeconds with
            | Some seconds when seconds <= 0 -> return ()
            | Some seconds -> do! Task.Delay(TimeSpan.FromSeconds(float seconds), cancellationToken)
            | None -> do! Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
        }

    /// Start the HTTP control host, wait for the bounded duration or cancellation, and stop it.
    let startTextAsync startedUtc options (cancellationToken: CancellationToken) =
        task {
            match loadRuntime options with
            | Error message -> return Error message
            | Ok runtime ->
                if HostWebShell.isEnabled runtime.Config then
                    match! HostWebShell.tryStartAsync startedUtc cancellationToken runtime with
                    | Error issues -> return Error(formatIssues issues)
                    | Ok server ->
                        let runningText = webShellStartedText server

                        try
                            do! waitForRunDurationAsync options.RunSeconds cancellationToken
                            let! stoppedText = stopWebShellTextAsync server
                            return Ok(runningText + Environment.NewLine + stoppedText)
                        with
                        | :? OperationCanceledException ->
                            let! stoppedText = stopWebShellTextAsync server
                            return Ok(runningText + Environment.NewLine + stoppedText)
                        | ex ->
                            let! stoppedText = stopWebShellTextAsync server
                            let redacted = Redaction.redactHighRisk ex.Message
                            return Error(runningText + Environment.NewLine + stoppedText + Environment.NewLine + $"error={redacted.Text}")
                else
                    match! HostControl.tryStartAsync startedUtc cancellationToken runtime with
                    | Error issues -> return Error(formatIssues issues)
                    | Ok server ->
                        let runningText = startedText server

                        try
                            do! waitForRunDurationAsync options.RunSeconds cancellationToken
                            let! stoppedText = stopServerTextAsync server
                            return Ok(runningText + Environment.NewLine + stoppedText)
                        with
                        | :? OperationCanceledException ->
                            let! stoppedText = stopServerTextAsync server
                            return Ok(runningText + Environment.NewLine + stoppedText)
                        | ex ->
                            let! stoppedText = stopServerTextAsync server
                            let redacted = Redaction.redactHighRisk ex.Message
                            return Error(runningText + Environment.NewLine + stoppedText + Environment.NewLine + $"error={redacted.Text}")
        }

    let printResult successExit errorExit result =
        match result with
        | Ok text ->
            printfn "%s" text
            successExit
        | Error message ->
            eprintfn "%s" message
            errorExit

    /// Entry implementation shared by tests and the compiled dotnet tool.
    let runMain argv =
        if isRootHelp argv then
            printfn "%s" (helpText ())
            0
        else
            match tryParseAction argv with
            | Error message ->
                eprintfn "%s" message
                2
            | Ok None ->
                printfn "%s" (helpText ())
                0
            | Ok(Some(HostStatus options)) -> statusText options |> printResult 0 1
            | Ok(Some(HostStart options)) ->
                use cts = new CancellationTokenSource()

                Console.CancelKeyPress.Add(fun args ->
                    args.Cancel <- true
                    cts.Cancel())

                startTextAsync DateTimeOffset.UtcNow options cts.Token
                |> fun task -> task.GetAwaiter().GetResult()
                |> printResult 0 1

module Program =

    /// Entry point for the compiled codex.fs.host dotnet tool wrapper.
    [<EntryPoint>]
    let main (argv: string array) = HostTool.runMain argv
