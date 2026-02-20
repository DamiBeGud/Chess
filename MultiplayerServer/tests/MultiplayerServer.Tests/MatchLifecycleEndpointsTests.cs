using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using MultiplayerServer.Application.Matches;
using MultiplayerServer.Contracts.V1;
using MultiplayerServer.Hubs.V1;
using MultiplayerServer.Transport.V1;
using System.Linq;

namespace MultiplayerServer.Tests;

public sealed class MatchLifecycleEndpointsTests
{
    private static readonly IMatchSyncDispatchGate NoOpDispatchGate = new NoOpMatchSyncDispatchGate();
    private static readonly IMatchSyncPublisher NoOpPublisher = new NoOpMatchSyncPublisher();
    private static readonly IMatchSyncEventIdGenerator EventIdGenerator = new FixedMatchSyncEventIdGenerator();

    [Fact]
    public void CreateMatch_ReturnsMatchIdJoinCodeAndCreatorToken()
    {
        var lifecycleService = new InMemoryMatchLifecycleService();

        var result = MatchLifecycleEndpoints.CreateMatch(lifecycleService);

        var payload = result.Value;
        Assert.NotNull(payload);
        Assert.True(Guid.TryParseExact(payload.MatchId, "N", out _));
        Assert.Equal(6, payload.JoinCode.Length);
        Assert.NotEmpty(payload.CreatorToken);
    }

    [Fact]
    public void JoinMatch_SecondPlayerSucceedsAndIsAssignedBlackSeatDeterministically()
    {
        var lifecycleService = new InMemoryMatchLifecycleService();
        var errorMapper = new V1MatchErrorHttpMapper();
        var created = lifecycleService.CreateMatch();

        var joinResult = MatchLifecycleEndpoints.JoinMatch(new JoinMatchRequest(created.JoinCode), lifecycleService, errorMapper);

        var okResult = Assert.IsType<Ok<JoinMatchResponse>>(joinResult.Result);
        Assert.Equal(created.MatchId, okResult.Value!.MatchId);
        Assert.Equal(MatchProtocolConstants.JoinerSeat, okResult.Value.Seat);
        Assert.NotEmpty(okResult.Value.PlayerToken);
        // Deterministic seat rule documented by contract constants:
        // creator uses White, second player always joins as Black.
        Assert.Equal("White", MatchProtocolConstants.CreatorSeat);
    }

    [Fact]
    public void JoinMatch_ThirdPlayerAttemptReturnsConflictWithMatchFullErrorCode()
    {
        var lifecycleService = new InMemoryMatchLifecycleService();
        var errorMapper = new V1MatchErrorHttpMapper();
        var created = lifecycleService.CreateMatch();

        var firstJoin = MatchLifecycleEndpoints.JoinMatch(new JoinMatchRequest(created.JoinCode), lifecycleService, errorMapper);
        Assert.IsType<Ok<JoinMatchResponse>>(firstJoin.Result);

        var thirdJoin = MatchLifecycleEndpoints.JoinMatch(new JoinMatchRequest(created.JoinCode), lifecycleService, errorMapper);

        var conflict = Assert.IsType<Conflict<ApiErrorResponse>>(thirdJoin.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        Assert.Equal(MatchProtocolConstants.ErrorMatchFull, conflict.Value!.Code);
    }

    [Fact]
    public async Task SubmitMove_AcceptedMove_PublishesRealtimeUpdate()
    {
        var lifecycleService = new InMemoryMatchLifecycleService();
        var errorMapper = new V1MatchErrorHttpMapper();
        var publisher = new CapturingMatchSyncPublisher();
        var created = lifecycleService.CreateMatch();
        var joined = lifecycleService.JoinMatch(created.JoinCode);
        Assert.IsType<JoinMatchSucceeded>(joined);

        var result = await MatchLifecycleEndpoints.SubmitMove(
            new SubmitMoveRequest(created.MatchId, created.CreatorToken, "e2", "e4"),
            lifecycleService,
            errorMapper,
            NoOpDispatchGate,
            publisher,
            EventIdGenerator,
            CancellationToken.None);

        var ok = Assert.IsType<Ok<SubmitMoveResponse>>(result);
        Assert.NotNull(ok.Value);
        Assert.True(ok.Value.Accepted);
        var published = Assert.Single(publisher.PublishedUpdates);
        Assert.Equal(created.MatchId, published.MatchId);
    }

    [Fact]
    public void GetMatchSnapshot_AuthorizedPlayer_ReturnsCanonicalSnapshot()
    {
        var lifecycleService = new InMemoryMatchLifecycleService();
        var errorMapper = new V1MatchErrorHttpMapper();
        var created = lifecycleService.CreateMatch();
        _ = lifecycleService.JoinMatch(created.JoinCode);
        _ = lifecycleService.SubmitMove(created.MatchId, created.CreatorToken, "e2", "e4", null);

        var result = MatchLifecycleEndpoints.GetMatchSnapshot(
            new GetMatchSnapshotRequest(created.MatchId, created.CreatorToken),
            lifecycleService,
            errorMapper);

        var ok = Assert.IsType<Ok<MatchSnapshotResponse>>(result);
        Assert.NotNull(ok.Value);
        Assert.Equal(created.MatchId, ok.Value.MatchId);
        Assert.Equal(2, ok.Value.MoveNumber);
        Assert.Equal(MatchSeats.Joiner, ok.Value.SideToMove);
        Assert.Equal(MatchProtocolConstants.MatchStatusInProgress, ok.Value.Status);
        Assert.Null(ok.Value.Resolution);
        Assert.Null(ok.Value.WinnerSeat);
        Assert.NotNull(ok.Value.Presence);
        Assert.True(ok.Value.Presence.Creator.IsReserved);
        Assert.True(ok.Value.Presence.Joiner.IsReserved);
    }

    [Fact]
    public void GetMatchSnapshot_Failure_IsMappedByHttpMapper()
    {
        var lifecycleService = new StubGetSnapshotUseCase(
            new GetMatchSnapshotFailed(
                new GetMatchSnapshotFailure(MatchErrorCodes.InvalidPlayerToken, "invalid token")));
        var errorMapper = new V1MatchErrorHttpMapper();

        var result = MatchLifecycleEndpoints.GetMatchSnapshot(
            new GetMatchSnapshotRequest("match", "token"),
            lifecycleService,
            errorMapper);

        var statusCode = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusCode.StatusCode);
        var json = Assert.IsType<JsonHttpResult<ApiErrorResponse>>(result);
        Assert.NotNull(json.Value);
        Assert.Equal(MatchErrorCodes.InvalidPlayerToken, json.Value.Code);
    }

    [Fact]
    public void GetMatchSnapshot_NullRequest_UsesUseCaseValidationAndReturnsBadRequestCode()
    {
        var lifecycleService = new InMemoryMatchLifecycleService();
        var errorMapper = new V1MatchErrorHttpMapper();

        var result = MatchLifecycleEndpoints.GetMatchSnapshot(
            request: null,
            lifecycleService,
            errorMapper);

        var badRequest = Assert.IsType<BadRequest<ApiErrorResponse>>(result);
        Assert.NotNull(badRequest.Value);
        Assert.Equal(MatchErrorCodes.MatchIdRequired, badRequest.Value.Code);
    }

    [Fact]
    public async Task SubmitMove_PublishFailure_BubblesException()
    {
        var lifecycleService = new InMemoryMatchLifecycleService();
        var errorMapper = new V1MatchErrorHttpMapper();
        var publisher = new ThrowingMatchSyncPublisher();
        var created = lifecycleService.CreateMatch();
        var joined = lifecycleService.JoinMatch(created.JoinCode);
        Assert.IsType<JoinMatchSucceeded>(joined);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MatchLifecycleEndpoints.SubmitMove(
                new SubmitMoveRequest(created.MatchId, created.CreatorToken, "e2", "e4"),
                lifecycleService,
                errorMapper,
                NoOpDispatchGate,
                publisher,
                EventIdGenerator,
                CancellationToken.None));
    }

    [Fact]
    public void GetMatchSnapshot_UnsupportedOutcomeType_ThrowsInvalidOperationException()
    {
        var lifecycleService = new StubGetSnapshotUseCase(new UnknownGetSnapshotOutcome());
        var errorMapper = new V1MatchErrorHttpMapper();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MatchLifecycleEndpoints.GetMatchSnapshot(
                new GetMatchSnapshotRequest("match", "token"),
                lifecycleService,
                errorMapper));

        Assert.StartsWith("Unsupported get-snapshot outcome type:", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetMatchSnapshot_NullOutcome_ThrowsInvalidOperationException()
    {
        var lifecycleService = new StubGetSnapshotUseCase(null);
        var errorMapper = new V1MatchErrorHttpMapper();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MatchLifecycleEndpoints.GetMatchSnapshot(
                new GetMatchSnapshotRequest("match", "token"),
                lifecycleService,
                errorMapper));

        Assert.Equal("Get-snapshot outcome must not be null.", exception.Message);
    }

    [Fact]
    public void GetMatchSnapshot_NullSuccessPayload_ThrowsInvalidOperationException()
    {
        var lifecycleService = new StubGetSnapshotUseCase(new GetMatchSnapshotSucceeded(null!));
        var errorMapper = new V1MatchErrorHttpMapper();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MatchLifecycleEndpoints.GetMatchSnapshot(
                new GetMatchSnapshotRequest("match", "token"),
                lifecycleService,
                errorMapper));

        Assert.Equal("Get-snapshot success outcome must include a valid snapshot payload.", exception.Message);
    }

    [Fact]
    public void GetMatchSnapshot_SuccessOutcomeWithInvalidSnapshot_ThrowsInvalidOperationException()
    {
        var lifecycleService = new StubGetSnapshotUseCase(
            new GetMatchSnapshotSucceeded(
                new GetMatchSnapshotSuccess(CreateSnapshot(moveNumber: 0))));
        var errorMapper = new V1MatchErrorHttpMapper();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MatchLifecycleEndpoints.GetMatchSnapshot(
                new GetMatchSnapshotRequest("match", "token"),
                lifecycleService,
                errorMapper));

        Assert.Equal("Get-snapshot success outcome must include a valid snapshot payload.", exception.Message);
    }

    [Fact]
    public void GetMatchSnapshot_NullFailurePayload_ThrowsInvalidOperationException()
    {
        var lifecycleService = new StubGetSnapshotUseCase(new GetMatchSnapshotFailed(null!));
        var errorMapper = new V1MatchErrorHttpMapper();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MatchLifecycleEndpoints.GetMatchSnapshot(
                new GetMatchSnapshotRequest("match", "token"),
                lifecycleService,
                errorMapper));

        Assert.Equal("Get-snapshot failure outcome must include an error payload.", exception.Message);
    }

    [Fact]
    public void JoinMatch_UnsupportedOutcomeType_ThrowsInvalidOperationException()
    {
        var lifecycleService = new StubJoinMatchUseCase(new UnknownJoinMatchOutcome());
        var errorMapper = new V1MatchErrorHttpMapper();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MatchLifecycleEndpoints.JoinMatch(new JoinMatchRequest("ABC123"), lifecycleService, errorMapper));

        Assert.StartsWith("Unsupported join outcome type:", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void JoinMatch_NullOutcome_ThrowsInvalidOperationException()
    {
        var lifecycleService = new StubJoinMatchUseCase(null);
        var errorMapper = new V1MatchErrorHttpMapper();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MatchLifecycleEndpoints.JoinMatch(new JoinMatchRequest("ABC123"), lifecycleService, errorMapper));

        Assert.Equal("Join outcome must not be null.", exception.Message);
    }

    [Fact]
    public void JoinMatch_NullSuccessPayload_ThrowsInvalidOperationException()
    {
        var lifecycleService = new StubJoinMatchUseCase(new JoinMatchSucceeded(null!));
        var errorMapper = new V1MatchErrorHttpMapper();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MatchLifecycleEndpoints.JoinMatch(new JoinMatchRequest("ABC123"), lifecycleService, errorMapper));

        Assert.Equal("Join success outcome must include a valid response payload.", exception.Message);
    }

    [Fact]
    public void JoinMatch_SuccessPayloadWithMissingFields_ThrowsInvalidOperationException()
    {
        var lifecycleService = new StubJoinMatchUseCase(
            new JoinMatchSucceeded(new JoinMatchSuccess("", MatchSeats.Joiner, "token")));
        var errorMapper = new V1MatchErrorHttpMapper();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MatchLifecycleEndpoints.JoinMatch(new JoinMatchRequest("ABC123"), lifecycleService, errorMapper));

        Assert.Equal("Join success outcome must include a valid response payload.", exception.Message);
    }

    [Fact]
    public void JoinMatch_SuccessPayloadWithInvalidSeat_ThrowsInvalidOperationException()
    {
        var lifecycleService = new StubJoinMatchUseCase(
            new JoinMatchSucceeded(new JoinMatchSuccess("match", "Observer", "token")));
        var errorMapper = new V1MatchErrorHttpMapper();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MatchLifecycleEndpoints.JoinMatch(new JoinMatchRequest("ABC123"), lifecycleService, errorMapper));

        Assert.Equal("Join success outcome must include a valid response payload.", exception.Message);
    }

    [Fact]
    public void JoinMatch_NullFailurePayload_ThrowsInvalidOperationException()
    {
        var lifecycleService = new StubJoinMatchUseCase(new JoinMatchFailed(null!));
        var errorMapper = new V1MatchErrorHttpMapper();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MatchLifecycleEndpoints.JoinMatch(new JoinMatchRequest("ABC123"), lifecycleService, errorMapper));

        Assert.Equal("Join failure outcome must include an error payload.", exception.Message);
    }

    [Fact]
    public async Task SubmitMove_UnsupportedOutcomeType_ThrowsInvalidOperationException()
    {
        var lifecycleService = new StubSubmitMoveUseCase(new UnknownSubmitMoveOutcome());
        var errorMapper = new V1MatchErrorHttpMapper();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await MatchLifecycleEndpoints.SubmitMove(
                new SubmitMoveRequest("match", "token", "e2", "e4"),
                lifecycleService,
                errorMapper,
                NoOpDispatchGate,
                NoOpPublisher,
                EventIdGenerator,
                CancellationToken.None));

        Assert.StartsWith("Unsupported submit-move outcome type:", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitMove_NullOutcome_ThrowsInvalidOperationException()
    {
        var lifecycleService = new StubSubmitMoveUseCase(null);
        var errorMapper = new V1MatchErrorHttpMapper();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await MatchLifecycleEndpoints.SubmitMove(
                new SubmitMoveRequest("match", "token", "e2", "e4"),
                lifecycleService,
                errorMapper,
                NoOpDispatchGate,
                NoOpPublisher,
                EventIdGenerator,
                CancellationToken.None));

        Assert.Equal("Submit-move outcome must not be null.", exception.Message);
    }

    [Fact]
    public async Task SubmitMove_NullSuccessPayload_ThrowsInvalidOperationException()
    {
        var lifecycleService = new StubSubmitMoveUseCase(new SubmitMoveSucceeded(null!));
        var errorMapper = new V1MatchErrorHttpMapper();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await MatchLifecycleEndpoints.SubmitMove(
                new SubmitMoveRequest("match", "token", "e2", "e4"),
                lifecycleService,
                errorMapper,
                NoOpDispatchGate,
                NoOpPublisher,
                EventIdGenerator,
                CancellationToken.None));

        Assert.Equal(
            "Submit-move success outcome must include a valid snapshot payload.",
            exception.Message);
    }

    [Fact]
    public async Task SubmitMove_SuccessOutcomeWithNullSnapshot_ThrowsInvalidOperationException()
    {
        var lifecycleService = new StubSubmitMoveUseCase(
            new SubmitMoveSucceeded(
                new SubmitMoveSuccess(null!)));
        var errorMapper = new V1MatchErrorHttpMapper();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await MatchLifecycleEndpoints.SubmitMove(
                new SubmitMoveRequest("match", "token", "e2", "e4"),
                lifecycleService,
                errorMapper,
                NoOpDispatchGate,
                NoOpPublisher,
                EventIdGenerator,
                CancellationToken.None));

        Assert.Equal(
            "Submit-move success outcome must include a valid snapshot payload.",
            exception.Message);
    }

    [Fact]
    public async Task SubmitMove_SuccessOutcomeWithInvalidSnapshotFields_ThrowsInvalidOperationException()
    {
        var lifecycleService = new StubSubmitMoveUseCase(
            new SubmitMoveSucceeded(
                new SubmitMoveSuccess(CreateSnapshot(moveNumber: 0))));
        var errorMapper = new V1MatchErrorHttpMapper();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await MatchLifecycleEndpoints.SubmitMove(
                new SubmitMoveRequest("match", "token", "e2", "e4"),
                lifecycleService,
                errorMapper,
                NoOpDispatchGate,
                NoOpPublisher,
                EventIdGenerator,
                CancellationToken.None));

        Assert.Equal(
            "Submit-move success outcome must include a valid snapshot payload.",
            exception.Message);
    }

    [Fact]
    public async Task SubmitMove_SuccessOutcomeWithInvalidSideToMove_ThrowsInvalidOperationException()
    {
        var lifecycleService = new StubSubmitMoveUseCase(
            new SubmitMoveSucceeded(
                new SubmitMoveSuccess(CreateSnapshot(sideToMove: "Green"))));
        var errorMapper = new V1MatchErrorHttpMapper();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await MatchLifecycleEndpoints.SubmitMove(
                new SubmitMoveRequest("match", "token", "e2", "e4"),
                lifecycleService,
                errorMapper,
                NoOpDispatchGate,
                NoOpPublisher,
                EventIdGenerator,
                CancellationToken.None));

        Assert.Equal(
            "Submit-move success outcome must include a valid snapshot payload.",
            exception.Message);
    }

    [Fact]
    public async Task SubmitMove_SuccessOutcomeWithInvalidBoardSymbol_ThrowsInvalidOperationException()
    {
        var lifecycleService = new StubSubmitMoveUseCase(
            new SubmitMoveSucceeded(
                new SubmitMoveSuccess(
                    CreateSnapshot(
                        board:
                        new[]
                        {
                            "rnbqkbnr",
                            "pppppppp",
                            "........",
                            "........",
                            "....X...",
                            "........",
                            "PPPPPPPP",
                            "RNBQKBNR"
                        }))));
        var errorMapper = new V1MatchErrorHttpMapper();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await MatchLifecycleEndpoints.SubmitMove(
                new SubmitMoveRequest("match", "token", "e2", "e4"),
                lifecycleService,
                errorMapper,
                NoOpDispatchGate,
                NoOpPublisher,
                EventIdGenerator,
                CancellationToken.None));

        Assert.Equal(
            "Submit-move success outcome must include a valid snapshot payload.",
            exception.Message);
    }

    [Fact]
    public async Task SubmitMove_NullFailurePayload_ThrowsInvalidOperationException()
    {
        var lifecycleService = new StubSubmitMoveUseCase(new SubmitMoveFailed(null!));
        var errorMapper = new V1MatchErrorHttpMapper();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await MatchLifecycleEndpoints.SubmitMove(
                new SubmitMoveRequest("match", "token", "e2", "e4"),
                lifecycleService,
                errorMapper,
                NoOpDispatchGate,
                NoOpPublisher,
                EventIdGenerator,
                CancellationToken.None));

        Assert.Equal("Submit-move failure outcome must include an error payload.", exception.Message);
    }

    private static MatchSnapshot CreateSnapshot(
        string matchId = "match",
        string sideToMove = MatchSeats.Creator,
        int moveNumber = 1,
        string[]? board = null)
    {
        return new MatchSnapshot(
            matchId,
            sideToMove,
            moveNumber,
            board ?? Enumerable.Repeat("........", 8).ToArray());
    }

    private sealed class StubJoinMatchUseCase(JoinMatchOutcome? outcome) : IJoinMatchUseCase
    {
        public JoinMatchOutcome JoinMatch(string? joinCode) => outcome!;
    }

    private sealed class StubSubmitMoveUseCase(SubmitMoveOutcome? outcome) : ISubmitMoveUseCase
    {
        public SubmitMoveOutcome SubmitMove(string? matchId, string? playerToken, string? from, string? to, string? promotion)
            => outcome!;
    }

    private sealed class StubGetSnapshotUseCase(GetMatchSnapshotOutcome? outcome) : IGetMatchSnapshotUseCase
    {
        public GetMatchSnapshotOutcome GetMatchSnapshot(string? matchId, string? playerToken)
            => outcome!;
    }

    private sealed class NoOpMatchSyncPublisher : IMatchSyncPublisher
    {
        public Task PublishMatchUpdatedAsync(
            MatchSnapshot snapshot,
            string eventId,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task PublishMatchSnapshotToConnectionAsync(
            string connectionId,
            MatchSnapshot snapshot,
            string eventId,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task PublishMatchPresenceChangedAsync(
            MatchSnapshot snapshot,
            string seat,
            string eventId,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task PublishMatchEndedAsync(
            MatchSnapshot snapshot,
            string eventId,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task PublishTransportErrorToConnectionAsync(
            string connectionId,
            string matchId,
            string eventId,
            string code,
            string message,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class NoOpMatchSyncDispatchGate : IMatchSyncDispatchGate
    {
        public ValueTask<IAsyncDisposable> AcquireAsync(string matchId, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<IAsyncDisposable>(NoOpDispatchLease.Instance);
        }
    }

    private sealed class NoOpDispatchLease : IAsyncDisposable
    {
        public static readonly NoOpDispatchLease Instance = new();

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
    }

    private sealed class FixedMatchSyncEventIdGenerator : IMatchSyncEventIdGenerator
    {
        public string Generate()
            => "fixed-transport-event-id";
    }

    private sealed class CapturingMatchSyncPublisher : IMatchSyncPublisher
    {
        public List<MatchSnapshot> PublishedUpdates { get; } = [];

        public Task PublishMatchUpdatedAsync(
            MatchSnapshot snapshot,
            string eventId,
            CancellationToken cancellationToken)
        {
            PublishedUpdates.Add(snapshot);
            return Task.CompletedTask;
        }

        public Task PublishMatchSnapshotToConnectionAsync(
            string connectionId,
            MatchSnapshot snapshot,
            string eventId,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task PublishMatchPresenceChangedAsync(
            MatchSnapshot snapshot,
            string seat,
            string eventId,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task PublishMatchEndedAsync(
            MatchSnapshot snapshot,
            string eventId,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task PublishTransportErrorToConnectionAsync(
            string connectionId,
            string matchId,
            string eventId,
            string code,
            string message,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class ThrowingMatchSyncPublisher : IMatchSyncPublisher
    {
        public Task PublishMatchUpdatedAsync(
            MatchSnapshot snapshot,
            string eventId,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("publish failed");
        }

        public Task PublishMatchSnapshotToConnectionAsync(
            string connectionId,
            MatchSnapshot snapshot,
            string eventId,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task PublishMatchPresenceChangedAsync(
            MatchSnapshot snapshot,
            string seat,
            string eventId,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task PublishMatchEndedAsync(
            MatchSnapshot snapshot,
            string eventId,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task PublishTransportErrorToConnectionAsync(
            string connectionId,
            string matchId,
            string eventId,
            string code,
            string message,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed record UnknownJoinMatchOutcome : JoinMatchOutcome;

    private sealed record UnknownSubmitMoveOutcome : SubmitMoveOutcome;

    private sealed record UnknownGetSnapshotOutcome : GetMatchSnapshotOutcome;
}
