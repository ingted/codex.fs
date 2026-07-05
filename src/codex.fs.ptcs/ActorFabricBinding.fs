namespace CodexFs.Ptcs

open System
open System.IO
open System.Text
open System.Threading
open System.Threading.Tasks
open Akka.Actor
open CodexFs.Domain
open PulseTrade.Comm.Spa

/// PTCS ActorFabric-backed worker shell helpers.
module ActorFabricBinding =

    /// Participant metadata owned by a codex.fs actor shell.
    type WorkerParticipantSpec =
        { /// PTCS participant id, for example `agent.codexfs.foreman`.
          ParticipantId: string
          /// Human-readable participant display name.
          DisplayName: string option
          /// PTCS participant kind. Actor workers should use `agent`.
          Kind: string option
          /// Non-secret labels visible to PTCS chat/filtering surfaces.
          Labels: string list }

    /// Command asking a worker actor shell to register or refresh its PTCS participant row.
    type EnsureParticipantRegistered =
        { /// Optional timestamp supplied by tests/verifiers for traceability.
          RequestedAtUtc: DateTimeOffset option }

    /// Command asking a Foreman actor shell to spawn and register a child worker.
    type SpawnWorkerParticipant =
        { /// PTCS participant metadata for the child worker.
          Spec: WorkerParticipantSpec
          /// Optional Akka actor name. When omitted, a safe name is derived from the participant id.
          ActorName: string option }

    /// Result returned after a worker actor shell registers its PTCS participant.
    type WorkerParticipantRegistered =
        { /// PTCS participant id.
          ParticipantId: string
          /// Participant display name returned by PTCS.
          DisplayName: string
          /// Participant kind returned by PTCS.
          Kind: string
          /// Labels returned by PTCS.
          Labels: string list
          /// PTCS participant status.
          Status: string
          /// Actor path that performed the registration.
          ActorPath: string
          /// ActorSystem node address from the owning PTCS ActorFabric.
          NodeAddress: string }

    /// Result returned after a Foreman actor shell spawns and registers a child worker.
    type WorkerParticipantSpawned =
        { /// Foreman participant id that spawned the worker.
          ForemanParticipantId: string
          /// Registered child worker metadata.
          Worker: WorkerParticipantRegistered }

    /// Command asking a WorkerActor to consume its PTCS inbox and run one runtime cycle.
    type RunRuntimeCycle =
        { /// Target session id used for artifact paths and prompt identity.
          SessionId: string
          /// PTCS participant id whose inbox should be consumed. Defaults to the actor participant.
          SessionParticipantId: string option
          /// Optional participant id used when sending replies.
          ReplyParticipantId: string option
          /// Engine family override; defaults to Agy.
          Engine: EngineKind option
          /// Executable path or command; defaults to `agy`.
          ExecutablePath: string option
          /// Optional executable overrides keyed by engine family.
          EngineExecutableOverrides: Map<EngineKind, string> option
          /// Working directory used by the engine process; defaults to current directory.
          WorkingDirectory: string option
          /// Artifact root for private run evidence.
          ArtifactRoot: string
          /// Engine timeout; defaults to 20 minutes.
          Timeout: TimeSpan option
          /// Optional instruction prepended to the assembled prompt.
          SystemInstruction: string option
          /// Additional directories exposed to the engine.
          AdditionalDirectories: string list
          /// Optional Agy permission auto-approval for bounded Foreman/tool execution.
          AgyDangerouslySkipPermissions: bool option
          /// Optional Codex model used when no incoming intent tag supplies one.
          CodexModel: string option
          /// Optional Codex approval/sandbox bypass for bounded Foreman/tool execution.
          CodexDangerouslyBypassApprovalsAndSandbox: bool option }

    /// Result returned after a WorkerActor completes one runtime cycle.
    type RuntimeCycleCompleted =
        { /// PTCS participant id of the actor that ran the cycle.
          ParticipantId: string
          /// Actor path that ran the cycle.
          ActorPath: string
          /// ActorSystem node address from the owning PTCS ActorFabric.
          NodeAddress: string
          /// Runtime cycle result.
          Result: RuntimeMessageFabricCycle.RuntimeCycleResult }

    let textOr fallback value =
        if String.IsNullOrWhiteSpace value then fallback else value.Trim()

    let defaultLabels role =
        [ "codex.fs"
          "actorfabric"
          "worker"
          "role:" + role ]

    /// Build the default Foreman participant spec for a PTCS participant prefix.
    let foremanSpec participantPrefix =
        let normalizedPrefix = textOr "agent.codexfs" participantPrefix

        { ParticipantId = normalizedPrefix.TrimEnd('.') + ".foreman"
          DisplayName = Some "codex.fs Foreman"
          Kind = Some "agent"
          Labels = defaultLabels "foreman" }

    /// Build a child worker participant spec.
    let workerSpec participantId displayName labels =
        { ParticipantId = textOr "agent.codexfs.worker" participantId
          DisplayName = displayName
          Kind = Some "agent"
          Labels =
            match labels with
            | [] -> defaultLabels "worker"
            | _ -> labels }

    /// Convert a participant id to an Akka-safe actor name.
    let actorNameFromParticipantId prefix participantId =
        let builder = StringBuilder()
        let appendSafe ch =
            if Char.IsLetterOrDigit ch || ch = '-' || ch = '_' then
                builder.Append ch |> ignore
            else
                builder.Append '-' |> ignore

        (textOr "actor" prefix) |> Seq.iter appendSafe
        builder.Append '-' |> ignore
        (textOr "participant" participantId) |> Seq.iter appendSafe
        builder.ToString().Trim('-')

    type CodexWorkerActor(messageFabric: CommSpaMessageFabric, spec: WorkerParticipantSpec, nodeAddress: string) as this =
        inherit ReceiveActor()

        do
            this.Receive<EnsureParticipantRegistered>(Action<EnsureParticipantRegistered>(fun _ -> this.HandleEnsureRegistered()))

            this.Receive<SpawnWorkerParticipant>(Action<SpawnWorkerParticipant>(fun command -> this.HandleSpawnWorker command))

            this.Receive<RunRuntimeCycle>(Action<RunRuntimeCycle>(fun command -> this.HandleRunRuntimeCycle command))

        member _.ActorCtx: IActorContext = ActorBase.Context

        member this.RegisterAsync() =
            task {
                let binding = MessageFabricBinding.defaultBinding spec.ParticipantId

                let registration: MessageFabricBinding.ParticipantRegistration =
                    { DisplayName = spec.DisplayName
                      Kind = spec.Kind |> Option.orElse (Some "agent")
                      Labels = Some spec.Labels }

                let! reply = MessageFabricBinding.registerParticipantAsync messageFabric binding registration
                let participant = reply.Participant

                return
                    { ParticipantId = participant.ParticipantId
                      DisplayName = participant.DisplayName
                      Kind = participant.Kind
                      Labels = participant.Labels
                      Status = participant.Status
                      ActorPath = this.ActorCtx.Self.Path.ToStringWithoutAddress()
                      NodeAddress = nodeAddress }
            }

        member this.RuntimeOptions(command: RunRuntimeCycle) : RuntimeMessageFabricCycle.RuntimeCycleOptions =
            let sessionParticipantId =
                command.SessionParticipantId
                |> Option.filter (String.IsNullOrWhiteSpace >> not)
                |> Option.map _.Trim()
                |> Option.defaultValue spec.ParticipantId

            let engine = command.Engine |> Option.defaultValue Agy
            let executablePath =
                command.ExecutablePath
                |> Option.filter (String.IsNullOrWhiteSpace >> not)
                |> Option.defaultValue (RuntimeMessageFabricCycle.defaultExecutable engine)

            let workingDirectory =
                command.WorkingDirectory
                |> Option.filter (String.IsNullOrWhiteSpace >> not)
                |> Option.defaultValue (Directory.GetCurrentDirectory())
                |> Path.GetFullPath

            let artifactRoot = Path.GetFullPath command.ArtifactRoot
            let timeout = command.Timeout |> Option.defaultValue (TimeSpan.FromMinutes 20.0)

            { SessionId = textOr "foreman" command.SessionId
              SessionParticipantId = sessionParticipantId
              ReplyParticipantId = command.ReplyParticipantId
              Engine = engine
              ExecutablePath = executablePath
              EngineExecutableOverrides = command.EngineExecutableOverrides |> Option.defaultValue Map.empty
              WorkingDirectory = workingDirectory
              ArtifactRoot = artifactRoot
              Timeout = timeout
              SystemInstruction = command.SystemInstruction
              AdditionalDirectories = command.AdditionalDirectories
              AgyDangerouslySkipPermissions = command.AgyDangerouslySkipPermissions |> Option.defaultValue false
              CodexModel = command.CodexModel
              CodexDangerouslyBypassApprovalsAndSandbox =
                command.CodexDangerouslyBypassApprovalsAndSandbox
                |> Option.defaultValue false
              InboxLimit = MessageFabricBinding.defaultInboxLimit }

        member this.HandleEnsureRegistered() =
            let replyTo = this.ActorCtx.Sender
            let selfRef = this.ActorCtx.Self

            task {
                try
                    let! registered = this.RegisterAsync()
                    replyTo.Tell(registered, selfRef)
                with ex ->
                    replyTo.Tell(Status.Failure ex, selfRef)
            }
            |> ignore

        member this.HandleSpawnWorker(command: SpawnWorkerParticipant) =
            let replyTo = this.ActorCtx.Sender
            let selfRef = this.ActorCtx.Self
            let actorName =
                command.ActorName
                |> Option.filter (String.IsNullOrWhiteSpace >> not)
                |> Option.map _.Trim()
                |> Option.defaultValue (actorNameFromParticipantId "worker" command.Spec.ParticipantId)

            let child =
                this.ActorCtx.ActorOf(
                    Props.Create(fun () -> CodexWorkerActor(messageFabric, command.Spec, nodeAddress)),
                    actorName
                )

            task {
                try
                    let! registered =
                        child.Ask<WorkerParticipantRegistered>({ RequestedAtUtc = Some DateTimeOffset.UtcNow }, TimeSpan.FromSeconds 10.0)

                    replyTo.Tell(
                        { ForemanParticipantId = spec.ParticipantId
                          Worker = registered },
                        selfRef
                    )
                with ex ->
                    replyTo.Tell(Status.Failure ex, selfRef)
            }
            |> ignore

        member this.HandleRunRuntimeCycle(command: RunRuntimeCycle) =
            let replyTo = this.ActorCtx.Sender
            let selfRef = this.ActorCtx.Self

            task {
                try
                    let! registered = this.RegisterAsync()
                    let runtimeOptions = this.RuntimeOptions command
                    let! result = RuntimeMessageFabricCycle.runSingleCycleAsync messageFabric runtimeOptions CancellationToken.None

                    replyTo.Tell(
                        { ParticipantId = registered.ParticipantId
                          ActorPath = registered.ActorPath
                          NodeAddress = registered.NodeAddress
                          Result = result },
                        selfRef
                    )
                with ex ->
                    replyTo.Tell(Status.Failure ex, selfRef)
            }
            |> ignore

    /// Build Akka props for a codex.fs worker actor shell.
    let props messageFabric spec nodeAddress =
        Props.Create(fun () -> CodexWorkerActor(messageFabric, spec, nodeAddress))

    /// Spawn a codex.fs worker actor shell on a PTCS ActorFabric-owned ActorSystem.
    let spawnWorker (fabric: CommSpaActorFabric) messageFabric actorName spec =
        if isNull (box fabric) then
            nullArg (nameof fabric)

        if isNull (box messageFabric) then
            nullArg (nameof messageFabric)

        fabric.System.ActorOf(props messageFabric spec fabric.NodeAddress, actorName)
