namespace MultiplayerServer.Contracts.V1;

public static class MatchProtocolConstants
{
    // MS-002 deterministic rule: creator is always White and first successful joiner is always Black.
    public const string CreatorSeat = "White";
    public const string JoinerSeat = "Black";

    public const string ErrorJoinCodeRequired = "join_code_required";
    public const string ErrorMatchNotFound = "match_not_found";
    public const string ErrorMatchFull = "match_full";
}
