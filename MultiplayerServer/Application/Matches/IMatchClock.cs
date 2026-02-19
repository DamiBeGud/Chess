namespace MultiplayerServer.Application.Matches;

public interface IMatchClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemMatchClock : IMatchClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
