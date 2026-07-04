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

    /// Codex CLI engine surfaces.
    module Codex =

        /// Codex CLI 0.142.x surfaces.
        module V0_142 =

            /// Probe helpers for the Codex CLI 0.142.x exec surface.
            module Exec =

                /// Stable surface id for Codex 0.142.x exec mode.
                [<Literal>]
                let SurfaceId = "codex-exec-0.142"

                /// Normalize raw version text captured from `codex --version`.
                let normalizeVersionText (versionText: string) =
                    if isNull versionText then
                        String.Empty
                    else
                        let trimmed = versionText.Trim()
                        let prefix = "codex-cli "

                        if trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) then
                            trimmed.Substring(prefix.Length).Trim()
                        else
                            trimmed

                /// Return true when the version belongs to the supported Codex 0.142.x family.
                let isSupportedVersion (versionText: string) =
                    match Version.TryParse(normalizeVersionText versionText) with
                    | true, version -> version.Major = 0 && version.Minor = 142
                    | false, _ -> false

                /// Tokenize `codex exec --help` output for option discovery.
                let helpTokens (helpText: string) =
                    if String.IsNullOrWhiteSpace helpText then
                        Set.empty
                    else
                        helpText.Split([| ' '; '\t'; '\r'; '\n'; ','; ':'; '('; ')' |], StringSplitOptions.RemoveEmptyEntries)
                        |> Set.ofArray

                /// Return true when the help text exposes an option token.
                let hasOption optionName helpText =
                    helpTokens helpText |> Set.contains optionName

                /// Discover normalized engine capabilities from `codex exec --help` text.
                let discoverCapabilities (helpText: string) =
                    let tokens = helpTokens helpText
                    let has optionName = tokens |> Set.contains optionName
                    let contains (text: string) = helpText.Contains(text, StringComparison.OrdinalIgnoreCase)

                    [ if contains "Usage: codex exec" then
                          SingleTurnHeadless
                      if has "resume" then
                          Continuation
                      if has "--json" then
                          StructuredEventStream
                      if has "--output-last-message" then
                          FinalMessageFile
                      if has "--add-dir" then
                          WorkspaceDirectories
                      if has "--sandbox" then
                          SandboxMode
                      if has "--model" then
                          ModelSelection ]
                    |> Set.ofList

                /// Try to create a Codex 0.142.x exec surface from captured probe text.
                let trySurface versionText helpText =
                    let capabilities = discoverCapabilities helpText

                    if isSupportedVersion versionText && capabilities.Contains SingleTurnHeadless then
                        Some
                            { Kind = Domain.EngineKind.Codex
                              VersionText = normalizeVersionText versionText
                              SurfaceId = SurfaceId
                              Capabilities = capabilities }
                    else
                        None

                /// Build a normalized engine probe from captured `codex --version` and `codex exec --help` output.
                let probeFromText executablePath versionText helpText : EngineProbe =
                    { ExecutablePath = executablePath
                      VersionText = normalizeVersionText versionText
                      Surfaces = trySurface versionText helpText |> Option.toList }

                /// Codex exec sandbox mode values.
                type SandboxMode =
                    /// Read-only sandbox.
                    | ReadOnly
                    /// Workspace-write sandbox.
                    | WorkspaceWrite
                    /// Full-access sandbox.
                    | DangerFullAccess

                /// Codex exec color output mode values.
                type ColorMode =
                    /// Always emit color.
                    | Always
                    /// Never emit color.
                    | Never
                    /// Let Codex decide based on terminal detection.
                    | Auto

                /// FAkka.Argu DU for the Codex CLI 0.142.x exec surface.
                type CliArgument =
                    /// Initial instructions for the agent as a positional prompt.
                    | [<MainCommand>] Prompt of prompt: string
                    /// Override one config value, repeatable.
                    | [<AltCommandLine("-c")>] Config of assignment: string
                    /// Enable a feature, repeatable.
                    | Enable of feature: string
                    /// Disable a feature, repeatable.
                    | Disable of feature: string
                    /// Error when config contains fields not recognized by this Codex version.
                    | Strict_Config
                    /// Attach an image to the initial prompt, repeatable.
                    | [<AltCommandLine("-i")>] Image of path: string
                    /// Model the agent should use.
                    | [<AltCommandLine("-m")>] Model of model: string
                    /// Use open-source provider.
                    | Oss
                    /// Local provider to use with OSS mode.
                    | Local_Provider of provider: string
                    /// Config profile name.
                    | [<AltCommandLine("-p")>] Profile of profile: string
                    /// Sandbox policy value.
                    | [<AltCommandLine("-s")>] Sandbox of mode: string
                    /// Bypass approvals and sandbox for externally sandboxed automation.
                    | Dangerously_Bypass_Approvals_And_Sandbox
                    /// Run enabled hooks without persisted hook trust.
                    | Dangerously_Bypass_Hook_Trust
                    /// Working root for the agent.
                    | [<AltCommandLine("-C")>] Cd of directory: string
                    /// Additional writable directory, repeatable.
                    | Add_Dir of directory: string
                    /// Allow running outside a Git repository.
                    | Skip_Git_Repo_Check
                    /// Run without persisting session files to disk.
                    | Ephemeral
                    /// Do not load user config.
                    | Ignore_User_Config
                    /// Do not load user or project execpolicy rules.
                    | Ignore_Rules
                    /// JSON schema file for the final response shape.
                    | Output_Schema of path: string
                    /// Color output mode.
                    | Color of mode: string
                    /// Print events to stdout as JSONL.
                    | Json
                    /// File where the last message should be written.
                    | [<AltCommandLine("-o")>] Output_Last_Message of path: string

                    interface IArgParserTemplate with
                        member this.Usage =
                            match this with
                            | Prompt _ -> "Initial instructions for the agent as a positional prompt."
                            | Config _ -> "Override one config value; repeatable."
                            | Enable _ -> "Enable a feature; repeatable."
                            | Disable _ -> "Disable a feature; repeatable."
                            | Strict_Config -> "Reject unrecognized config fields."
                            | Image _ -> "Attach an image to the initial prompt; repeatable."
                            | Model _ -> "Model the agent should use."
                            | Oss -> "Use open-source provider."
                            | Local_Provider _ -> "Local provider to use with OSS mode."
                            | Profile _ -> "Config profile name."
                            | Sandbox _ -> "Sandbox policy value."
                            | Dangerously_Bypass_Approvals_And_Sandbox -> "Bypass approvals and sandbox."
                            | Dangerously_Bypass_Hook_Trust -> "Bypass hook trust prompt."
                            | Cd _ -> "Working root for the agent."
                            | Add_Dir _ -> "Additional writable directory; repeatable."
                            | Skip_Git_Repo_Check -> "Allow running outside a Git repository."
                            | Ephemeral -> "Run without persisting session files to disk."
                            | Ignore_User_Config -> "Do not load user config."
                            | Ignore_Rules -> "Do not load user/project execpolicy rules."
                            | Output_Schema _ -> "JSON schema file for final response shape."
                            | Color _ -> "Color output mode."
                            | Json -> "Print events to stdout as JSONL."
                            | Output_Last_Message _ -> "File where the last message should be written."

                /// Normalized Codex 0.142.x exec arguments.
                type Args =
                    { /// Optional positional prompt. Prefer stdin/prompt artifact policy for long prompts.
                      Prompt: string option
                      /// Repeatable `-c` / `--config` values.
                      Config: string list
                      /// Repeatable `--enable` values.
                      Enable: string list
                      /// Repeatable `--disable` values.
                      Disable: string list
                      /// Render `--strict-config`.
                      StrictConfig: bool
                      /// Repeatable `--image` values.
                      Images: string list
                      /// Render `--model <model>`.
                      Model: string option
                      /// Render `--oss`.
                      Oss: bool
                      /// Render `--local-provider <provider>`.
                      LocalProvider: string option
                      /// Render `--profile <name>`.
                      Profile: string option
                      /// Render `--sandbox <mode>`.
                      Sandbox: SandboxMode option
                      /// Render `--dangerously-bypass-approvals-and-sandbox`.
                      DangerouslyBypassApprovalsAndSandbox: bool
                      /// Render `--dangerously-bypass-hook-trust`.
                      DangerouslyBypassHookTrust: bool
                      /// Render `--cd <dir>`.
                      Cd: string option
                      /// Repeatable `--add-dir` values.
                      AddDirs: string list
                      /// Render `--skip-git-repo-check`.
                      SkipGitRepoCheck: bool
                      /// Render `--ephemeral`.
                      Ephemeral: bool
                      /// Render `--ignore-user-config`.
                      IgnoreUserConfig: bool
                      /// Render `--ignore-rules`.
                      IgnoreRules: bool
                      /// Render `--output-schema <file>`.
                      OutputSchema: string option
                      /// Render `--color <mode>`.
                      Color: ColorMode option
                      /// Render `--json`.
                      Json: bool
                      /// Render `--output-last-message <file>`.
                      OutputLastMessage: string option }

                /// Empty Codex exec args.
                let emptyArgs =
                    { Prompt = None
                      Config = []
                      Enable = []
                      Disable = []
                      StrictConfig = false
                      Images = []
                      Model = None
                      Oss = false
                      LocalProvider = None
                      Profile = None
                      Sandbox = None
                      DangerouslyBypassApprovalsAndSandbox = false
                      DangerouslyBypassHookTrust = false
                      Cd = None
                      AddDirs = []
                      SkipGitRepoCheck = false
                      Ephemeral = false
                      IgnoreUserConfig = false
                      IgnoreRules = false
                      OutputSchema = None
                      Color = None
                      Json = false
                      OutputLastMessage = None }

                /// Create an Argu parser for the Codex 0.142.x exec surface.
                let argumentParser programName =
                    ArgumentParser.Create<CliArgument>(programName = programName)

                /// Parse a Codex sandbox mode string.
                let parseSandboxMode (text: string) =
                    match text.Trim().ToLowerInvariant() with
                    | "read-only" -> ReadOnly
                    | "workspace-write" -> WorkspaceWrite
                    | "danger-full-access" -> DangerFullAccess
                    | _ -> invalidArg "text" $"Unsupported Codex sandbox mode: {text}"

                /// Render a Codex sandbox mode string.
                let formatSandboxMode mode =
                    match mode with
                    | ReadOnly -> "read-only"
                    | WorkspaceWrite -> "workspace-write"
                    | DangerFullAccess -> "danger-full-access"

                /// Parse a Codex color mode string.
                let parseColorMode (text: string) =
                    match text.Trim().ToLowerInvariant() with
                    | "always" -> Always
                    | "never" -> Never
                    | "auto" -> Auto
                    | _ -> invalidArg "text" $"Unsupported Codex color mode: {text}"

                /// Render a Codex color mode string.
                let formatColorMode mode =
                    match mode with
                    | Always -> "always"
                    | Never -> "never"
                    | Auto -> "auto"

                /// Convert parsed FAkka.Argu results into normalized Codex exec args.
                let fromParseResults (results: ParseResults<CliArgument>) =
                    { emptyArgs with
                        Prompt = results.TryGetResult Prompt
                        Config = results.GetResults Config
                        Enable = results.GetResults Enable
                        Disable = results.GetResults Disable
                        StrictConfig = results.Contains Strict_Config
                        Images = results.GetResults Image
                        Model = results.TryGetResult Model
                        Oss = results.Contains Oss
                        LocalProvider = results.TryGetResult Local_Provider
                        Profile = results.TryGetResult Profile
                        Sandbox = results.TryGetResult Sandbox |> Option.map parseSandboxMode
                        DangerouslyBypassApprovalsAndSandbox = results.Contains Dangerously_Bypass_Approvals_And_Sandbox
                        DangerouslyBypassHookTrust = results.Contains Dangerously_Bypass_Hook_Trust
                        Cd = results.TryGetResult Cd
                        AddDirs = results.GetResults Add_Dir
                        SkipGitRepoCheck = results.Contains Skip_Git_Repo_Check
                        Ephemeral = results.Contains Ephemeral
                        IgnoreUserConfig = results.Contains Ignore_User_Config
                        IgnoreRules = results.Contains Ignore_Rules
                        OutputSchema = results.TryGetResult Output_Schema
                        Color = results.TryGetResult Color |> Option.map parseColorMode
                        Json = results.Contains Json
                        OutputLastMessage = results.TryGetResult Output_Last_Message }

                /// Render normalized Codex exec args into argv without shell interpolation.
                let renderArguments (args: Args) =
                    [ "exec"
                      for config in args.Config do
                          "-c"
                          config
                      for feature in args.Enable do
                          "--enable"
                          feature
                      for feature in args.Disable do
                          "--disable"
                          feature
                      if args.StrictConfig then
                          "--strict-config"
                      for image in args.Images do
                          "--image"
                          image
                      match args.Model with
                      | Some model ->
                          "--model"
                          model
                      | None -> ()
                      if args.Oss then
                          "--oss"
                      match args.LocalProvider with
                      | Some provider ->
                          "--local-provider"
                          provider
                      | None -> ()
                      match args.Profile with
                      | Some profile ->
                          "--profile"
                          profile
                      | None -> ()
                      match args.Sandbox with
                      | Some mode ->
                          "--sandbox"
                          formatSandboxMode mode
                      | None -> ()
                      if args.DangerouslyBypassApprovalsAndSandbox then
                          "--dangerously-bypass-approvals-and-sandbox"
                      if args.DangerouslyBypassHookTrust then
                          "--dangerously-bypass-hook-trust"
                      match args.Cd with
                      | Some directory ->
                          "--cd"
                          directory
                      | None -> ()
                      for addDir in args.AddDirs do
                          "--add-dir"
                          addDir
                      if args.SkipGitRepoCheck then
                          "--skip-git-repo-check"
                      if args.Ephemeral then
                          "--ephemeral"
                      if args.IgnoreUserConfig then
                          "--ignore-user-config"
                      if args.IgnoreRules then
                          "--ignore-rules"
                      match args.OutputSchema with
                      | Some path ->
                          "--output-schema"
                          path
                      | None -> ()
                      match args.Color with
                      | Some color ->
                          "--color"
                          formatColorMode color
                      | None -> ()
                      if args.Json then
                          "--json"
                      match args.OutputLastMessage with
                      | Some path ->
                          "--output-last-message"
                          path
                      | None -> ()
                      match args.Prompt with
                      | Some prompt ->
                          prompt
                      | None -> () ]

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

                /// Raw Codex stdout/stderr/final output captured from one exec run.
                type OutputCapture =
                    { /// Captured stdout text.
                      Stdout: string
                      /// Captured stderr text.
                      Stderr: string
                      /// Captured JSONL event stream, usually stdout when `--json` is enabled.
                      EventJsonl: string option
                      /// Captured final assistant markdown, usually from `--output-last-message`.
                      FinalMessage: string option
                      /// UTC process start time.
                      StartedUtc: DateTimeOffset
                      /// UTC process completion time.
                      CompletedUtc: DateTimeOffset option
                      /// Normalized run outcome.
                      Outcome: RunOutcome }

                /// Stored artifact mapping for one Codex exec run.
                type OutputArtifactMapping =
                    { /// Stored stdout log artifact.
                      Stdout: FileArtifactStore.StoredArtifact
                      /// Stored stderr log artifact.
                      Stderr: FileArtifactStore.StoredArtifact
                      /// Stored event JSONL artifact, when supplied.
                      EventJsonl: FileArtifactStore.StoredArtifact option
                      /// Stored final markdown artifact, when supplied.
                      FinalMessage: FileArtifactStore.StoredArtifact option
                      /// In-memory manifest containing the stored artifact refs.
                      Manifest: ArtifactManifest }

                /// Map captured Codex exec output into file artifacts and an artifact manifest.
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

                    let eventJsonl =
                        match capture.EventJsonl with
                        | Some text when not (String.IsNullOrWhiteSpace text) ->
                            FileArtifactStore.writeText
                                storeConfig
                                request.SessionId
                                request.RunId
                                EventJsonl
                                "events.jsonl"
                                text
                                createdUtc
                            |> Some
                        | _ -> None

                    let finalMessage =
                        match capture.FinalMessage with
                        | Some text when not (String.IsNullOrWhiteSpace text) ->
                            FileArtifactStore.writeText
                                storeConfig
                                request.SessionId
                                request.RunId
                                FinalMarkdown
                                "final.md"
                                text
                                createdUtc
                            |> Some
                        | _ -> None

                    let artifactRefs =
                        [ stdout.Reference
                          stderr.Reference
                          match eventJsonl with
                          | Some artifact -> artifact.Reference
                          | None -> ()
                          match finalMessage with
                          | Some artifact -> artifact.Reference
                          | None -> () ]

                    { Stdout = stdout
                      Stderr = stderr
                      EventJsonl = eventJsonl
                      FinalMessage = finalMessage
                      Manifest =
                        { RunId = request.RunId
                          SessionId = request.SessionId
                          Engine = Domain.EngineKind.Codex
                          SurfaceId = SurfaceId
                          PtcsMessages = request.PtcsMessages
                          PtcsTask = request.PtcsTask
                          StartedUtc = capture.StartedUtc
                          CompletedUtc = capture.CompletedUtc
                          Outcome = capture.Outcome
                          Artifacts = artifactRefs } }

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
                      /// Optional positional prompt text passed to Agy print mode.
                      PromptText: string option
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
                      PromptText = None
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
                    [ if args.Continue then
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
                          $"--print-timeout={formatDurationText timeout}"
                      | None -> ()
                      if args.Sandbox then
                          "--sandbox"
                      if args.DangerouslySkipPermissions then
                          "--dangerously-skip-permissions"
                      if args.Print then
                          "--print"
                      if args.PromptAlias then
                          "--prompt"
                      match args.PromptText with
                      | Some promptText ->
                          promptText
                      | None -> () ]

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
