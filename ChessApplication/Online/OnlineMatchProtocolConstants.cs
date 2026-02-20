namespace Chess.Online;

/// <summary>
/// OnlineMatchProtocolConstants is a concrete type within the Online module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include OnlineErrorMapper (Online), OnlineSnapshotGameStateMapper (Online), SignalROnlineMatchRealtimeClient (Online).
/// No constructor-injected collaborators were detected in this declaration.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> OnlineErrorMapper (Online), OnlineSnapshotGameStateMapper (Online), SignalROnlineMatchRealtimeClient (Online), MultiplayerServerHttpClient (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> No constructor-injected collaborators were detected in this declaration.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public static class OnlineMatchProtocolConstants
{
    public const string MatchesRoute = "/api/v1/matches";
    public const string JoinRoute = "/api/v1/matches/join";
    public const string MovesRoute = "/api/v1/matches/moves";
    public const string SnapshotRoute = "/api/v1/matches/snapshot";
    public const string MatchHubRoute = "/hubs/v1/matches";

    public const string CreatorSeat = "White";
    public const string JoinerSeat = "Black";

    public const string MatchStatusInProgress = "in_progress";
    public const string MatchStatusEnded = "ended";
    public const string MatchResolutionForfeit = "forfeit";
    public const string MatchResolutionDraw = "draw";

    public const string EventMatchSnapshot = "match.snapshot";
    public const string EventMatchUpdated = "match.updated";
    public const string EventMatchPresenceChanged = "match.presenceChanged";
    public const string EventMatchEnded = "match.ended";
    public const string EventMatchError = "match.error";

    public const string ErrorJoinCodeRequired = "join_code_required";
    public const string ErrorMatchIdRequired = "match_id_required";
    public const string ErrorPlayerTokenRequired = "player_token_required";
    public const string ErrorMoveCoordinatesRequired = "move_coordinates_required";
    public const string ErrorInvalidPromotion = "invalid_promotion";
    public const string ErrorInvalidPlayerToken = "invalid_player_token";
    public const string ErrorUnauthorizedResume = "unauthorized_resume";
    public const string ErrorMatchNotFound = "match_not_found";
    public const string ErrorMatchNotReady = "match_not_ready";
    public const string ErrorMatchFull = "match_full";
    public const string ErrorOutOfTurn = "out_of_turn";
    public const string ErrorIllegalMove = "illegal_move";
    public const string ErrorSeatNotReconnectable = "seat_not_reconnectable";
    public const string ErrorGraceExpired = "grace_expired";
    public const string ErrorMatchAlreadyEnded = "match_already_ended";
    public const string ErrorTransportNotSubscribed = "transport_not_subscribed";
}
