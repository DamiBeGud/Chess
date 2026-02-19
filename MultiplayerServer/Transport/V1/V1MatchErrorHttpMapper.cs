using Microsoft.AspNetCore.Http.HttpResults;
using MultiplayerServer.Application.Matches;
using MultiplayerServer.Contracts.V1;

namespace MultiplayerServer.Transport.V1;

public sealed class V1MatchErrorHttpMapper : IMatchErrorHttpMapper
{
    public Results<Ok<JoinMatchResponse>, BadRequest<ApiErrorResponse>, NotFound<ApiErrorResponse>, Conflict<ApiErrorResponse>>
        MapJoinFailure(JoinMatchFailure failure)
    {
        return failure.Code switch
        {
            MatchProtocolConstants.ErrorJoinCodeRequired =>
                TypedResults.BadRequest(new ApiErrorResponse(failure.Code, failure.Message)),
            MatchProtocolConstants.ErrorMatchNotFound =>
                TypedResults.NotFound(new ApiErrorResponse(failure.Code, failure.Message)),
            MatchProtocolConstants.ErrorMatchFull =>
                TypedResults.Conflict(new ApiErrorResponse(failure.Code, failure.Message)),
            _ =>
                TypedResults.BadRequest(new ApiErrorResponse(failure.Code, failure.Message))
        };
    }

    public IResult MapSubmitMoveFailure(SubmitMoveFailure failure)
    {
        return failure.Code switch
        {
            MatchProtocolConstants.ErrorMatchNotFound =>
                TypedResults.NotFound(new ApiErrorResponse(failure.Code, failure.Message)),
            MatchProtocolConstants.ErrorInvalidPlayerToken =>
                TypedResults.Json(
                    new ApiErrorResponse(failure.Code, failure.Message),
                    statusCode: StatusCodes.Status403Forbidden),
            MatchProtocolConstants.ErrorMatchNotReady or
            MatchProtocolConstants.ErrorOutOfTurn or
            MatchProtocolConstants.ErrorIllegalMove =>
                TypedResults.Conflict(new ApiErrorResponse(failure.Code, failure.Message)),
            MatchProtocolConstants.ErrorInvalidPromotion =>
                TypedResults.BadRequest(new ApiErrorResponse(failure.Code, failure.Message)),
            _ =>
                TypedResults.BadRequest(new ApiErrorResponse(failure.Code, failure.Message))
        };
    }
}
