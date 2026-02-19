using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using MultiplayerServer.Application.Matches;
using MultiplayerServer.Contracts.V1;
using MultiplayerServer.Transport.V1;

namespace MultiplayerServer.Tests;

public sealed class MatchLifecycleEndpointsTests
{
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
}
