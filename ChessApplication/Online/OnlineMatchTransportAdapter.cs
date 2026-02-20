using System;
using System.Threading;
using System.Threading.Tasks;
using Chess.Domain;

namespace Chess.Online;

internal sealed class OnlineMatchTransportAdapter : IOnlineMatchTransportAdapter
{
    private readonly IOnlineMatchHttpClient _httpClient;

    internal OnlineMatchTransportAdapter(IOnlineMatchHttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public Task<OnlineOperationResult<OnlineCreateMatchResponse>> CreateMatchAsync(CancellationToken cancellationToken = default)
    {
        return _httpClient.CreateMatchAsync(cancellationToken);
    }

    public Task<OnlineOperationResult<OnlineJoinMatchResponse>> JoinMatchAsync(
        string joinCode,
        CancellationToken cancellationToken = default)
    {
        return _httpClient.JoinMatchAsync(joinCode, cancellationToken);
    }

    public Task<OnlineOperationResult<OnlineSubmitMoveResponse>> SubmitMoveAsync(
        OnlineMatchCredentials credentials,
        Square fromSquare,
        Square toSquare,
        PieceType? promotionPieceType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        return _httpClient.SubmitMoveAsync(
            new OnlineSubmitMoveRequest(
                credentials.MatchId,
                credentials.PlayerToken,
                BoardGeometry.ToCoordinate(fromSquare),
                BoardGeometry.ToCoordinate(toSquare),
                ToPromotionToken(promotionPieceType)),
            cancellationToken);
    }

    public Task<OnlineOperationResult<OnlineMatchSnapshot>> GetSnapshotAsync(
        OnlineMatchCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        return _httpClient.GetSnapshotAsync(
            new OnlineSnapshotRequest(credentials.MatchId, credentials.PlayerToken),
            cancellationToken);
    }

    private static string? ToPromotionToken(PieceType? promotionPieceType)
    {
        return promotionPieceType switch
        {
            null => null,
            PieceType.Queen => "Q",
            PieceType.Rook => "R",
            PieceType.Bishop => "B",
            PieceType.Knight => "N",
            _ => null
        };
    }
}
