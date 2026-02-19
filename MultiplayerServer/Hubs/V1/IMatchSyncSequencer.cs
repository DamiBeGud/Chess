namespace MultiplayerServer.Hubs.V1;

public interface IMatchSyncSequencer
{
    bool TryReserve(string matchId, string eventId, out long sequence);
}
