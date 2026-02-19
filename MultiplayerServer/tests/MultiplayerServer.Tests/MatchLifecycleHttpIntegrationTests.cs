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
}
