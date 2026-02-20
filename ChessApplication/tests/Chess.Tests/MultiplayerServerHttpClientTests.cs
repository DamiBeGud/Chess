using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Chess.Online;
using Xunit;

namespace Chess.Tests;

public sealed class MultiplayerServerHttpClientTests
{
    [Fact]
    public async Task CreateMatchAsync_CallsExpectedRouteAndMapsResponse()
    {
        var handler = new RecordingHttpMessageHandler(
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new OnlineCreateMatchResponse(
                        "58b02d5d9d5c43cc9a0314a8f1f4a14c",
                        "8Q2KLM",
                        "e2ef01fdb8517a608fcf4862ef35f6a1"))
            }));
        var client = CreateClient(handler);

        var result = await client.CreateMatchAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("/api/v1/matches", handler.LastRequestPath);
        Assert.Equal(HttpMethod.Post, handler.LastRequestMethod);
    }

    [Fact]
    public async Task JoinMatchAsync_SendsJoinCodePayloadAndMapsSeat()
    {
        var handler = new RecordingHttpMessageHandler(
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new OnlineJoinMatchResponse(
                        "58b02d5d9d5c43cc9a0314a8f1f4a14c",
                        OnlineMatchProtocolConstants.JoinerSeat,
                        "a8dc535a026f085f0fc9d57fceab5902"))
            }));
        var client = CreateClient(handler);

        var result = await client.JoinMatchAsync("8Q2KLM");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("/api/v1/matches/join", handler.LastRequestPath);
        Assert.Contains("\"joinCode\":\"8Q2KLM\"", handler.LastRequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitMoveAsync_SendsMoveContractAndMapsSnapshot()
    {
        var snapshot = CreateSnapshot();
        var handler = new RecordingHttpMessageHandler(
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new OnlineSubmitMoveResponse(true, snapshot))
            }));
        var client = CreateClient(handler);

        var result = await client.SubmitMoveAsync(
            new OnlineSubmitMoveRequest(
                "58b02d5d9d5c43cc9a0314a8f1f4a14c",
                "e2ef01fdb8517a608fcf4862ef35f6a1",
                "e2",
                "e4"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("/api/v1/matches/moves", handler.LastRequestPath);
        Assert.Contains("\"from\":\"e2\"", handler.LastRequestBody, StringComparison.Ordinal);
        Assert.Contains("\"to\":\"e4\"", handler.LastRequestBody, StringComparison.Ordinal);
        Assert.Equal(snapshot.MatchId, result.Value!.Snapshot.MatchId);
    }

    [Fact]
    public async Task GetSnapshotAsync_MapsApiErrorByCode()
    {
        var handler = new RecordingHttpMessageHandler(
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = JsonContent.Create(
                    new OnlineApiErrorResponse(
                        OnlineMatchProtocolConstants.ErrorInvalidPlayerToken,
                        "Invalid player token."))
            }));
        var client = CreateClient(handler);

        var result = await client.GetSnapshotAsync(
            new OnlineSnapshotRequest(
                "58b02d5d9d5c43cc9a0314a8f1f4a14c",
                "bad-token"));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(OnlineMatchProtocolConstants.ErrorInvalidPlayerToken, result.Error!.Code);
        Assert.Equal(OnlineUserAction.Reconnect, result.Error.RecommendedAction);
        Assert.Equal("/api/v1/matches/snapshot", handler.LastRequestPath);
    }

    private static MultiplayerServerHttpClient CreateClient(RecordingHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8080")
        };
        return new MultiplayerServerHttpClient(httpClient, new OnlineErrorMapper());
    }

    private static OnlineMatchSnapshot CreateSnapshot()
    {
        return new OnlineMatchSnapshot(
            MatchId: "58b02d5d9d5c43cc9a0314a8f1f4a14c",
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
            ],
            Status: OnlineMatchProtocolConstants.MatchStatusInProgress,
            Resolution: null,
            WinnerSeat: null,
            Presence: new OnlineMatchPresence(
                new OnlineSeatPresence(OnlineMatchProtocolConstants.CreatorSeat, true, true, null, null),
                new OnlineSeatPresence(OnlineMatchProtocolConstants.JoinerSeat, true, true, null, null)));
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responseFactory;

        public RecordingHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public string LastRequestPath { get; private set; } = string.Empty;
        public HttpMethod LastRequestMethod { get; private set; } = HttpMethod.Get;
        public string LastRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestPath = request.RequestUri?.PathAndQuery ?? string.Empty;
            LastRequestMethod = request.Method;
            LastRequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return await _responseFactory(request);
        }
    }
}
