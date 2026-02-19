namespace MultiplayerServer.Contracts.V1;

public static class MatchProtocolConstants
{
    // MS-002 deterministic rule: creator is always White and first successful joiner is always Black.
    public const string CreatorSeat = "White";
    public const string JoinerSeat = "Black";

    public const string ErrorJoinCodeRequired = "join_code_required";
    public const string ErrorMatchIdRequired = "match_id_required";
    public const string ErrorPlayerTokenRequired = "player_token_required";
    public const string ErrorMoveCoordinatesRequired = "move_coordinates_required";
    public const string ErrorMatchNotFound = "match_not_found";
    public const string ErrorMatchNotReady = "match_not_ready";
    public const string ErrorMatchFull = "match_full";
    public const string ErrorInvalidPlayerToken = "invalid_player_token";
    public const string ErrorInvalidPromotion = "invalid_promotion";
    public const string ErrorOutOfTurn = "out_of_turn";
    public const string ErrorIllegalMove = "illegal_move";
}
