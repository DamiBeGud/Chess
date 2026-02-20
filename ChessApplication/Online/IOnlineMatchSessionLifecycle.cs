using System;
using System.Threading;
using System.Threading.Tasks;
using Chess.Domain;

namespace Chess.Online;

/// <summary>
/// IOnlineMatchSessionLifecycle defines a contract within the Online module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include IOnlineMatchSessionService (Online).
/// Key collaborators are Implementations include IOnlineMatchSessionService (Online); related base contracts include IAsyncDisposable.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> IOnlineMatchSessionService (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include IOnlineMatchSessionService (Online); related base contracts include IAsyncDisposable.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
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
