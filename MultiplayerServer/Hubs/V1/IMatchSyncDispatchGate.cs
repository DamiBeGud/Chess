namespace MultiplayerServer.Hubs.V1;

public interface IMatchSyncDispatchGate
{
    ValueTask<IAsyncDisposable> AcquireAsync(string matchId, CancellationToken cancellationToken);
}
