using System;
using System.Threading;
using System.Threading.Tasks;
using Chess.Domain;
using Chess.Online;
using Xunit;

namespace Chess.Tests;

public sealed class OnlineMatchTransportAdapterTests
{
    [Fact]
    public async Task SubmitMoveAsync_MapsCoordinatesAndPromotionToken()
    {
        OnlineSubmitMoveRequest? capturedRequest = null;
        var httpClient = new StubOnlineMatchHttpClient
        {
            SubmitMoveAsyncHandler = (request, _) =>
            {
                capturedRequest = request;
                var snapshot = new OnlineMatchSnapshot(
                    MatchId: request.MatchId!,
                    SideToMove: OnlineMatchProtocolConstants.JoinerSeat,
                    MoveNumber: 2,
                    Board:
                    [
                        "rnbqkbnr",
                        "pppppppp",
                        "........",
                        "........",
                        "....P...",
                        "........",
                        "PPPP.PPP",
                        "RNBQKBNR"
                    ]);

                return Task.FromResult(
                    OnlineOperationResult<OnlineSubmitMoveResponse>.Success(
                        new OnlineSubmitMoveResponse(true, snapshot)));
            }
        };
        var adapter = new OnlineMatchTransportAdapter(httpClient);
        var credentials = new OnlineMatchCredentials("match-1", "token-1", PieceColor.White);

        var result = await adapter.SubmitMoveAsync(
            credentials,
            new Square(4, 1),
            new Square(4, 3),
            PieceType.Queen);

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedRequest);
        Assert.Equal("match-1", capturedRequest!.MatchId);
        Assert.Equal("token-1", capturedRequest.PlayerToken);
        Assert.Equal("e2", capturedRequest.From);
        Assert.Equal("e4", capturedRequest.To);
        Assert.Equal("Q", capturedRequest.Promotion);
    }

    [Fact]
    public async Task SubmitMoveAsync_WithNullCredentials_Throws()
    {
        var adapter = new OnlineMatchTransportAdapter(new StubOnlineMatchHttpClient());

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await adapter.SubmitMoveAsync(
                credentials: null!,
                fromSquare: new Square(4, 1),
                toSquare: new Square(4, 3)));
    }

    private sealed class StubOnlineMatchHttpClient : IOnlineMatchHttpClient
    {
        private static readonly OnlineUserError UnsupportedError = new("unsupported", "Unsupported in test.", OnlineUserAction.None);

        public Func<CancellationToken, Task<OnlineOperationResult<OnlineCreateMatchResponse>>>? CreateMatchAsyncHandler { get; set; }

        public Func<string, CancellationToken, Task<OnlineOperationResult<OnlineJoinMatchResponse>>>? JoinMatchAsyncHandler { get; set; }

        public Func<OnlineSubmitMoveRequest, CancellationToken, Task<OnlineOperationResult<OnlineSubmitMoveResponse>>>? SubmitMoveAsyncHandler { get; set; }

        public Func<OnlineSnapshotRequest, CancellationToken, Task<OnlineOperationResult<OnlineMatchSnapshot>>>? GetSnapshotAsyncHandler { get; set; }

        public Task<OnlineOperationResult<OnlineCreateMatchResponse>> CreateMatchAsync(CancellationToken cancellationToken = default)
        {
            if (CreateMatchAsyncHandler is not null)
            {
                return CreateMatchAsyncHandler(cancellationToken);
            }

            return Task.FromResult(OnlineOperationResult<OnlineCreateMatchResponse>.Failure(UnsupportedError));
        }

        public Task<OnlineOperationResult<OnlineJoinMatchResponse>> JoinMatchAsync(
            string joinCode,
            CancellationToken cancellationToken = default)
        {
            if (JoinMatchAsyncHandler is not null)
            {
                return JoinMatchAsyncHandler(joinCode, cancellationToken);
            }

            return Task.FromResult(OnlineOperationResult<OnlineJoinMatchResponse>.Failure(UnsupportedError));
        }

        public Task<OnlineOperationResult<OnlineSubmitMoveResponse>> SubmitMoveAsync(
            OnlineSubmitMoveRequest request,
            CancellationToken cancellationToken = default)
        {
            if (SubmitMoveAsyncHandler is not null)
            {
                return SubmitMoveAsyncHandler(request, cancellationToken);
            }

            return Task.FromResult(OnlineOperationResult<OnlineSubmitMoveResponse>.Failure(UnsupportedError));
        }

        public Task<OnlineOperationResult<OnlineMatchSnapshot>> GetSnapshotAsync(
            OnlineSnapshotRequest request,
            CancellationToken cancellationToken = default)
        {
            if (GetSnapshotAsyncHandler is not null)
            {
                return GetSnapshotAsyncHandler(request, cancellationToken);
            }

            return Task.FromResult(OnlineOperationResult<OnlineMatchSnapshot>.Failure(UnsupportedError));
        }
    }
}
