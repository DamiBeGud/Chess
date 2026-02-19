namespace MultiplayerServer.Contracts.V1;

public static class MatchProtocolConstants
{
    // MS-002 deterministic rule: creator is always White and first successful joiner is always Black.
    public const string CreatorSeat = "White";
    public const string JoinerSeat = "Black";

    // MS-004 transport event names.
    public const string EventMatchSnapshot = "match.snapshot";
    public const string EventMatchUpdated = "match.updated";
    public const string EventMatchError = "match.error";
    public const string EventMatchPresenceChanged = "match.presenceChanged";
    public const string EventMatchEnded = "match.ended";

    public const string ErrorJoinCodeRequired = "join_code_required";
    public const string ErrorMatchIdRequired = "match_id_required";
    public const string ErrorInvalidMatchIdFormat = "invalid_match_id_format";
    public const string ErrorPlayerTokenRequired = "player_token_required";
    public const string ErrorInvalidPlayerTokenFormat = "invalid_player_token_format";
    public const string ErrorMoveCoordinatesRequired = "move_coordinates_required";
    public const string ErrorMatchNotFound = "match_not_found";
    public const string ErrorMatchNotReady = "match_not_ready";
    public const string ErrorMatchFull = "match_full";
    public const string ErrorInvalidPlayerToken = "invalid_player_token";
    public const string ErrorInvalidPromotion = "invalid_promotion";
    public const string ErrorOutOfTurn = "out_of_turn";
    public const string ErrorIllegalMove = "illegal_move";
    public const string ErrorGraceExpired = "grace_expired";
    public const string ErrorUnauthorizedResume = "unauthorized_resume";
    public const string ErrorSeatNotReconnectable = "seat_not_reconnectable";
    public const string ErrorMatchAlreadyEnded = "match_already_ended";

    // MS-004 transport-specific validation/authorization codes.
    public const string ErrorTransportForbidden = "transport_forbidden";
    public const string ErrorTransportNotSubscribed = "transport_not_subscribed";

    // MS-005 match lifecycle state values.
    public const string MatchStatusInProgress = "in_progress";
    public const string MatchStatusEnded = "ended";
    public const string MatchResolutionForfeit = "forfeit";
    public const string MatchResolutionDraw = "draw";
}
