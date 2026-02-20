using System.Threading;
using System.Threading.Tasks;
using Chess.Domain;

namespace Chess.Online;

/// <summary>
/// IOnlineMatchTransportAdapter defines a contract within the Online module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include OnlineMatchSessionService (Online), OnlineMatchTransportAdapter (Online).
/// Key collaborators are Implementations include OnlineMatchTransportAdapter (Online).
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> OnlineMatchSessionService (Online), OnlineMatchTransportAdapter (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include OnlineMatchTransportAdapter (Online).</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
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
