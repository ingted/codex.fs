namespace CodexFs.Cli

open System
open Argu

/// Compiled FAkka.Argu command surface for codex.fs.cli.
module Cli =

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
        /// Existing session id.
        | Session of sessionId: string
        /// Prompt text or @file reference.
        | Prompt of textOrFile: string
        /// Advertised codex.fs.host control URI.
        | Host of uri: string

        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Session _ -> "Existing session id."
                | Prompt _ -> "Prompt text or @file reference."
                | Host _ -> "Advertised codex.fs.host control URI."

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

    /// Top-level codex.fs.cli command groups.
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

    /// Create the compiled CLI parser.
    let argumentParser () =
        ArgumentParser.Create<CliArgument>(programName = "codex.fs.cli")

    /// Usage examples shown after the generated Argu help text.
    let examples =
        [ "codex.fs.cli host status --host http://192.168.10.20:8788"
          "codex.fs.cli session create --engine agy --host http://192.168.10.20:8788"
          "codex.fs.cli session send --session sess-001 --prompt @prompt.md --host http://192.168.10.20:8788"
          "codex.fs.cli session status --session sess-001 --host http://192.168.10.20:8788"
          "codex.fs.cli run status --run run-001 --host http://192.168.10.20:8788"
          "codex.fs.cli engine probe --engine agy --executable agy" ]

    /// Render generated help plus stable examples.
    let helpText () =
        let parser = argumentParser ()
        parser.PrintUsage() + Environment.NewLine + "Examples:" + Environment.NewLine + String.concat Environment.NewLine examples

    /// Parse argv with Argu and return a non-throwing result for tests and program entrypoint.
    let tryParse argv =
        try
            argumentParser().ParseCommandLine(argv) |> ignore
            Ok()
        with :? ArguParseException as ex ->
            Error ex.Message

    /// Parsed options for `session send`.
    type SessionSendOptions =
        { /// Advertised codex.fs.host control URI.
          Host: string
          /// Target session id.
          SessionId: string
          /// Prompt text or @file reference.
          Prompt: string }

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
    let tryParseSessionSend argv =
        try
            let parsed = argumentParser().ParseCommandLine(argv)

            match parsed.TryGetResult CliArgument.Session with
            | Some sessionCommands ->
                match sessionCommands.TryGetResult SessionCommand.Send with
                | Some sendArgs ->
                    match requireArg "--host" (sendArgs.TryGetResult SessionSendArgument.Host),
                          requireArg "--session" (sendArgs.TryGetResult SessionSendArgument.Session),
                          requireArg "--prompt" (sendArgs.TryGetResult SessionSendArgument.Prompt) with
                    | Ok host, Ok sessionId, Ok prompt ->
                        Ok(Some { Host = host; SessionId = sessionId; Prompt = prompt })
                    | Error message, _, _
                    | _, Error message, _
                    | _, _, Error message -> Error message
                | None -> Ok None
            | None -> Ok None
        with :? ArguParseException as ex ->
            Error ex.Message

    let sessionTargetOptions (targetArgs: ParseResults<SessionTargetArgument>) =
        match requireArg "--host" (targetArgs.TryGetResult SessionTargetArgument.Host),
              requireArg "--session" (targetArgs.TryGetResult SessionTargetArgument.Session) with
        | Ok host, Ok sessionId -> Ok { Host = host; SessionId = sessionId }
        | Error message, _
        | _, Error message -> Error message

    /// Try to extract session status/attach/drain options from argv. Other valid commands return `Ok None`.
    let tryParseSessionRead argv =
        try
            let parsed = argumentParser().ParseCommandLine(argv)

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
