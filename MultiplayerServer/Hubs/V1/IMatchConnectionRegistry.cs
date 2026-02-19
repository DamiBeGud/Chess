namespace MultiplayerServer.Hubs.V1;

public sealed record MatchConnectionSubscription(
    string MatchId,
    string PlayerToken);

public interface IMatchConnectionRegistry
{
    void AddSubscription(string connectionId, string matchId, string playerToken);
    MatchConnectionSubscription? RemoveSubscription(string connectionId, string matchId);
    bool IsSubscribed(string connectionId, string matchId);
    IReadOnlyList<MatchConnectionSubscription> RemoveConnection(string connectionId);
}
