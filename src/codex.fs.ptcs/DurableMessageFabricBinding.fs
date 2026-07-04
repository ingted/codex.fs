namespace CodexFs.Ptcs

open System
open System.Threading
open System.Threading.Tasks
open PulseTrade.Comm.Spa

/// Thin wrappers over PTCS durable MessageFabric admission.
module DurableMessageFabricBinding =

    /// Durable PTCS fabric instances owned by one codex.fs host profile.
    type DurableFabric =
        { /// PTCS durable ingress facade used for ticket health/query.
          Ingress: DurableIngress
          /// PTCS durable MessageFabric wrapper used for ticketed mutations.
          Fabric: CommSpaDurableMessageFabric }

    /// Non-secret provider proof summary for durable readiness gates.
    type DurableProviderProof =
        { /// PTCS durable ingress mode reported by health.
          Mode: DurableIngressMode
          /// PTCS durable ingress profile id.
          ProfileId: string
          /// True only when PTCS reports crash-durable backing.
          IsCrashDurable: bool
          /// True only when PTCS reports delivery retry support.
          SupportsDeliveryRetry: bool
          /// Current pending task count.
          PendingCount: int
          /// Current dead-letter task count.
          DeadLetterCount: int
          /// Provider implementation kind supplied to PTCS provider proof.
          ImplementationKind: string
          /// Missing production requirements returned by PTCS provider proof.
          MissingRequirements: string list
          /// True when PTCS provider proof accepts this provider as sharded durable delivery.
          SatisfiesShardedDeliveryProvider: bool }

    /// Agent-task handoff request accepted through PTCS durable MessageFabric.
    type DurableAgentTask =
        { /// Stable task id supplied by the caller.
          AgentTaskId: string
          /// Optional parent request id used for causality.
          ParentRequestId: string option
          /// Sender participant id.
          FromParticipantId: string
          /// Target session/worker participant id.
          ToParticipantId: string
          /// Non-secret task intent.
          Intent: string
          /// Task body to deliver to the target worker inbox.
          Body: string
          /// Optional content type. Defaults to `text/markdown`.
          ContentType: string option
          /// Non-secret PTCS tags.
          Tags: string list
          /// Optional direct reply participant id.
          ReplyToParticipantId: string option
          /// Optional reply/result correlation id.
          CorrelationId: string option
          /// Optional operation id for task identity.
          OperationId: string option
          /// Optional idempotency key for PTCS durable ingress.
          IdempotencyKey: string option
          /// Optional entity id, usually the target session id or participant id.
          EntityId: string option
          /// Optional PTCS result vault profile id.
          VaultProfileId: string option
          /// Optional result payload limit.
          ResultMaxBytes: int64 option
          /// Optional caller-created timestamp.
          CreatedAtUtc: DateTimeOffset option
          /// Optional task deadline.
          DeadlineAtUtc: DateTimeOffset option }

    /// Build a volatile durable PTCS MessageFabric for local tests and demos.
    ///
    /// This uses PTCS ticketed durable admission, but the provider proof intentionally
    /// does not satisfy production sharded crash-durable delivery requirements.
    let createVolatileDurableFabric () =
        let hub = CommHub.createEmpty ()
        let ingressOptions =
            CommSpaDurableIngressOptions.volatileLocal ()
            |> CommSpaDurableIngressOptions.normalize
        let ingress = CommSpaDurableIngress.createVolatile ingressOptions
        let fabric = CommSpaMessageFabric.createDurable hub ingress
        { Ingress = ingress
          Fabric = fabric }

    /// Build a durable fabric from a caller-owned hub and durable ingress.
    let createDurableFabric hub ingress =
        { Ingress = ingress
          Fabric = CommSpaMessageFabric.createDurable hub ingress }

    /// Query PTCS durable ingress health.
    let healthAsync (durable: DurableFabric) (cancellationToken: CancellationToken) =
        durable.Ingress.HealthAsync cancellationToken

    /// Build PTCS provider proof diagnostics for one implementation kind.
    let providerProofAsync implementationKind durable cancellationToken =
        task {
            let! health = healthAsync durable cancellationToken
            let missing = DurableIngressProviderProof.missingRequirements implementationKind health

            return
                { Mode = health.Mode
                  ProfileId = health.ProfileId
                  IsCrashDurable = health.IsCrashDurable
                  SupportsDeliveryRetry = health.SupportsDeliveryRetry
                  PendingCount = health.PendingCount
                  DeadLetterCount = health.DeadLetterCount
                  ImplementationKind = implementationKind
                  MissingRequirements = missing
                  SatisfiesShardedDeliveryProvider =
                    DurableIngressProviderProof.satisfiesShardedDeliveryProvider implementationKind health }
        }

    /// Build provider proof for PTCS volatile local durable ingress.
    let volatileProviderProofAsync durable cancellationToken =
        providerProofAsync DurableIngressProviderProof.VolatileLocalImplementationKind durable cancellationToken

    /// Register the session participant through PTCS durable admission.
    let registerParticipantAsync (durable: DurableFabric) (binding: MessageFabricBinding.SessionBinding) (registration: MessageFabricBinding.ParticipantRegistration) =
        let args: RegisterParticipantArgs =
            { ParticipantId = binding.ParticipantId
              DisplayName = registration.DisplayName
              Kind = registration.Kind
              Labels = registration.Labels }

        durable.Fabric.RegisterParticipantDurableAsync args

    /// Submit one direct agent task through PTCS durable MessageFabric.
    let submitAgentTaskAsync (durable: DurableFabric) (task: DurableAgentTask) =
        let replyTarget =
            { Mode =
                match task.ReplyToParticipantId with
                | Some _ -> MessageFabricAgentTaskReplyMode.MessageFabric
                | None -> MessageFabricAgentTaskReplyMode.ResultVault
              ParticipantId = task.ReplyToParticipantId
              CorrelationId = task.CorrelationId
              ResultRouteKey = None }

        let envelope: MessageFabricAgentTaskEnvelope =
            { AgentTaskId = task.AgentTaskId
              ParentRequestId = task.ParentRequestId
              FromParticipantId = task.FromParticipantId
              ToParticipantId = task.ToParticipantId
              Intent = task.Intent
              Body = task.Body
              ContentType = task.ContentType |> Option.orElse (Some "text/markdown")
              Tags = task.Tags
              ReplyTo = replyTarget
              OperationId = task.OperationId
              IdempotencyKey = task.IdempotencyKey
              PayloadHash = None
              EntityId = task.EntityId
              VaultProfileId = task.VaultProfileId
              ResultMaxBytes = task.ResultMaxBytes
              CreatedAtUtc = task.CreatedAtUtc
              DeadlineAtUtc = task.DeadlineAtUtc }

        durable.Fabric.SubmitAgentTaskDurableAsync envelope

    /// Poll the durable MessageFabric inbox read path.
    let pollInboxAsync (durable: DurableFabric) binding after =
        durable.Fabric.PollInboxAsync(MessageFabricBinding.inboxQuery binding after)

    /// Acknowledge a processed inbox cursor through durable admission.
    let ackInboxAsync (durable: DurableFabric) (binding: MessageFabricBinding.SessionBinding) cursor =
        let args: MessageFabricInboxAck =
            { ParticipantId = binding.ParticipantId
              Cursor = cursor }

        durable.Fabric.AckDurableAsync args

    /// Query a PTCS durable task ticket.
    let queryTicketAsync (durable: DurableFabric) ticketId cancellationToken =
        durable.Ingress.QueryAsync ticketId cancellationToken
