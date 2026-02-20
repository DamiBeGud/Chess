using System;
using System.Threading;
using System.Threading.Tasks;
using Chess.Domain;

namespace Chess.Online;

public interface IOnlineMatchSessionService : IAsyncDisposable
{
    event EventHandler? SessionStateChanged;
    event EventHandler<OnlineUserError>? SessionError;

    bool IsInMatch { get; }
    bool IsConnected { get; }
    string? MatchId { get; }
    string? JoinCode { get; }
    PieceColor? Seat { get; }
    OnlineMatchSnapshot? CurrentSnapshot { get; }
    GameState? CurrentGameState { get; }
    long LastSequence { get; }

    Task<OnlineOperationResult<OnlineCreatedMatch>> CreateMatchAsync(CancellationToken cancellationToken = default);

    Task<OnlineOperationResult<OnlineJoinedMatch>> JoinMatchAsync(string joinCode, CancellationToken cancellationToken = default);

    Task<OnlineOperationResult<OnlineResumedMatch>> ResumeMatchAsync(
        string matchId,
        string playerToken,
        PieceColor seat,
        CancellationToken cancellationToken = default);

    Task<OnlineOperationResult<OnlineMatchSnapshot>> SubmitMoveAsync(
        Square fromSquare,
        Square toSquare,
        PieceType? promotionPieceType = null,
        CancellationToken cancellationToken = default);

    Task<OnlineOperationResult<OnlineMatchSnapshot>> RecoverAsync(CancellationToken cancellationToken = default);

    Task<OnlineOperationResult<OnlineMatchSnapshot>> RequestResyncAsync(CancellationToken cancellationToken = default);

    Task SuspendRealtimeAsync(CancellationToken cancellationToken = default);

    Task LeaveMatchAsync(CancellationToken cancellationToken = default);
}
