namespace CodexFs

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
