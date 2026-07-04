namespace CodexFs.Cli

open System
open Argu

/// Compiled FAkka.Argu command surface for the `codex.fs.cli` terminal command and `codex.fs` alias.
module Cli =

    /// Canonical explicit CLI command name installed by package `codex.fs.cli`.
    let CanonicalProgramName = "codex.fs.cli"

    /// Short convenience command name installed by package `codex.fs.tool`.
    let ShortProgramName = "codex.fs"

    /// Common host endpoint option shared by control commands.
    type HostOption =
        { /// Advertised codex.fs.host control URI.
          Host: string option }

    /// Arguments for `session create`.
    type SessionCreateArgument =
        /// Engine family for the new session.
        | Engine of engine: string
        /// Advertised codex.fs.host control URI.
        | Host of uri: string

        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Engine _ -> "Engine family for the new session: codex or agy."
                | Host _ -> "Advertised codex.fs.host control URI."

    /// Arguments for `session send`.
    type SessionSendArgument =
        /// Optional existing session id; omitted sends to the default Foreman/SessionWorker.
        | Session of sessionId: string
        /// Prompt text or @file reference.
        | Prompt of textOrFile: string
        /// Advertised codex.fs.host control URI.
        | Host of uri: string
        /// Optional target worker participant id; default is the session worker/foreman.
        | Worker_Id of workerId: string

        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Session _ -> "Optional existing session id; omitted sends to the default Foreman/SessionWorker."
                | Prompt _ -> "Prompt text or @file reference."
                | Host _ -> "Advertised codex.fs.host control URI."
                | Worker_Id _ -> "Optional target worker participant id; default is the session worker/foreman."

    /// Arguments for `session attach` and `session drain`.
    type SessionTargetArgument =
        /// Existing session id.
        | Session of sessionId: string
        /// Advertised codex.fs.host control URI.
        | Host of uri: string

        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Session _ -> "Existing session id."
                | Host _ -> "Advertised codex.fs.host control URI."

    /// Subcommands under `session`.
    [<CliPrefix(CliPrefix.None)>]
    type SessionCommand =
        /// Create a session.
        | Create of ParseResults<SessionCreateArgument>
        /// Send a prompt/message to a session.
        | Send of ParseResults<SessionSendArgument>
        /// Show session inbox status.
        | Status of ParseResults<SessionTargetArgument>
        /// Attach to a session stream.
        | Attach of ParseResults<SessionTargetArgument>
        /// Drain currently available session messages.
        | Drain of ParseResults<SessionTargetArgument>

        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Create _ -> "Create a session."
                | Send _ -> "Send a prompt/message to a session."
                | Status _ -> "Show session inbox status."
                | Attach _ -> "Attach to a session stream."
                | Drain _ -> "Drain currently available session messages."

    /// Arguments for `run status` and `run artifacts`.
    type RunTargetArgument =
        /// Existing run id.
        | Run of runId: string
        /// Advertised codex.fs.host control URI.
        | Host of uri: string

        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Run _ -> "Existing run id."
                | Host _ -> "Advertised codex.fs.host control URI."

    /// Subcommands under `run`.
    [<CliPrefix(CliPrefix.None)>]
    type RunCommand =
        /// Show run status.
        | Status of ParseResults<RunTargetArgument>
        /// Show run artifact manifest references.
        | Artifacts of ParseResults<RunTargetArgument>

        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Status _ -> "Show run status."
                | Artifacts _ -> "Show run artifact manifest references."

    /// Arguments for `host status`.
    type HostStatusArgument =
        /// Advertised codex.fs.host control URI.
        | Host of uri: string

        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Host _ -> "Advertised codex.fs.host control URI."

    /// Subcommands under `host`.
    [<CliPrefix(CliPrefix.None)>]
    type HostCommand =
        /// Show host health/status.
        | Status of ParseResults<HostStatusArgument>

        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Status _ -> "Show host health/status."

    /// Arguments for `engine probe`.
    type EngineProbeArgument =
        /// Engine family to probe.
        | Engine of engine: string
        /// Executable path or command to probe.
        | Executable of pathOrCommand: string

        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Engine _ -> "Engine family to probe: codex or agy."
                | Executable _ -> "Executable path or command to probe."

    /// Subcommands under `engine`.
    [<CliPrefix(CliPrefix.None)>]
    type EngineCommand =
        /// Probe an installed engine executable.
        | Probe of ParseResults<EngineProbeArgument>

        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Probe _ -> "Probe an installed engine executable."

    /// Top-level CLI command groups.
    [<CliPrefix(CliPrefix.None)>]
    type CliArgument =
        /// Session commands.
        | Session of ParseResults<SessionCommand>
        /// Run commands.
        | Run of ParseResults<RunCommand>
        /// Host commands.
        | Host of ParseResults<HostCommand>
        /// Engine commands.
        | Engine of ParseResults<EngineCommand>

        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Session _ -> "Session commands."
                | Run _ -> "Run commands."
                | Host _ -> "Host commands."
                | Engine _ -> "Engine commands."

    /// Create the compiled CLI parser for the selected command name.
    let argumentParserFor programName =
        ArgumentParser.Create<CliArgument>(programName = programName)

    /// Create the compiled CLI parser for the canonical explicit command.
    let argumentParser () =
        argumentParserFor CanonicalProgramName

    /// Usage examples shown after the generated Argu help text.
    let examplesFor programName =
        [ $"{programName} host status --host http://192.168.10.20:8788"
          $"{programName} session create --engine agy --host http://192.168.10.20:8788"
          $"{programName} session send --prompt @prompt.md --host http://192.168.10.20:8788"
          $"{programName} session send --session sess-001 --prompt @prompt.md --host http://192.168.10.20:8788"
          $"{programName} session send --session sess-001 --worker-id agent.codexfs.worker-001 --prompt @prompt.md --host http://192.168.10.20:8788"
          $"{programName} session status --session sess-001 --host http://192.168.10.20:8788"
          $"{programName} run status --run run-001 --host http://192.168.10.20:8788"
          $"{programName} engine probe --engine agy --executable agy" ]

    /// Usage examples shown after the generated Argu help text for the canonical explicit command.
    let examples =
        examplesFor CanonicalProgramName

    /// Render generated help plus stable examples.
    let helpTextFor programName =
        let parser = argumentParserFor programName
        parser.PrintUsage() + Environment.NewLine + "Examples:" + Environment.NewLine + String.concat Environment.NewLine (examplesFor programName)

    /// Render generated help for the canonical explicit command.
    let helpText () =
        helpTextFor CanonicalProgramName

    /// Parse argv with Argu and return a non-throwing result for tests and program entrypoint.
    let tryParseFor programName argv =
        try
            argumentParserFor programName |> fun parser -> parser.ParseCommandLine(argv) |> ignore
            Ok()
        with :? ArguParseException as ex ->
            Error ex.Message

    /// Parse argv with the canonical explicit command name.
    let tryParse argv =
        tryParseFor CanonicalProgramName argv

    /// Parsed options for `session send`.
    type SessionSendOptions =
        { /// Advertised codex.fs.host control URI.
          Host: string
          /// Optional target session id. None means the default Foreman/SessionWorker.
          SessionId: string option
          /// Optional target worker participant id.
          WorkerId: string option
          /// Prompt text or @file reference.
          Prompt: string }

    /// Parsed options for `host status`.
    type HostStatusOptions =
        { /// Advertised codex.fs.host control URI.
          Host: string }

    /// Parsed options for `session status`, `session attach`, and `session drain`.
    type SessionTargetOptions =
        { /// Advertised codex.fs.host control URI.
          Host: string
          /// Target session id.
          SessionId: string }

    /// Parsed bounded session read command.
    type SessionReadCommand =
        /// Show session status without acknowledging messages.
        | SessionStatus of SessionTargetOptions
        /// Bounded wait for session messages without acknowledging them.
        | SessionAttach of SessionTargetOptions
        /// Drain and acknowledge current session messages.
        | SessionDrain of SessionTargetOptions

    let requireArg name value =
        match value with
        | Some result when not (String.IsNullOrWhiteSpace result) -> Ok result
        | _ -> Error $"Missing required {name}."

    /// Try to extract `session send` options from argv. Other valid commands return `Ok None`.
    let tryParseSessionSendFor programName argv =
        try
            let parsed = argumentParserFor programName |> fun parser -> parser.ParseCommandLine(argv)

            match parsed.TryGetResult CliArgument.Session with
            | Some sessionCommands ->
                match sessionCommands.TryGetResult SessionCommand.Send with
                | Some sendArgs ->
                    match requireArg "--host" (sendArgs.TryGetResult SessionSendArgument.Host),
                          requireArg "--prompt" (sendArgs.TryGetResult SessionSendArgument.Prompt) with
                    | Ok host, Ok prompt ->
                        let sessionId =
                            sendArgs.TryGetResult SessionSendArgument.Session
                            |> Option.bind (fun value -> if String.IsNullOrWhiteSpace value then None else Some value)

                        let workerId =
                            sendArgs.TryGetResult SessionSendArgument.Worker_Id
                            |> Option.bind (fun value -> if String.IsNullOrWhiteSpace value then None else Some value)

                        Ok(Some { Host = host; SessionId = sessionId; WorkerId = workerId; Prompt = prompt })
                    | Error message, _
                    | _, Error message -> Error message
                | None -> Ok None
            | None -> Ok None
        with :? ArguParseException as ex ->
            Error ex.Message

    /// Try to extract `session send` options using the canonical explicit command name.
    let tryParseSessionSend argv =
        tryParseSessionSendFor CanonicalProgramName argv

    let sessionTargetOptions (targetArgs: ParseResults<SessionTargetArgument>) =
        match requireArg "--host" (targetArgs.TryGetResult SessionTargetArgument.Host),
              requireArg "--session" (targetArgs.TryGetResult SessionTargetArgument.Session) with
        | Ok host, Ok sessionId -> Ok { Host = host; SessionId = sessionId }
        | Error message, _
        | _, Error message -> Error message

    /// Try to extract session status/attach/drain options from argv. Other valid commands return `Ok None`.
    let tryParseSessionReadFor programName argv =
        try
            let parsed = argumentParserFor programName |> fun parser -> parser.ParseCommandLine(argv)

            match parsed.TryGetResult CliArgument.Session with
            | Some sessionCommands ->
                match sessionCommands.TryGetResult SessionCommand.Status,
                      sessionCommands.TryGetResult SessionCommand.Attach,
                      sessionCommands.TryGetResult SessionCommand.Drain with
                | Some statusArgs, _, _ ->
                    sessionTargetOptions statusArgs |> Result.map (fun options -> Some(SessionStatus options))
                | _, Some attachArgs, _ ->
                    sessionTargetOptions attachArgs |> Result.map (fun options -> Some(SessionAttach options))
                | _, _, Some drainArgs ->
                    sessionTargetOptions drainArgs |> Result.map (fun options -> Some(SessionDrain options))
                | None, None, None -> Ok None
            | None -> Ok None
        with :? ArguParseException as ex ->
            Error ex.Message

    /// Try to extract session read options using the canonical explicit command name.
    let tryParseSessionRead argv =
        tryParseSessionReadFor CanonicalProgramName argv

    /// Try to extract `host status` options from argv. Other valid commands return `Ok None`.
    let tryParseHostStatusFor programName argv =
        try
            let parsed = argumentParserFor programName |> fun parser -> parser.ParseCommandLine(argv)

            match parsed.TryGetResult CliArgument.Host with
            | Some hostCommands ->
                match hostCommands.TryGetResult HostCommand.Status with
                | Some hostArgs ->
                    requireArg "--host" (hostArgs.TryGetResult HostStatusArgument.Host)
                    |> Result.map (fun host -> Some { Host = host })
                | None -> Ok None
            | None -> Ok None
        with :? ArguParseException as ex ->
            Error ex.Message

    /// Try to extract `host status` options using the canonical explicit command name.
    let tryParseHostStatus argv =
        tryParseHostStatusFor CanonicalProgramName argv

    /// Resolve prompt input. A leading `@` means the rest of the value is a file path read by the caller.
    let tryResolvePromptText (readAllText: string -> string) (value: string) =
        if String.IsNullOrWhiteSpace value then
            Error "Prompt must not be blank."
        elif value.StartsWith("@", StringComparison.Ordinal) then
            let path = value.Substring(1)

            if String.IsNullOrWhiteSpace path then
                Error "Prompt file path after @ must not be blank."
            else
                try
                    let text = readAllText path

                    if String.IsNullOrWhiteSpace text then
                        Error $"Prompt file must not be blank: {path}"
                    else
                        Ok text
                with ex ->
                    Error $"Prompt file could not be read: {path}; {ex.GetType().Name}: {ex.Message}"
        else
            Ok value
