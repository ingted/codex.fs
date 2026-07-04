namespace CodexFs.Host

open System
open CodexFs
open CodexFs.Domain
open CodexFs.Ptcs
open PulseTrade.Comm.Spa

/// Minimal host runtime state and non-secret health reporting.
module HostRuntime =

    /// Lifecycle state of the minimal host runtime.
    type HostRuntimeStatus =
        /// Runtime was created from config but no fabric was initialized.
        | Created
        /// Runtime initialized the selected in-process PTCS MessageFabric boundary.
        | Running
        /// Runtime was stopped by the caller.
        | Stopped

    /// Minimal host runtime state.
    type HostRuntime =
        { /// Effective host configuration.
          Config: HostConfig.HostConfig
          /// Redacted config diagnostics captured during loading.
          ConfigDiagnostics: HostConfig.HostConfigDiagnostic list
          /// Runtime lifecycle status.
          Status: HostRuntimeStatus
          /// UTC timestamp when the runtime was started.
          StartedUtc: DateTimeOffset option
          /// Concrete PTCS MessageFabric instance when initialized.
          MessageFabric: CommSpaMessageFabric option }

    /// Non-secret health view for CLI/HTTP/control surfaces.
    type HostHealth =
        { /// Runtime lifecycle status.
          Status: HostRuntimeStatus
          /// Default engine name.
          DefaultEngine: string
          /// Enabled engine names.
          EnabledEngines: string list
          /// Artifact root path from config.
          ArtifactRoot: string
          /// Host control protocol.
          ControlProtocol: string
          /// Advertised control URI.
          ControlAdvertiseUri: string
          /// True when loopback-only control addresses are allowed.
          ControlAllowLoopbackOnly: bool
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
          /// Concrete PTCS MessageFabric type name, when present.
          MessageFabricType: string option
          /// Started timestamp, when present.
          StartedUtc: DateTimeOffset option
          /// Engine families with executable overrides; values are intentionally omitted.
          EngineOverrideKeys: string list
          /// Redacted setting diagnostics.
          RedactedDiagnostics: HostConfig.HostConfigDiagnostic list
          /// Non-secret warnings for operators.
          Warnings: string list }

    /// Format a runtime status for display.
    let formatStatus status =
        match status with
        | Created -> "created"
        | Running -> "running"
        | Stopped -> "stopped"

    /// Create a runtime from an already validated config.
    let create config diagnostics =
        { Config = config
          ConfigDiagnostics = diagnostics
          Status = Created
          StartedUtc = None
          MessageFabric = None }

    /// Create a runtime from a host config load result.
    let tryCreateFromLoadResult (loadResult: HostConfig.HostConfigLoadResult) =
        match loadResult.Config with
        | Some config -> Ok(create config loadResult.Diagnostics)
        | None -> Error loadResult.Issues

    /// Initialize a real in-process PTCS MessageFabric runtime boundary without creating an ActorSystem.
    let startInProcessMessageFabric startedUtc runtime =
        { runtime with
            Status = Running
            StartedUtc = Some startedUtc
            MessageFabric = Some(MessageFabricBinding.createInProcessFabric ()) }

    /// Mark the runtime stopped without disposing caller-owned resources.
    let stop runtime =
        { runtime with
            Status = Stopped
            MessageFabric = None }

    /// Build non-secret health data for the runtime.
    let health runtime =
        let config = runtime.Config

        let warnings =
            [ if config.ControlEndpoint.AllowLoopbackOnly then
                  "loopback-control-enabled"
              if config.Ptcs.DurableAgentTasks then
                  "durable-agent-tasks-enabled" ]

        { Status = runtime.Status
          DefaultEngine = HostConfig.formatEngineKind config.DefaultEngine
          EnabledEngines = config.EnabledEngines |> List.map HostConfig.formatEngineKind
          ArtifactRoot = config.ArtifactRoot
          ControlProtocol = config.ControlEndpoint.Protocol
          ControlAdvertiseUri = config.ControlEndpoint.AdvertiseUri
          ControlAllowLoopbackOnly = config.ControlEndpoint.AllowLoopbackOnly
          PtcsFabricMode = config.Ptcs.FabricMode
          PtcsSessionParticipantPrefix = config.Ptcs.SessionParticipantPrefix
          PtcsDefaultInboxLimit = config.Ptcs.DefaultInboxLimit
          DurableAgentTasks = config.Ptcs.DurableAgentTasks
          HasMessageFabric = runtime.MessageFabric.IsSome
          MessageFabricType = runtime.MessageFabric |> Option.map (fun fabric -> fabric.GetType().FullName)
          StartedUtc = runtime.StartedUtc
          EngineOverrideKeys = config.EngineExecutableOverrides |> Map.toList |> List.map (fst >> HostConfig.formatEngineKind)
          RedactedDiagnostics = runtime.ConfigDiagnostics
          Warnings = warnings }

    /// Render a redacted single-text health summary for terminal/control output.
    let healthSummary runtime =
        let current = health runtime
        let enabledEnginesText = String.concat "," current.EnabledEngines
        let engineOverrideKeysText = String.concat "," current.EngineOverrideKeys

        [ $"status={formatStatus current.Status}"
          $"defaultEngine={current.DefaultEngine}"
          $"enabledEngines={enabledEnginesText}"
          $"artifactRoot={current.ArtifactRoot}"
          $"controlProtocol={current.ControlProtocol}"
          $"controlAdvertiseUri={current.ControlAdvertiseUri}"
          $"controlAllowLoopbackOnly={current.ControlAllowLoopbackOnly}"
          $"ptcsFabricMode={current.PtcsFabricMode}"
          $"ptcsSessionParticipantPrefix={current.PtcsSessionParticipantPrefix}"
          $"ptcsDefaultInboxLimit={current.PtcsDefaultInboxLimit}"
          $"durableAgentTasks={current.DurableAgentTasks}"
          $"hasMessageFabric={current.HasMessageFabric}"
          match current.MessageFabricType with
          | Some typeName -> $"messageFabricType={typeName}"
          | None -> "messageFabricType="
          match current.StartedUtc with
          | Some startedUtc -> $"startedUtc={startedUtc:O}"
          | None -> "startedUtc="
          $"engineOverrideKeys={engineOverrideKeysText}"
          for diagnostic in current.RedactedDiagnostics do
              $"setting.{diagnostic.Key}={diagnostic.Value}" ]
        |> String.concat "\n"
        |> Redaction.redactHighRisk
        |> fun result -> result.Text
