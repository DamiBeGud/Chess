namespace MultiplayerServer.Contracts.V1;

public static class ServerRouteConventions
{
    public const string Health = "/health";
    public const string ApiV1Prefix = "/api/v1";
    public const string ApiV1Root = "/";
    public const string ApiV1Matches = "/matches";
    public const string ApiV1MatchesJoin = "/matches/join";
    public const string ApiV1MatchesMoves = "/matches/moves";
    public const string ApiV1MatchesSnapshot = "/matches/snapshot";
    public const string MatchHubV1 = "/hubs/v1/matches";
}
