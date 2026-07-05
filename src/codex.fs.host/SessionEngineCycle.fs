namespace CodexFs.Host

open System
open System.IO
open System.Threading
open CodexFs
open CodexFs.Domain
open CodexFs.Ptcs

/// Host-facing wrapper for the shared PTCS runtime cycle.
module SessionEngineCycle =

    /// Caller options for one bounded session cycle.
    type SingleCycleOptions =
        { /// Target session id.
          SessionId: string
          /// Engine family override; defaults to host config.
          Engine: EngineKind option
          /// Executable path/command override; defaults to `agy` or `codex`.
          ExecutablePath: string option
          /// Working directory used by the engine process.
          WorkingDirectory: string option
          /// Artifact root override; defaults to host config.
          ArtifactRoot: string option
          /// Engine timeout override; defaults to host config.
          Timeout: TimeSpan option
          /// Optional instruction prepended to the assembled prompt.
          SystemInstruction: string option
          /// Additional directories exposed to the engine.
          AdditionalDirectories: string list }

    /// Result returned by one bounded session cycle.
    type SingleCycleResult =
        { /// Stable status text: `empty`, `completed`, `failed`, `timed-out`, or `cancelled`.
          Status: string
          /// Target session id.
          SessionId: string
          /// Session participant id derived from host config.
          SessionParticipantId: string
          /// Run id when an engine run was started.
          RunId: string
          /// Number of messages consumed into the prompt.
          ConsumedMessageCount: int
          /// Cursor acknowledged after artifact persistence and reply.
          AckCursor: string
          /// Engine run outcome when an engine run was started.
          Outcome: string
          /// Process exit code text, or blank when unavailable.
          ExitCode: string
          /// Artifact manifest path relative to artifact root.
          ArtifactManifestPath: string
          /// Persistence boundary artifact written after reply evidence and before inbox ack.
          PersistenceBoundaryPath: string
          /// Final message artifact path relative to artifact root.
          FinalMessagePath: string
          /// Redacted run note artifact path relative to artifact root.
          RunNotePath: string
          /// PTCS reply message id.
          ReplyMessageId: string
          /// Reply body sent through MessageFabric.
          ReplyBody: string }

    let defaultExecutable engine =
        RuntimeMessageFabricCycle.defaultExecutable engine

    let configuredExecutable (config: HostConfig.HostConfig) engine =
        config.EngineExecutableOverrides
        |> Map.tryFind engine
        |> Option.defaultValue (defaultExecutable engine)

    let effectiveOptions (runtime: HostRuntime.HostRuntime) options =
        let engine = options.Engine |> Option.defaultValue runtime.Config.DefaultEngine
        let executablePath = options.ExecutablePath |> Option.defaultValue (configuredExecutable runtime.Config engine)
        let workingDirectory = options.WorkingDirectory |> Option.defaultValue (Directory.GetCurrentDirectory())
        let artifactRoot = options.ArtifactRoot |> Option.defaultValue runtime.Config.ArtifactRoot
        let timeout = options.Timeout |> Option.defaultValue runtime.Config.DefaultTimeout
        engine, executablePath, Path.GetFullPath workingDirectory, Path.GetFullPath artifactRoot, timeout

    let toRuntimeOptions runtime options : RuntimeMessageFabricCycle.RuntimeCycleOptions =
        let engine, executablePath, workingDirectory, artifactRoot, timeout = effectiveOptions runtime options

        { SessionId = options.SessionId
          SessionParticipantId = HostControl.sessionParticipantId runtime.Config options.SessionId
          ReplyParticipantId = runtime.Config.Ptcs.ReplyParticipantId
          Engine = engine
          ExecutablePath = executablePath
          WorkingDirectory = workingDirectory
          ArtifactRoot = artifactRoot
          Timeout = timeout
          SystemInstruction = options.SystemInstruction
          AdditionalDirectories = options.AdditionalDirectories
          InboxLimit = runtime.Config.Ptcs.DefaultInboxLimit }

    let fromRuntimeResult (result: RuntimeMessageFabricCycle.RuntimeCycleResult) =
        { Status = result.Status
          SessionId = result.SessionId
          SessionParticipantId = result.SessionParticipantId
          RunId = result.RunId
          ConsumedMessageCount = result.ConsumedMessageCount
          AckCursor = result.AckCursor
          Outcome = result.Outcome
          ExitCode = result.ExitCode
          ArtifactManifestPath = result.ArtifactManifestPath
          PersistenceBoundaryPath = result.PersistenceBoundaryPath
          FinalMessagePath = result.FinalMessagePath
          RunNotePath = result.RunNotePath
          ReplyMessageId = result.ReplyMessageId
          ReplyBody = result.ReplyBody }

    /// Run one bounded session cycle through the shared PTCS runtime adapter.
    let runSingleCycleAsync (runtime: HostRuntime.HostRuntime) (options: SingleCycleOptions) (cancellationToken: CancellationToken) =
        task {
            match runtime.MessageFabric with
            | None -> return invalidOp "Host MessageFabric is not initialized."
            | Some fabric ->
                let runtimeOptions = toRuntimeOptions runtime options
                let! result = RuntimeMessageFabricCycle.runSingleCycleAsync fabric runtimeOptions cancellationToken
                return fromRuntimeResult result
        }
