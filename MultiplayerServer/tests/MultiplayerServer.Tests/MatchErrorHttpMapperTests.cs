using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using MultiplayerServer.Application.Matches;
using MultiplayerServer.Contracts.V1;
using MultiplayerServer.Transport.V1;

namespace MultiplayerServer.Tests;

public sealed class MatchErrorHttpMapperTests
{
    private static readonly IMatchErrorHttpMapper Mapper = new V1MatchErrorHttpMapper();

    [Fact]
    public void MapJoinFailure_MatchFull_ReturnsConflict()
    {
        var result = Mapper.MapJoinFailure(new JoinMatchFailure(MatchProtocolConstants.ErrorMatchFull, "full"));

        var conflict = Assert.IsType<Conflict<ApiErrorResponse>>(result.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        Assert.Equal(MatchProtocolConstants.ErrorMatchFull, conflict.Value!.Code);
    }

    [Fact]
    public void MapSubmitMoveFailure_InvalidPlayerToken_ReturnsForbiddenJson()
    {
        var result = Mapper.MapSubmitMoveFailure(
            new SubmitMoveFailure(MatchProtocolConstants.ErrorInvalidPlayerToken, "invalid"));

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusCodeResult.StatusCode);

        var jsonResult = Assert.IsType<JsonHttpResult<ApiErrorResponse>>(result);
        Assert.Equal(MatchProtocolConstants.ErrorInvalidPlayerToken, jsonResult.Value!.Code);
    }

    [Fact]
    public void MapSubmitMoveFailure_IllegalMove_ReturnsConflict()
    {
        var result = Mapper.MapSubmitMoveFailure(new SubmitMoveFailure(MatchProtocolConstants.ErrorIllegalMove, "illegal"));

        var conflict = Assert.IsType<Conflict<ApiErrorResponse>>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        Assert.Equal(MatchProtocolConstants.ErrorIllegalMove, conflict.Value!.Code);
    }
}
