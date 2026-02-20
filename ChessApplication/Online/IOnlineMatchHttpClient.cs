using System.Threading;
using System.Threading.Tasks;

namespace Chess.Online;

public interface IOnlineMatchHttpClient
{
    Task<OnlineOperationResult<OnlineCreateMatchResponse>> CreateMatchAsync(CancellationToken cancellationToken = default);

    Task<OnlineOperationResult<OnlineJoinMatchResponse>> JoinMatchAsync(string joinCode, CancellationToken cancellationToken = default);

    Task<OnlineOperationResult<OnlineSubmitMoveResponse>> SubmitMoveAsync(
        OnlineSubmitMoveRequest request,
        CancellationToken cancellationToken = default);

    Task<OnlineOperationResult<OnlineMatchSnapshot>> GetSnapshotAsync(
        OnlineSnapshotRequest request,
        CancellationToken cancellationToken = default);
}
