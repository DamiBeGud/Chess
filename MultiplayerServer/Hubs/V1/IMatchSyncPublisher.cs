using MultiplayerServer.Application.Matches;

namespace MultiplayerServer.Hubs.V1;

public interface IMatchSyncPublisher
{
    Task PublishMatchUpdatedAsync(
        MatchSnapshot snapshot,
        string eventId,
        CancellationToken cancellationToken);

    Task PublishMatchSnapshotToConnectionAsync(
        string connectionId,
        MatchSnapshot snapshot,
        string eventId,
        CancellationToken cancellationToken);

    Task PublishTransportErrorToConnectionAsync(
        string connectionId,
        string matchId,
        string eventId,
        string code,
        string message,
        CancellationToken cancellationToken);
}
