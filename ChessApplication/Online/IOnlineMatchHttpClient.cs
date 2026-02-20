using System.Threading;
using System.Threading.Tasks;

namespace Chess.Online;

/// <summary>
/// IOnlineMatchHttpClient defines a contract within the Online module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include OnlineMatchTransportAdapter (Online), MultiplayerServerHttpClient (Online), OnlineMatchSessionService (Online).
/// Key collaborators are Implementations include MultiplayerServerHttpClient (Online).
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> OnlineMatchTransportAdapter (Online), MultiplayerServerHttpClient (Online), OnlineMatchSessionService (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include MultiplayerServerHttpClient (Online).</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
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
