using System;
using System.Threading;
using System.Threading.Tasks;
using Chess.Domain;

namespace Chess.Online;

public sealed class NoOpOnlineMatchSessionService : IOnlineMatchSessionService
{
    private static readonly OnlineUserError UnavailableError = new(
        "online_unavailable",
        "Online multiplayer is unavailable in this build.",
        OnlineUserAction.None,
        IsTerminal: true);

    public static NoOpOnlineMatchSessionService Instance { get; } = new();

    private NoOpOnlineMatchSessionService()
    {
    }

    public event EventHandler? SessionStateChanged
    {
        add { }
        remove { }
    }

    public event EventHandler<OnlineUserError>? SessionError
    {
        add { }
        remove { }
    }

    public bool IsInMatch => false;
    public bool IsConnected => false;
    public string? MatchId => null;
    public string? JoinCode => null;
    public PieceColor? Seat => null;
    public OnlineMatchSnapshot? CurrentSnapshot => null;
    public GameState? CurrentGameState => null;
    public long LastSequence => 0;

    public Task<OnlineOperationResult<OnlineCreatedMatch>> CreateMatchAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(OnlineOperationResult<OnlineCreatedMatch>.Failure(UnavailableError));
    }

    public Task<OnlineOperationResult<OnlineJoinedMatch>> JoinMatchAsync(string joinCode, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(OnlineOperationResult<OnlineJoinedMatch>.Failure(UnavailableError));
    }

    public Task<OnlineOperationResult<OnlineResumedMatch>> ResumeMatchAsync(
        string matchId,
        string playerToken,
        PieceColor seat,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(OnlineOperationResult<OnlineResumedMatch>.Failure(UnavailableError));
    }

    public Task<OnlineOperationResult<OnlineMatchSnapshot>> SubmitMoveAsync(
        Square fromSquare,
        Square toSquare,
        PieceType? promotionPieceType = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(OnlineOperationResult<OnlineMatchSnapshot>.Failure(UnavailableError));
    }

    public Task<OnlineOperationResult<OnlineMatchSnapshot>> RecoverAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(OnlineOperationResult<OnlineMatchSnapshot>.Failure(UnavailableError));
    }

    public Task<OnlineOperationResult<OnlineMatchSnapshot>> RequestResyncAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(OnlineOperationResult<OnlineMatchSnapshot>.Failure(UnavailableError));
    }

    public Task SuspendRealtimeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task LeaveMatchAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
