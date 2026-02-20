using System;
using System.Net.Http;
using System.Threading.Tasks;
using Chess.Domain;
using Chess.Online;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MultiplayerServer.Application.Matches;
using Xunit;

namespace Chess.Tests;

[Collection(nameof(OnlineComponentTestCollection))]
public sealed class OnlineMatchSessionServiceComponentTests
{
    [Fact]
    public async Task CreateJoinAndMove_HappyPathStaysAuthoritative()
    {
        await using var factory = new WebApplicationFactory<Program>();
        await using var creator = CreateHarness(factory);
        await using var joiner = CreateHarness(factory);

        var createResult = await creator.Session.CreateMatchAsync();
        Assert.True(createResult.IsSuccess);
        Assert.NotNull(createResult.Value);

        var joinResult = await joiner.Session.JoinMatchAsync(createResult.Value!.JoinCode);
        Assert.True(joinResult.IsSuccess);

        var moveResult = await creator.Session.SubmitMoveAsync(new Square(4, 1), new Square(4, 3));
        Assert.True(moveResult.IsSuccess);

        await WaitForConditionAsync(
            () => joiner.Session.CurrentSnapshot?.MoveNumber == 2,
            TimeSpan.FromSeconds(5));

        Assert.NotNull(creator.Session.CurrentSnapshot);
        Assert.NotNull(joiner.Session.CurrentSnapshot);
        Assert.Equal(2, creator.Session.CurrentSnapshot!.MoveNumber);
        Assert.Equal(2, joiner.Session.CurrentSnapshot!.MoveNumber);
        Assert.Equal('P', PieceAt(joiner.Session.CurrentSnapshot, "e4"));
        Assert.Equal('.', PieceAt(joiner.Session.CurrentSnapshot, "e2"));
    }

    [Fact]
    public async Task ResumeMatch_WithUnauthorizedToken_ReturnsExplicitAuthError()
    {
        await using var factory = new WebApplicationFactory<Program>();
        await using var owner = CreateHarness(factory);
        await using var unauthorized = CreateHarness(factory);

        var createResult = await owner.Session.CreateMatchAsync();
        Assert.True(createResult.IsSuccess);
        Assert.NotNull(createResult.Value);

        var resumeResult = await unauthorized.Session.ResumeMatchAsync(
            matchId: createResult.Value!.MatchId,
            playerToken: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            seat: PieceColor.White);

        Assert.False(resumeResult.IsSuccess);
        Assert.NotNull(resumeResult.Error);
        Assert.Equal(OnlineMatchProtocolConstants.ErrorInvalidPlayerToken, resumeResult.Error!.Code);
        Assert.Equal(OnlineUserAction.Reconnect, resumeResult.Error.RecommendedAction);
    }

    [Fact]
    public async Task SnapshotRecovery_AfterDisconnect_ResyncsDeterministically()
    {
        await using var factory = new WebApplicationFactory<Program>();
        await using var creator = CreateHarness(factory);
        await using var joiner = CreateHarness(factory);

        var createResult = await creator.Session.CreateMatchAsync();
        Assert.True(createResult.IsSuccess);
        Assert.NotNull(createResult.Value);
        var joinResult = await joiner.Session.JoinMatchAsync(createResult.Value!.JoinCode);
        Assert.True(joinResult.IsSuccess);

        var whiteMove = await creator.Session.SubmitMoveAsync(new Square(4, 1), new Square(4, 3));
        Assert.True(whiteMove.IsSuccess, $"{whiteMove.Error?.Code}:{whiteMove.Error?.Message}");

        await joiner.Session.SuspendRealtimeAsync();

        var recover = await joiner.Session.RecoverAsync();
        Assert.True(recover.IsSuccess);

        Assert.NotNull(joiner.Session.CurrentSnapshot);
        Assert.Equal(2, joiner.Session.CurrentSnapshot!.MoveNumber);
        Assert.Equal('P', PieceAt(joiner.Session.CurrentSnapshot, "e4"));
        Assert.Equal(OnlineMatchProtocolConstants.JoinerSeat, joiner.Session.CurrentSnapshot.SideToMove);
    }

    [Fact]
    public async Task MatchEndedEvent_PropagatesTerminalState()
    {
        await using var factory = CreateFactoryWithDisconnectPolicy(
            options =>
            {
                options.DisconnectGracePeriodSeconds = 1;
                options.AbandonmentResolution = MatchAbandonmentResolutionMode.Forfeit;
            });
        await using var creator = CreateHarness(factory);
        await using var joiner = CreateHarness(factory);

        var createResult = await creator.Session.CreateMatchAsync();
        Assert.True(createResult.IsSuccess);
        Assert.NotNull(createResult.Value);
        var joinResult = await joiner.Session.JoinMatchAsync(createResult.Value!.JoinCode);
        Assert.True(joinResult.IsSuccess);

        await creator.Session.SuspendRealtimeAsync();

        await WaitForConditionAsync(
            () => joiner.Session.CurrentSnapshot?.Status == OnlineMatchProtocolConstants.MatchStatusEnded,
            TimeSpan.FromSeconds(10));

        Assert.NotNull(joiner.Session.CurrentSnapshot);
        Assert.Equal(OnlineMatchProtocolConstants.MatchStatusEnded, joiner.Session.CurrentSnapshot!.Status);
        Assert.Equal(OnlineMatchProtocolConstants.MatchResolutionForfeit, joiner.Session.CurrentSnapshot.Resolution);
        Assert.Equal(OnlineMatchProtocolConstants.JoinerSeat, joiner.Session.CurrentSnapshot.WinnerSeat);
        Assert.NotNull(joiner.Session.CurrentGameState);
        Assert.Equal(GameStatus.BlackWin, joiner.Session.CurrentGameState!.Status);
    }

    private static WebApplicationFactory<Program> CreateFactoryWithDisconnectPolicy(
        Action<MatchDisconnectPolicyOptions> configurePolicy)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(
                builder =>
                {
                    builder.ConfigureServices(
                        services =>
                        {
                            services.PostConfigure(configurePolicy);
                        });
                });
    }

    private static SessionHarness CreateHarness(WebApplicationFactory<Program> factory)
    {
        var httpClient = new HttpClient(factory.Server.CreateHandler())
        {
            BaseAddress = factory.Server.BaseAddress
        };
        var errorMapper = new OnlineErrorMapper();
        var apiClient = new MultiplayerServerHttpClient(httpClient, errorMapper);
        var realtimeFactory = new SignalROnlineMatchRealtimeClientFactory(
            factory.Server.BaseAddress,
            options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            });

        var session = new OnlineMatchSessionService(
            apiClient,
            errorMapper,
            realtimeFactory,
            new OnlineSnapshotGameStateMapper(),
            new OnlineRealtimeEventReducer());

        return new SessionHarness(httpClient, session);
    }

    private static char PieceAt(OnlineMatchSnapshot snapshot, string coordinate)
    {
        var file = coordinate[0] - 'a';
        var rank = coordinate[1] - '1';
        var row = 7 - rank;
        return snapshot.Board[row][file];
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, TimeSpan timeout)
    {
        var until = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < until)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.True(condition(), "Condition did not become true within timeout.");
    }

    private sealed class SessionHarness : IAsyncDisposable
    {
        private readonly HttpClient _httpClient;

        public SessionHarness(HttpClient httpClient, OnlineMatchSessionService session)
        {
            _httpClient = httpClient;
            Session = session;
        }

        public OnlineMatchSessionService Session { get; }

        public async ValueTask DisposeAsync()
        {
            await Session.DisposeAsync();
            _httpClient.Dispose();
        }
    }
}
