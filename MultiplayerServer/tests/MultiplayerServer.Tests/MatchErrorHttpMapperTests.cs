using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using MultiplayerServer.Application.Matches;
using MultiplayerServer.Contracts.V1;
using MultiplayerServer.Transport.V1;

namespace MultiplayerServer.Tests;

public sealed class MatchErrorHttpMapperTests
{
    private static readonly IMatchErrorHttpMapper Mapper = new V1MatchErrorHttpMapper();

    public static TheoryData<string, int> JoinFailureMappings =>
        new()
        {
            { MatchProtocolConstants.ErrorJoinCodeRequired, StatusCodes.Status400BadRequest },
            { MatchProtocolConstants.ErrorMatchNotFound, StatusCodes.Status404NotFound },
            { MatchProtocolConstants.ErrorMatchFull, StatusCodes.Status409Conflict },
            { "unknown_join_error", StatusCodes.Status400BadRequest }
        };

    public static TheoryData<string, int> SubmitMoveFailureMappings =>
        new()
        {
            { MatchProtocolConstants.ErrorMatchNotFound, StatusCodes.Status404NotFound },
            { MatchProtocolConstants.ErrorInvalidPlayerToken, StatusCodes.Status403Forbidden },
            { MatchProtocolConstants.ErrorMatchNotReady, StatusCodes.Status409Conflict },
            { MatchProtocolConstants.ErrorOutOfTurn, StatusCodes.Status409Conflict },
            { MatchProtocolConstants.ErrorIllegalMove, StatusCodes.Status409Conflict },
            { MatchProtocolConstants.ErrorInvalidPromotion, StatusCodes.Status400BadRequest },
            { MatchProtocolConstants.ErrorGraceExpired, StatusCodes.Status409Conflict },
            { MatchProtocolConstants.ErrorSeatNotReconnectable, StatusCodes.Status409Conflict },
            { MatchProtocolConstants.ErrorMatchAlreadyEnded, StatusCodes.Status409Conflict },
            { MatchProtocolConstants.ErrorUnauthorizedResume, StatusCodes.Status403Forbidden },
            { MatchProtocolConstants.ErrorMatchIdRequired, StatusCodes.Status400BadRequest },
            { MatchProtocolConstants.ErrorPlayerTokenRequired, StatusCodes.Status400BadRequest },
            { MatchProtocolConstants.ErrorMoveCoordinatesRequired, StatusCodes.Status400BadRequest },
            { "unknown_submit_move_error", StatusCodes.Status400BadRequest }
        };

    public static TheoryData<string, int> SnapshotFailureMappings =>
        new()
        {
            { MatchProtocolConstants.ErrorMatchNotFound, StatusCodes.Status404NotFound },
            { MatchProtocolConstants.ErrorInvalidPlayerToken, StatusCodes.Status403Forbidden },
            { MatchProtocolConstants.ErrorUnauthorizedResume, StatusCodes.Status403Forbidden },
            { MatchProtocolConstants.ErrorMatchIdRequired, StatusCodes.Status400BadRequest },
            { MatchProtocolConstants.ErrorPlayerTokenRequired, StatusCodes.Status400BadRequest },
            { MatchProtocolConstants.ErrorGraceExpired, StatusCodes.Status409Conflict },
            { MatchProtocolConstants.ErrorSeatNotReconnectable, StatusCodes.Status409Conflict },
            { MatchProtocolConstants.ErrorMatchAlreadyEnded, StatusCodes.Status409Conflict },
            { "unknown_snapshot_error", StatusCodes.Status400BadRequest }
        };

    [Theory]
    [MemberData(nameof(JoinFailureMappings))]
    public void MapJoinFailure_AllRelevantCodes_MapToExpectedStatusAndPayload(string code, int expectedStatusCode)
    {
        var message = $"{code} message";
        var result = Mapper.MapJoinFailure(new JoinMatchFailure(code, message));
        var mapped = result.Result;

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(mapped);
        Assert.Equal(expectedStatusCode, statusCodeResult.StatusCode);

        var payload = ExtractPayload(mapped);
        Assert.Equal(code, payload.Code);
        Assert.Equal(message, payload.Message);
    }

    [Theory]
    [MemberData(nameof(SubmitMoveFailureMappings))]
    public void MapSubmitMoveFailure_AllRelevantCodes_MapToExpectedStatusAndPayload(string code, int expectedStatusCode)
    {
        var message = $"{code} message";
        var mapped = Mapper.MapSubmitMoveFailure(new SubmitMoveFailure(code, message));

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(mapped);
        Assert.Equal(expectedStatusCode, statusCodeResult.StatusCode);

        var payload = ExtractPayload(mapped);
        Assert.Equal(code, payload.Code);
        Assert.Equal(message, payload.Message);
    }

    [Theory]
    [MemberData(nameof(SnapshotFailureMappings))]
    public void MapGetSnapshotFailure_AllRelevantCodes_MapToExpectedStatusAndPayload(string code, int expectedStatusCode)
    {
        var message = $"{code} message";
        var mapped = Mapper.MapGetSnapshotFailure(new GetMatchSnapshotFailure(code, message));

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(mapped);
        Assert.Equal(expectedStatusCode, statusCodeResult.StatusCode);

        var payload = ExtractPayload(mapped);
        Assert.Equal(code, payload.Code);
        Assert.Equal(message, payload.Message);
    }

    [Fact]
    public void MapSubmitMoveFailure_InvalidPlayerToken_UsesJsonForbiddenResponse()
    {
        var mapped = Mapper.MapSubmitMoveFailure(
            new SubmitMoveFailure(MatchProtocolConstants.ErrorInvalidPlayerToken, "invalid"));

        Assert.IsType<JsonHttpResult<ApiErrorResponse>>(mapped);
    }

    [Fact]
    public void MapGetSnapshotFailure_InvalidPlayerToken_UsesJsonForbiddenResponse()
    {
        var mapped = Mapper.MapGetSnapshotFailure(
            new GetMatchSnapshotFailure(MatchProtocolConstants.ErrorInvalidPlayerToken, "invalid"));

        Assert.IsType<JsonHttpResult<ApiErrorResponse>>(mapped);
    }

    private static ApiErrorResponse ExtractPayload(IResult mapped)
    {
        switch (mapped)
        {
            case BadRequest<ApiErrorResponse> badRequest:
                Assert.NotNull(badRequest.Value);
                return badRequest.Value;
            case NotFound<ApiErrorResponse> notFound:
                Assert.NotNull(notFound.Value);
                return notFound.Value;
            case Conflict<ApiErrorResponse> conflict:
                Assert.NotNull(conflict.Value);
                return conflict.Value;
            case JsonHttpResult<ApiErrorResponse> json:
                Assert.NotNull(json.Value);
                return json.Value;
            default:
                throw new InvalidOperationException($"Unexpected mapper result type: {mapped.GetType().Name}");
        }
    }
}
