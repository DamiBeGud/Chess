using MultiplayerServer.Application.Matches;

namespace MultiplayerServer.Hubs.V1;

public sealed class V1MatchLifecycleEventPublisher(
    IMatchSyncDispatchGate dispatchGate,
    IMatchSyncPublisher syncPublisher,
    IMatchSyncEventIdGenerator eventIdGenerator) : IMatchLifecycleEventPublisher
{
    public async Task PublishMatchEndedAsync(MatchSnapshot snapshot, CancellationToken cancellationToken)
    {
        await using var dispatchLease = await dispatchGate.AcquireAsync(snapshot.MatchId, cancellationToken);
        await syncPublisher.PublishMatchEndedAsync(
            snapshot,
            eventIdGenerator.Generate(),
            cancellationToken);
    }
}
