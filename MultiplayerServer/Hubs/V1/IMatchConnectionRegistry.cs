namespace MultiplayerServer.Hubs.V1;

public interface IMatchConnectionRegistry
{
    void AddSubscription(string connectionId, string matchId);
    bool RemoveSubscription(string connectionId, string matchId);
    bool IsSubscribed(string connectionId, string matchId);
    IReadOnlyList<string> RemoveConnection(string connectionId);
}
