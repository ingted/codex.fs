namespace CodexFs

open System
open System.Text.RegularExpressions
open System.Threading
open System.Threading.Tasks
open Argu
open CodexFs.Artifacts
open CodexFs.Domain

/// Engine adapter contracts for CLI surface probing, argv rendering and artifact mapping.
module Engine =

    /// Probe result returned by an engine adapter.
    type EngineProbe =
        { /// Executable path that was probed.
          ExecutablePath: string
          /// Raw version text returned by the executable.
          VersionText: string
          /// Engine surfaces discovered for this executable.
          Surfaces: EngineSurface list }

    /// Rendered command produced by an engine adapter.
    type RenderedCommand =
        { /// Executable file name or absolute path.
          FileName: string
          /// Argument list passed to the process runner.
          Arguments: string list
          /// Working directory passed to the process runner.
          WorkingDirectory: string
          /// Environment variable overlay; `None` means remove the variable.
          Environment: Map<string, string option>
          /// Human-readable command display with sensitive values redacted.
          RedactedDisplay: string }

    /// Functional contract implemented by one engine adapter.
    type EngineAdapter =
        { /// Engine family handled by this adapter.
          Kind: EngineKind
          /// Probe the executable and discover supported surfaces.
          Probe: CancellationToken -> Task<EngineProbe>
          /// Return true when this adapter can handle the surface/request pair.
          CanHandle: EngineSurface -> RunRequest -> bool
          /// Render a normalized run request into process command data.
          Render: EngineSurface -> RunRequest -> RenderedCommand
          /// Map process output artifacts into the normalized run result/artifact layout.
          MapArtifacts: EngineSurface -> RunRequest -> RunResult -> Task<unit> }

    /// Agy CLI engine surfaces.
    module Agy =

        /// Agy CLI 1.0.x surfaces.
        module V1_0 =

            /// Probe helpers for the Agy CLI 1.0.x print/prompt surface.
            module Print =

                /// Stable surface id for Agy 1.0.x print mode.
                [<Literal>]
                let SurfaceId = "agy-print-1.0"

                /// Normalize raw version text captured from `agy --version`.
                let normalizeVersionText (versionText: string) =
                    if isNull versionText then
                        String.Empty
                    else
                        versionText.Trim()

                /// Return true when the version belongs to the supported Agy 1.0.x family.
                let isSupportedVersion (versionText: string) =
                    match Version.TryParse(normalizeVersionText versionText) with
                    | true, version -> version.Major = 1 && version.Minor = 0
                    | false, _ -> false

                /// Tokenize `agy --help` output for option discovery.
                let helpTokens (helpText: string) =
                    if String.IsNullOrWhiteSpace helpText then
                        Set.empty
                    else
                        helpText.Split([| ' '; '\t'; '\r'; '\n' |], StringSplitOptions.RemoveEmptyEntries)
                        |> Set.ofArray

                /// Return true when the help text exposes an option token.
                let hasOption optionName helpText =
                    helpTokens helpText |> Set.contains optionName

                /// Discover normalized engine capabilities from `agy --help` text.
                let discoverCapabilities helpText =
                    let tokens = helpTokens helpText
                    let has optionName = tokens |> Set.contains optionName

                    [ if has "--print" || has "--prompt" then
                          SingleTurnHeadless
                      if has "--continue" || has "--conversation" then
                          Continuation
                      if has "--add-dir" then
                          WorkspaceDirectories
                      if has "--sandbox" then
                          SandboxMode
                      if has "--model" then
                          ModelSelection
                      if has "--print-timeout" then
                          Timeout
                      if has "--log-file" then
                          LogFile ]
                    |> Set.ofList

                /// Try to create an Agy 1.0.x print surface from captured probe text.
                let trySurface versionText helpText =
                    let capabilities = discoverCapabilities helpText

                    if isSupportedVersion versionText && capabilities.Contains SingleTurnHeadless then
                        Some
                            { Kind = Agy
                              VersionText = normalizeVersionText versionText
                              SurfaceId = SurfaceId
                              Capabilities = capabilities }
                    else
                        None

                /// Build a normalized engine probe from captured `agy --version` and `agy --help` output.
                let probeFromText executablePath versionText helpText : EngineProbe =
                    { ExecutablePath = executablePath
                      VersionText = normalizeVersionText versionText
                      Surfaces = trySurface versionText helpText |> Option.toList }

                /// FAkka.Argu DU for the Agy 1.0.x print/prompt command-line surface.
                type CliArgument =
                    /// Run a single prompt non-interactively and print the response.
                    | [<AltCommandLine("-p")>] Print
                    /// Alias for `--print` in Agy 1.0.x.
                    | Prompt
                    /// Continue the most recent conversation.
                    | [<AltCommandLine("-c")>] Continue
                    /// Resume a previous conversation by id.
                    | Conversation of conversationId: string
                    /// Add a directory to the workspace; repeatable.
                    | Add_Dir of path: string
                    /// Model for the current CLI session.
                    | Model of model: string
                    /// Project id for the current CLI session.
                    | Project of projectId: string
                    /// Create a new project for this session.
                    | New_Project
                    /// Override CLI log file path.
                    | Log_File of path: string
                    /// Timeout for print mode wait, using Agy/Go duration text such as `5m0s`.
                    | Print_Timeout of duration: string
                    /// Run in a sandbox with terminal restrictions enabled.
                    | Sandbox
                    /// Auto-approve all tool permission requests without prompting.
                    | Dangerously_Skip_Permissions

                    interface IArgParserTemplate with
                        member this.Usage =
                            match this with
                            | Print -> "Run a single prompt non-interactively and print the response."
                            | Prompt -> "Alias for --print in Agy 1.0.x."
                            | Continue -> "Continue the most recent conversation."
                            | Conversation _ -> "Resume a previous conversation by id."
                            | Add_Dir _ -> "Add a directory to the workspace; repeatable."
                            | Model _ -> "Model for the current CLI session."
                            | Project _ -> "Project id for the current CLI session."
                            | New_Project -> "Create a new project for this session."
                            | Log_File _ -> "Override CLI log file path."
                            | Print_Timeout _ -> "Timeout for print mode wait."
                            | Sandbox -> "Run in a sandbox with terminal restrictions enabled."
                            | Dangerously_Skip_Permissions -> "Auto-approve tool permission requests."

                /// Normalized Agy 1.0.x print mode arguments.
                type Args =
                    { /// Render `--print`.
                      Print: bool
                      /// Render `--prompt`, the Agy 1.0.x alias for `--print`.
                      PromptAlias: bool
                      /// Render `--continue`.
                      Continue: bool
                      /// Render `--conversation <id>`.
                      Conversation: string option
                      /// Render repeatable `--add-dir <path>`.
                      AddDirs: string list
                      /// Render `--model <model>`.
                      Model: string option
                      /// Render `--project <id>`.
                      Project: string option
                      /// Render `--new-project`.
                      NewProject: bool
                      /// Render `--log-file <path>`.
                      LogFile: string option
                      /// Render `--print-timeout <duration>`.
                      PrintTimeout: TimeSpan option
                      /// Render `--sandbox`.
                      Sandbox: bool
                      /// Render `--dangerously-skip-permissions`.
                      DangerouslySkipPermissions: bool }

                /// Empty Agy print args.
                let emptyArgs =
                    { Print = false
                      PromptAlias = false
                      Continue = false
                      Conversation = None
                      AddDirs = []
                      Model = None
                      Project = None
                      NewProject = false
                      LogFile = None
                      PrintTimeout = None
                      Sandbox = false
                      DangerouslySkipPermissions = false }

                /// Create an Argu parser for the Agy 1.0.x print surface.
                let argumentParser programName =
                    ArgumentParser.Create<CliArgument>(programName = programName)

                /// Try to parse Agy/Go duration text or .NET TimeSpan text.
                let tryParseDurationText (text: string) =
                    let trimmed =
                        if isNull text then
                            String.Empty
                        else
                            text.Trim()

                    match TimeSpan.TryParse trimmed with
                    | true, value -> Some value
                    | false, _ ->
                        let regex = Regex(@"(?<value>\d+)(?<unit>ms|h|m|s)", RegexOptions.CultureInvariant)
                        let matches = regex.Matches trimmed |> Seq.cast<Match> |> Seq.toList
                        let rebuilt = matches |> List.map (fun m -> m.Value) |> String.concat String.Empty

                        if matches.IsEmpty || rebuilt <> trimmed then
                            None
                        else
                            let folder (total: TimeSpan) (m: Match) =
                                let value = Int64.Parse m.Groups["value"].Value

                                match m.Groups["unit"].Value with
                                | "h" -> total + TimeSpan.FromHours(float value)
                                | "m" -> total + TimeSpan.FromMinutes(float value)
                                | "s" -> total + TimeSpan.FromSeconds(float value)
                                | "ms" -> total + TimeSpan.FromMilliseconds(float value)
                                | _ -> total

                            Some(matches |> List.fold folder TimeSpan.Zero)

                /// Parse Agy/Go duration text or fail with an argument error.
                let parseDurationText text =
                    match tryParseDurationText text with
                    | Some value -> value
                    | None -> invalidArg "text" $"Unsupported Agy duration text: {text}"

                /// Convert parsed FAkka.Argu results into normalized Agy print args.
                let fromParseResults (results: ParseResults<CliArgument>) =
                    { emptyArgs with
                        Print = results.Contains Print
                        PromptAlias = results.Contains Prompt
                        Continue = results.Contains Continue
                        Conversation = results.TryGetResult Conversation
                        AddDirs = results.GetResults Add_Dir
                        Model = results.TryGetResult Model
                        Project = results.TryGetResult Project
                        NewProject = results.Contains New_Project
                        LogFile = results.TryGetResult Log_File
                        PrintTimeout = results.TryGetResult Print_Timeout |> Option.map parseDurationText
                        Sandbox = results.Contains Sandbox
                        DangerouslySkipPermissions = results.Contains Dangerously_Skip_Permissions }

                /// Format a TimeSpan as Agy/Go duration text.
                let formatDurationText (duration: TimeSpan) =
                    if duration < TimeSpan.Zero then
                        invalidArg "duration" "Agy print timeout cannot be negative."

                    let totalMilliseconds = int64 duration.TotalMilliseconds
                    let hours = totalMilliseconds / 3_600_000L
                    let minutes = (totalMilliseconds % 3_600_000L) / 60_000L
                    let seconds = (totalMilliseconds % 60_000L) / 1_000L
                    let milliseconds = totalMilliseconds % 1_000L

                    [ if hours > 0L then
                          $"{hours}h"
                      if minutes > 0L || hours > 0L then
                          $"{minutes}m"
                      if seconds > 0L || minutes > 0L || hours > 0L then
                          $"{seconds}s"
                      if milliseconds > 0L || totalMilliseconds = 0L then
                          $"{milliseconds}ms" ]
                    |> String.concat String.Empty

                /// Render normalized Agy print args into argv without shell interpolation.
                let renderArguments (args: Args) =
                    [ if args.Print then
                          "--print"
                      if args.PromptAlias then
                          "--prompt"
                      if args.Continue then
                          "--continue"
                      match args.Conversation with
                      | Some conversationId ->
                          "--conversation"
                          conversationId
                      | None -> ()
                      for addDir in args.AddDirs do
                          "--add-dir"
                          addDir
                      match args.Model with
                      | Some model ->
                          "--model"
                          model
                      | None -> ()
                      match args.Project with
                      | Some projectId ->
                          "--project"
                          projectId
                      | None -> ()
                      if args.NewProject then
                          "--new-project"
                      match args.LogFile with
                      | Some path ->
                          "--log-file"
                          path
                      | None -> ()
                      match args.PrintTimeout with
                      | Some timeout ->
                          "--print-timeout"
                          formatDurationText timeout
                      | None -> ()
                      if args.Sandbox then
                          "--sandbox"
                      if args.DangerouslySkipPermissions then
                          "--dangerously-skip-permissions" ]

                /// Quote a display argument for diagnostic text only.
                let quoteDisplayArgument (argument: string) =
                    if String.IsNullOrEmpty argument then
                        "\"\""
                    elif argument.IndexOfAny [| ' '; '\t'; '\r'; '\n'; '"' |] >= 0 then
                        "\"" + argument.Replace("\"", "\\\"") + "\""
                    else
                        argument

                /// Render a redacted human-readable command display.
                let renderRedactedDisplay executablePath args =
                    executablePath :: renderArguments args
                    |> List.map quoteDisplayArgument
                    |> String.concat " "
                    |> Redaction.redactHighRisk
                    |> fun result -> result.Text

                /// Render a normalized command record for the process runner boundary.
                let renderCommand executablePath workingDirectory args =
                    { FileName = executablePath
                      Arguments = renderArguments args
                      WorkingDirectory = workingDirectory
                      Environment = Map.empty
                      RedactedDisplay = renderRedactedDisplay executablePath args }

                /// Raw Agy stdout/stderr captured from one print-mode run.
                type OutputCapture =
                    { /// Captured stdout text.
                      Stdout: string
                      /// Captured stderr text.
                      Stderr: string
                      /// UTC process start time.
                      StartedUtc: DateTimeOffset
                      /// UTC process completion time.
                      CompletedUtc: DateTimeOffset option
                      /// Normalized run outcome.
                      Outcome: RunOutcome }

                /// Stored artifact mapping for one Agy print-mode run.
                type OutputArtifactMapping =
                    { /// Stored stdout log artifact.
                      Stdout: FileArtifactStore.StoredArtifact
                      /// Stored stderr log artifact.
                      Stderr: FileArtifactStore.StoredArtifact
                      /// Stored final markdown artifact, when stdout is not blank.
                      FinalMessage: FileArtifactStore.StoredArtifact option
                      /// In-memory manifest containing the stored artifact refs.
                      Manifest: ArtifactManifest }

                /// Map captured Agy print-mode output into file artifacts and an artifact manifest.
                let mapOutputArtifacts
                    (storeConfig: FileArtifactStore.FileArtifactStoreConfig)
                    (request: RunRequest)
                    (capture: OutputCapture)
                    =
                    let createdUtc = capture.CompletedUtc |> Option.defaultValue DateTimeOffset.UtcNow

                    let stdout =
                        FileArtifactStore.writeText
                            storeConfig
                            request.SessionId
                            request.RunId
                            StdoutLog
                            "stdout.log"
                            capture.Stdout
                            createdUtc

                    let stderr =
                        FileArtifactStore.writeText
                            storeConfig
                            request.SessionId
                            request.RunId
                            StderrLog
                            "stderr.log"
                            capture.Stderr
                            createdUtc

                    let finalMessage =
                        if String.IsNullOrWhiteSpace capture.Stdout then
                            None
                        else
                            FileArtifactStore.writeText
                                storeConfig
                                request.SessionId
                                request.RunId
                                FinalMarkdown
                                "final.md"
                                capture.Stdout
                                createdUtc
                            |> Some

                    let artifactRefs =
                        [ stdout.Reference
                          stderr.Reference
                          match finalMessage with
                          | Some artifact -> artifact.Reference
                          | None -> () ]

                    { Stdout = stdout
                      Stderr = stderr
                      FinalMessage = finalMessage
                      Manifest =
                        { RunId = request.RunId
                          SessionId = request.SessionId
                          Engine = Agy
                          SurfaceId = SurfaceId
                          PtcsMessages = request.PtcsMessages
                          PtcsTask = request.PtcsTask
                          StartedUtc = capture.StartedUtc
                          CompletedUtc = capture.CompletedUtc
                          Outcome = capture.Outcome
                          Artifacts = artifactRefs } }
