using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using MultiplayerServer.Contracts.V1;

namespace MultiplayerServer.Tests;

public sealed class MatchLifecycleHttpIntegrationTests
{
    [Fact]
    public async Task CreateMatch_ReturnsExpectedV1Contract()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/v1/matches", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CreateMatchResponse>();
        Assert.NotNull(payload);
        Assert.True(Guid.TryParseExact(payload.MatchId, "N", out _));
        Assert.Equal(6, payload.JoinCode.Length);
        Assert.NotEmpty(payload.CreatorToken);
    }

    [Fact]
    public async Task JoinMatch_SecondPlayerJoinsAsBlackDeterministically()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsync("/api/v1/matches", content: null);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateMatchResponse>();
        Assert.NotNull(created);

        var joinResponse = await client.PostAsJsonAsync(
            "/api/v1/matches/join",
            new JoinMatchRequest(created.JoinCode));

        Assert.Equal(HttpStatusCode.OK, joinResponse.StatusCode);
        var joined = await joinResponse.Content.ReadFromJsonAsync<JoinMatchResponse>();
        Assert.NotNull(joined);
        Assert.Equal(created.MatchId, joined.MatchId);
        Assert.Equal(MatchProtocolConstants.JoinerSeat, joined.Seat);
        Assert.NotEmpty(joined.PlayerToken);
        Assert.Equal("White", MatchProtocolConstants.CreatorSeat);
    }

    [Fact]
    public async Task JoinMatch_ThirdPlayerGetsConflictWithMatchFullCode()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsync("/api/v1/matches", content: null);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateMatchResponse>();
        Assert.NotNull(created);

        var firstJoin = await client.PostAsJsonAsync(
            "/api/v1/matches/join",
            new JoinMatchRequest(created.JoinCode));
        Assert.Equal(HttpStatusCode.OK, firstJoin.StatusCode);

        var secondJoin = await client.PostAsJsonAsync(
            "/api/v1/matches/join",
            new JoinMatchRequest(created.JoinCode));

        Assert.Equal(HttpStatusCode.Conflict, secondJoin.StatusCode);
        var payload = await secondJoin.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal(MatchProtocolConstants.ErrorMatchFull, payload.Code);
    }

    [Fact]
    public async Task SubmitMove_AcceptedMoveUpdatesCanonicalStateAndTurn()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var (created, _) = await CreateStartedMatchAsync(client);

        var moveResponse = await client.PostAsJsonAsync(
            "/api/v1/matches/moves",
            new SubmitMoveRequest(created.MatchId, created.CreatorToken, "e2", "e4"));

        Assert.Equal(HttpStatusCode.OK, moveResponse.StatusCode);
        var payload = await moveResponse.Content.ReadFromJsonAsync<SubmitMoveResponse>();
        Assert.NotNull(payload);
        Assert.True(payload.Accepted);
        Assert.Equal(created.MatchId, payload.Snapshot.MatchId);
        Assert.Equal(MatchProtocolConstants.JoinerSeat, payload.Snapshot.SideToMove);
        Assert.Equal(2, payload.Snapshot.MoveNumber);
        Assert.Equal('.', PieceAt(payload.Snapshot, "e2"));
        Assert.Equal('P', PieceAt(payload.Snapshot, "e4"));
    }

    [Fact]
    public async Task SubmitMove_IllegalMoveRejectedWithCodeAndNoStateMutation()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var (created, _) = await CreateStartedMatchAsync(client);

        var illegalMoveResponse = await client.PostAsJsonAsync(
            "/api/v1/matches/moves",
            new SubmitMoveRequest(created.MatchId, created.CreatorToken, "e2", "e5"));

        Assert.Equal(HttpStatusCode.Conflict, illegalMoveResponse.StatusCode);
        var illegalPayload = await illegalMoveResponse.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(illegalPayload);
        Assert.Equal(MatchProtocolConstants.ErrorIllegalMove, illegalPayload.Code);

        var legalMoveResponse = await client.PostAsJsonAsync(
            "/api/v1/matches/moves",
            new SubmitMoveRequest(created.MatchId, created.CreatorToken, "e2", "e4"));

        Assert.Equal(HttpStatusCode.OK, legalMoveResponse.StatusCode);
        var acceptedPayload = await legalMoveResponse.Content.ReadFromJsonAsync<SubmitMoveResponse>();
        Assert.NotNull(acceptedPayload);
        Assert.Equal(2, acceptedPayload.Snapshot.MoveNumber);
        Assert.Equal(MatchProtocolConstants.JoinerSeat, acceptedPayload.Snapshot.SideToMove);
    }

    [Fact]
    public async Task SubmitMove_OutOfTurnRejectedWithCodeAndNoStateMutation()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var (created, joined) = await CreateStartedMatchAsync(client);

        var outOfTurnResponse = await client.PostAsJsonAsync(
            "/api/v1/matches/moves",
            new SubmitMoveRequest(created.MatchId, joined.PlayerToken, "e7", "e5"));

        Assert.Equal(HttpStatusCode.Conflict, outOfTurnResponse.StatusCode);
        var outOfTurnPayload = await outOfTurnResponse.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(outOfTurnPayload);
        Assert.Equal(MatchProtocolConstants.ErrorOutOfTurn, outOfTurnPayload.Code);

        var legalMoveResponse = await client.PostAsJsonAsync(
            "/api/v1/matches/moves",
            new SubmitMoveRequest(created.MatchId, created.CreatorToken, "e2", "e4"));

        Assert.Equal(HttpStatusCode.OK, legalMoveResponse.StatusCode);
        var acceptedPayload = await legalMoveResponse.Content.ReadFromJsonAsync<SubmitMoveResponse>();
        Assert.NotNull(acceptedPayload);
        Assert.Equal(2, acceptedPayload.Snapshot.MoveNumber);
    }

    [Fact]
    public async Task SubmitMove_InvalidTokenAndUnknownMatchReturnExplicitCodes()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var (created, _) = await CreateStartedMatchAsync(client);

        var invalidTokenResponse = await client.PostAsJsonAsync(
            "/api/v1/matches/moves",
            new SubmitMoveRequest(created.MatchId, "invalid-token", "e2", "e4"));

        Assert.Equal(HttpStatusCode.Forbidden, invalidTokenResponse.StatusCode);
        var invalidTokenPayload = await invalidTokenResponse.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(invalidTokenPayload);
        Assert.Equal(MatchProtocolConstants.ErrorInvalidPlayerToken, invalidTokenPayload.Code);

        var unknownMatchResponse = await client.PostAsJsonAsync(
            "/api/v1/matches/moves",
            new SubmitMoveRequest(Guid.NewGuid().ToString("N"), created.CreatorToken, "e2", "e4"));

        Assert.Equal(HttpStatusCode.NotFound, unknownMatchResponse.StatusCode);
        var unknownMatchPayload = await unknownMatchResponse.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(unknownMatchPayload);
        Assert.Equal(MatchProtocolConstants.ErrorMatchNotFound, unknownMatchPayload.Code);
    }

    [Fact]
    public async Task SubmitMove_MatchNotReadyReturnsExplicitErrorCode()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsync("/api/v1/matches", content: null);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateMatchResponse>();
        Assert.NotNull(created);

        var moveResponse = await client.PostAsJsonAsync(
            "/api/v1/matches/moves",
            new SubmitMoveRequest(created.MatchId, created.CreatorToken, "e2", "e4"));

        Assert.Equal(HttpStatusCode.Conflict, moveResponse.StatusCode);
        var payload = await moveResponse.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal(MatchProtocolConstants.ErrorMatchNotReady, payload.Code);
    }

    [Fact]
    public async Task SubmitMove_RejectsMoveThatLeavesKingInCheck()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var (created, joined) = await CreateStartedMatchAsync(client);

        await SubmitMoveAcceptedAsync(client, created.MatchId, created.CreatorToken, "f2", "f3");
        await SubmitMoveAcceptedAsync(client, created.MatchId, joined.PlayerToken, "e7", "e5");
        await SubmitMoveAcceptedAsync(client, created.MatchId, created.CreatorToken, "g2", "g4");
        await SubmitMoveAcceptedAsync(client, created.MatchId, joined.PlayerToken, "d8", "h4");

        var ignoredCheck = await client.PostAsJsonAsync(
            "/api/v1/matches/moves",
            new SubmitMoveRequest(created.MatchId, created.CreatorToken, "a2", "a3"));

        Assert.Equal(HttpStatusCode.Conflict, ignoredCheck.StatusCode);
        var ignoredCheckPayload = await ignoredCheck.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(ignoredCheckPayload);
        Assert.Equal(MatchProtocolConstants.ErrorIllegalMove, ignoredCheckPayload.Code);

        var blackOutOfTurn = await client.PostAsJsonAsync(
            "/api/v1/matches/moves",
            new SubmitMoveRequest(created.MatchId, joined.PlayerToken, "a7", "a6"));
        Assert.Equal(HttpStatusCode.Conflict, blackOutOfTurn.StatusCode);
        var outOfTurnPayload = await blackOutOfTurn.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(outOfTurnPayload);
        Assert.Equal(MatchProtocolConstants.ErrorOutOfTurn, outOfTurnPayload.Code);
    }

    [Fact]
    public async Task SubmitMove_AllowsKingSideCastling()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var (created, joined) = await CreateStartedMatchAsync(client);

        await SubmitMoveAcceptedAsync(client, created.MatchId, created.CreatorToken, "e2", "e4");
        await SubmitMoveAcceptedAsync(client, created.MatchId, joined.PlayerToken, "a7", "a6");
        await SubmitMoveAcceptedAsync(client, created.MatchId, created.CreatorToken, "g1", "f3");
        await SubmitMoveAcceptedAsync(client, created.MatchId, joined.PlayerToken, "a6", "a5");
        await SubmitMoveAcceptedAsync(client, created.MatchId, created.CreatorToken, "f1", "e2");
        await SubmitMoveAcceptedAsync(client, created.MatchId, joined.PlayerToken, "a5", "a4");
        var castled = await SubmitMoveAcceptedAsync(client, created.MatchId, created.CreatorToken, "e1", "g1");

        Assert.Equal('K', PieceAt(castled.Snapshot, "g1"));
        Assert.Equal('R', PieceAt(castled.Snapshot, "f1"));
        Assert.Equal('.', PieceAt(castled.Snapshot, "e1"));
        Assert.Equal('.', PieceAt(castled.Snapshot, "h1"));
        Assert.Equal(MatchProtocolConstants.JoinerSeat, castled.Snapshot.SideToMove);
    }

    [Fact]
    public async Task SubmitMove_AllowsEnPassantCapture()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var (created, joined) = await CreateStartedMatchAsync(client);

        await SubmitMoveAcceptedAsync(client, created.MatchId, created.CreatorToken, "e2", "e4");
        await SubmitMoveAcceptedAsync(client, created.MatchId, joined.PlayerToken, "a7", "a6");
        await SubmitMoveAcceptedAsync(client, created.MatchId, created.CreatorToken, "e4", "e5");
        await SubmitMoveAcceptedAsync(client, created.MatchId, joined.PlayerToken, "d7", "d5");
        var enPassant = await SubmitMoveAcceptedAsync(client, created.MatchId, created.CreatorToken, "e5", "d6");

        Assert.Equal('P', PieceAt(enPassant.Snapshot, "d6"));
        Assert.Equal('.', PieceAt(enPassant.Snapshot, "d5"));
        Assert.Equal(6, enPassant.Snapshot.MoveNumber);
    }

    [Fact]
    public async Task SubmitMove_AutoPromotesPawnToQueen()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var (created, joined) = await CreateStartedMatchAsync(client);

        await SubmitMoveAcceptedAsync(client, created.MatchId, created.CreatorToken, "a2", "a4");
        await SubmitMoveAcceptedAsync(client, created.MatchId, joined.PlayerToken, "h7", "h6");
        await SubmitMoveAcceptedAsync(client, created.MatchId, created.CreatorToken, "a4", "a5");
        await SubmitMoveAcceptedAsync(client, created.MatchId, joined.PlayerToken, "h6", "h5");
        await SubmitMoveAcceptedAsync(client, created.MatchId, created.CreatorToken, "a5", "a6");
        await SubmitMoveAcceptedAsync(client, created.MatchId, joined.PlayerToken, "h5", "h4");
        await SubmitMoveAcceptedAsync(client, created.MatchId, created.CreatorToken, "a6", "b7");
        await SubmitMoveAcceptedAsync(client, created.MatchId, joined.PlayerToken, "h4", "h3");
        var promoted = await SubmitMoveAcceptedAsync(client, created.MatchId, created.CreatorToken, "b7", "c8");

        Assert.Equal('Q', PieceAt(promoted.Snapshot, "c8"));
        Assert.Equal(10, promoted.Snapshot.MoveNumber);
    }

    [Fact]
    public async Task SubmitMove_UsesRequestedPromotionPieceWhenProvided()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var (created, joined) = await CreateStartedMatchAsync(client);

        await SubmitMoveAcceptedAsync(client, created.MatchId, created.CreatorToken, "a2", "a4");
        await SubmitMoveAcceptedAsync(client, created.MatchId, joined.PlayerToken, "h7", "h6");
        await SubmitMoveAcceptedAsync(client, created.MatchId, created.CreatorToken, "a4", "a5");
        await SubmitMoveAcceptedAsync(client, created.MatchId, joined.PlayerToken, "h6", "h5");
        await SubmitMoveAcceptedAsync(client, created.MatchId, created.CreatorToken, "a5", "a6");
        await SubmitMoveAcceptedAsync(client, created.MatchId, joined.PlayerToken, "h5", "h4");
        await SubmitMoveAcceptedAsync(client, created.MatchId, created.CreatorToken, "a6", "b7");
        await SubmitMoveAcceptedAsync(client, created.MatchId, joined.PlayerToken, "h4", "h3");
        var promoted = await SubmitMoveAcceptedAsync(
            client,
            created.MatchId,
            created.CreatorToken,
            "b7",
            "c8",
            promotion: "N");

        Assert.Equal('N', PieceAt(promoted.Snapshot, "c8"));
        Assert.Equal(10, promoted.Snapshot.MoveNumber);
    }

    [Fact]
    public async Task SubmitMove_InvalidPromotionRejectedWithExplicitCodeAndNoStateMutation()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var (created, _) = await CreateStartedMatchAsync(client);

        var invalidPromotionResponse = await client.PostAsJsonAsync(
            "/api/v1/matches/moves",
            new SubmitMoveRequest(created.MatchId, created.CreatorToken, "e2", "e4", Promotion: "X"));

        Assert.Equal(HttpStatusCode.BadRequest, invalidPromotionResponse.StatusCode);
        var invalidPromotionPayload = await invalidPromotionResponse.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(invalidPromotionPayload);
        Assert.Equal(MatchProtocolConstants.ErrorInvalidPromotion, invalidPromotionPayload.Code);

        var legalMoveResponse = await client.PostAsJsonAsync(
            "/api/v1/matches/moves",
            new SubmitMoveRequest(created.MatchId, created.CreatorToken, "e2", "e4"));

        Assert.Equal(HttpStatusCode.OK, legalMoveResponse.StatusCode);
        var acceptedPayload = await legalMoveResponse.Content.ReadFromJsonAsync<SubmitMoveResponse>();
        Assert.NotNull(acceptedPayload);
        Assert.Equal(2, acceptedPayload.Snapshot.MoveNumber);
        Assert.Equal('P', PieceAt(acceptedPayload.Snapshot, "e4"));
    }

    private static async Task<(CreateMatchResponse Created, JoinMatchResponse Joined)> CreateStartedMatchAsync(HttpClient client)
    {
        var createResponse = await client.PostAsync("/api/v1/matches", content: null);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateMatchResponse>();
        Assert.NotNull(created);

        var joinResponse = await client.PostAsJsonAsync(
            "/api/v1/matches/join",
            new JoinMatchRequest(created.JoinCode));
        Assert.Equal(HttpStatusCode.OK, joinResponse.StatusCode);
        var joined = await joinResponse.Content.ReadFromJsonAsync<JoinMatchResponse>();
        Assert.NotNull(joined);

        return (created, joined);
    }

    private static async Task<SubmitMoveResponse> SubmitMoveAcceptedAsync(
        HttpClient client,
        string matchId,
        string playerToken,
        string from,
        string to,
        string? promotion = null)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/matches/moves",
            new SubmitMoveRequest(matchId, playerToken, from, to, promotion));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<SubmitMoveResponse>();
        Assert.NotNull(payload);
        Assert.True(payload.Accepted);
        return payload;
    }

    private static char PieceAt(MatchSnapshotResponse snapshot, string square)
    {
        var file = char.ToLowerInvariant(square[0]) - 'a';
        var rank = square[1] - '0';
        var row = 8 - rank;
        return snapshot.Board[row][file];
    }
}
