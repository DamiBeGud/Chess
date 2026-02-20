using System.Threading;
using System.Threading.Tasks;
using Chess.Domain;

namespace Chess.Online;

public interface IOnlineMatchSessionCommands
{
    Task<OnlineOperationResult<OnlineCreatedMatch>> CreateMatchAsync(CancellationToken cancellationToken = default);

    Task<OnlineOperationResult<OnlineJoinedMatch>> JoinMatchAsync(string joinCode, CancellationToken cancellationToken = default);

    Task<OnlineOperationResult<OnlineMatchSnapshot>> SubmitMoveAsync(
        Square fromSquare,
        Square toSquare,
        PieceType? promotionPieceType = null,
        CancellationToken cancellationToken = default);

    Task<OnlineOperationResult<OnlineMatchSnapshot>> RequestResyncAsync(CancellationToken cancellationToken = default);

    Task LeaveMatchAsync(CancellationToken cancellationToken = default);
}
