namespace MultiplayerServer.Application.Matches;

public interface IMatchLifecycleEventPublisher
{
    Task PublishMatchEndedAsync(MatchSnapshot snapshot, CancellationToken cancellationToken);
}

public sealed class NullMatchLifecycleEventPublisher : IMatchLifecycleEventPublisher
{
    public Task PublishMatchEndedAsync(MatchSnapshot snapshot, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
