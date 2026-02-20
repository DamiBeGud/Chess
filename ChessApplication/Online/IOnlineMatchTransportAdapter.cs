using System.Threading;
using System.Threading.Tasks;
using Chess.Domain;

namespace Chess.Online;

internal interface IOnlineMatchTransportAdapter
{
    Task<OnlineOperationResult<OnlineCreateMatchResponse>> CreateMatchAsync(CancellationToken cancellationToken = default);

    Task<OnlineOperationResult<OnlineJoinMatchResponse>> JoinMatchAsync(string joinCode, CancellationToken cancellationToken = default);

    Task<OnlineOperationResult<OnlineSubmitMoveResponse>> SubmitMoveAsync(
        OnlineMatchCredentials credentials,
        Square fromSquare,
        Square toSquare,
        PieceType? promotionPieceType = null,
        CancellationToken cancellationToken = default);

    Task<OnlineOperationResult<OnlineMatchSnapshot>> GetSnapshotAsync(
        OnlineMatchCredentials credentials,
        CancellationToken cancellationToken = default);
}
