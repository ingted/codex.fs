namespace CodexFs.Ptcs

open System
open CodexFs.Domain
open PulseTrade.Comm.Spa

/// Compile-time PTCS package reference boundary.
module PtcsReference =

    /// First supported PTCS package id.
    [<Literal>]
    let pulseTradeCommSpaPackageId = "PulseTrade.Comm.Spa"

    /// First supported PTCS package version.
    [<Literal>]
    let pulseTradeCommSpaVersion = "0.2.5-beta71"

    /// Exact first supported PTCS package version range.
    [<Literal>]
    let pulseTradeCommSpaVersionRange = "[0.2.5-beta71]"

    /// FAkka.Argu version aligned with the PTCS beta71 dependency graph.
    [<Literal>]
    let fAkkaArguVersionRange = "[10.1.301]"

    /// Concrete PTCS MessageFabric type used by later binding slices.
    let messageFabricType: Type = typeof<CommSpaMessageFabric>

    /// Concrete PTCS ActorFabric options type used by later host slices.
    let actorFabricOptionsType: Type = typeof<CommSpaActorFabricOptions>

    /// Create a PTCS public message scope without hiding the concrete PTCS DU from callers.
    let publicScope channel =
        MessageFabricScope.Public channel

    /// Create a PTCS direct message scope without hiding the concrete PTCS DU from callers.
    let directScope participantId =
        MessageFabricScope.Direct participantId

    /// Convert PTCS message metadata to the core codex.fs message reference model.
    let toMessageRef cursor (message: MessageFabricEnvelope) =
        let toParticipantId, groupId =
            match message.Scope with
            | MessageFabricScope.Public _ -> None, None
            | MessageFabricScope.Direct participantId -> Some participantId, None
            | MessageFabricScope.Group id -> None, Some id

        { MessageId = message.MessageId
          Cursor = cursor
          FromParticipantId = message.FromParticipantId
          ToParticipantId = toParticipantId
          GroupId = groupId
          CorrelationId = None }
