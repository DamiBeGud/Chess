using System;
using System.Threading;
using System.Threading.Tasks;
using Chess.Domain;

namespace Chess.Online;

public interface IOnlineMatchSessionLifecycle : IAsyncDisposable
{
    Task<OnlineOperationResult<OnlineResumedMatch>> ResumeMatchAsync(
        string matchId,
        string playerToken,
        PieceColor seat,
        CancellationToken cancellationToken = default);

    Task<OnlineOperationResult<OnlineMatchSnapshot>> RecoverAsync(CancellationToken cancellationToken = default);

    Task SuspendRealtimeAsync(CancellationToken cancellationToken = default);
}
