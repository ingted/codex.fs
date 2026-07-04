namespace CodexFs

open System
open System.Threading
open System.Threading.Tasks
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
