namespace CodexFs.Ptcs

open System
open System.Threading
open System.Threading.Tasks
open CodexFs.Domain
open PulseTrade.Comm.Spa

/// Thin wrappers over PTCS MessageFabric session operations.
module MessageFabricBinding =

    /// Session-level MessageFabric inbox settings.
    type SessionBinding =
        { /// PTCS participant id owned by this session.
          ParticipantId: string
          /// Optional participant id used when sending replies.
          ReplyParticipantId: string option
          /// Optional group id this session participates in.
          GroupId: string option
          /// Maximum messages returned by poll/wait/drain.
          InboxLimit: int
          /// Include public channel messages when polling.
          IncludePublic: bool
          /// Include group messages when polling.
          IncludeGroups: bool }

    /// Options for registering a session participant.
    type ParticipantRegistration =
        { /// Display name shown by PTCS surfaces.
          DisplayName: string option
          /// PTCS participant kind, usually `agent`.
          Kind: string option
          /// Non-secret labels for UI/provenance filtering.
          Labels: string list option }

    /// Outbound message request from codex.fs through PTCS.
    type OutboundMessage =
        { /// Sender participant id.
          FromParticipantId: string
          /// PTCS scope for public/direct/group delivery.
          Scope: MessageFabricScope
          /// Message body.
          Body: string
          /// Non-secret tags.
          Tags: string list
          /// Optional idempotency/correlation id.
          CorrelationId: string option
          /// Optional caller-supplied created timestamp.
          CreatedAtUtc: DateTimeOffset option }

    /// Default inbox limit for session bindings.
    [<Literal>]
    let defaultInboxLimit = 20

    /// Build default binding settings for one session participant.
    let defaultBinding participantId =
        { ParticipantId = participantId
          ReplyParticipantId = None
          GroupId = None
          InboxLimit = defaultInboxLimit
          IncludePublic = false
          IncludeGroups = false }

    /// Build default participant registration metadata.
    let defaultRegistration =
        { DisplayName = None
          Kind = Some "agent"
          Labels = Some [ "codex.fs"; "session" ] }

    /// Normalize inbox limit to the PTCS-supported range.
    let normalizedInboxLimit limit =
        if limit <= 0 then defaultInboxLimit else min limit 1000

    /// Create a local PTCS MessageFabric for tests and demos using the package default hub profile.
    let createLocalFabric () =
        CommHub.createEmpty ()
        |> CommSpaMessageFabric.create

    /// Register the session participant in PTCS.
    let registerParticipantAsync (fabric: CommSpaMessageFabric) (binding: SessionBinding) (registration: ParticipantRegistration) =
        let args: RegisterParticipantArgs =
            { ParticipantId = binding.ParticipantId
              DisplayName = registration.DisplayName
              Kind = registration.Kind
              Labels = registration.Labels }

        fabric.RegisterParticipantAsync args

    /// Upsert the configured group when `GroupId` is present.
    let tryUpsertConfiguredGroupAsync (fabric: CommSpaMessageFabric) (binding: SessionBinding) =
        match binding.GroupId with
        | Some groupId ->
            let args: MessageFabricGroupUpsert =
                { GroupId = groupId
                  DisplayName = Some groupId
                  ParticipantIds = [ binding.ParticipantId ]
                  Tags = Some [ "codex.fs"; "session-group" ] }

            task {
                let! groupView = fabric.UpsertGroupAsync args
                return Some groupView
            }
        | None -> Task.FromResult None

    /// Send one message through PTCS MessageFabric.
    let sendAsync (fabric: CommSpaMessageFabric) (message: OutboundMessage) =
        let args: MessageFabricAppend =
            { FromParticipantId = message.FromParticipantId
              Scope = message.Scope
              Body = message.Body
              Tags = message.Tags
              CorrelationId = message.CorrelationId
              CreatedAtUtc = message.CreatedAtUtc }

        fabric.SendAsync args

    /// Send a reply from the session participant to a direct target.
    let sendDirectReplyAsync (fabric: CommSpaMessageFabric) (binding: SessionBinding) targetParticipantId body tags correlationId =
        sendAsync
            fabric
            { FromParticipantId = binding.ReplyParticipantId |> Option.defaultValue binding.ParticipantId
              Scope = MessageFabricScope.Direct targetParticipantId
              Body = body
              Tags = tags
              CorrelationId = correlationId
              CreatedAtUtc = None }

    /// Build a PTCS inbox query for the binding.
    let inboxQuery (binding: SessionBinding) after =
        { ParticipantId = binding.ParticipantId
          After = after
          MaxMessages = normalizedInboxLimit binding.InboxLimit
          IncludePublic = binding.IncludePublic
          IncludeGroups = binding.IncludeGroups }

    /// Poll the PTCS inbox once.
    let pollInboxAsync (fabric: CommSpaMessageFabric) (binding: SessionBinding) after =
        fabric.PollInboxAsync(inboxQuery binding after)

    /// Wait for PTCS inbox messages with bounded timeout.
    let waitInboxAsync (fabric: CommSpaMessageFabric) (binding: SessionBinding) after timeout pollInterval cancellationToken =
        let args: MessageFabricInboxWait =
            { Query = inboxQuery binding after
              Timeout = timeout
              PollInterval = pollInterval
              CancellationToken = cancellationToken }

        fabric.WaitInboxAsync args

    /// Acknowledge a processed PTCS inbox cursor.
    let ackInboxAsync (fabric: CommSpaMessageFabric) (binding: SessionBinding) cursor =
        let args: MessageFabricInboxAck =
            { ParticipantId = binding.ParticipantId
              Cursor = cursor }

        fabric.AckAsync args

    /// Drain the current inbox batch and let PTCS ack the returned cursor.
    let drainInboxAsync (fabric: CommSpaMessageFabric) (binding: SessionBinding) after =
        fabric.DrainInboxAsync(inboxQuery binding after)

    /// Convert one PTCS envelope to core message reference.
    let envelopeToMessageRef (message: MessageFabricEnvelope) =
        PtcsReference.toMessageRef (Some message.MessageId) message

    /// Convert one PTCS inbox batch to core message references.
    let batchToMessageRefs (batch: MessageFabricInboxBatch) =
        batch.Messages |> List.map envelopeToMessageRef
