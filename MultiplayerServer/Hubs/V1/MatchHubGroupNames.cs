namespace MultiplayerServer.Hubs.V1;

internal static class MatchHubGroupNames
{
    public static string ForMatch(string matchId)
    {
        return $"match:{matchId}";
    }
}
