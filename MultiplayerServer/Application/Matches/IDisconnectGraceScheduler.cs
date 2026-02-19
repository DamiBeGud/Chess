namespace MultiplayerServer.Application.Matches;

public interface IDisconnectGraceScheduler : IDisposable
{
    void ScheduleSeatGraceTimeout(
        string matchId,
        string seat,
        DateTimeOffset dueUtc,
        Func<CancellationToken, Task> onTimeoutAsync);

    void CancelSeatGraceTimeout(string matchId, string seat);

    void CancelMatchGraceTimeouts(string matchId);
}
