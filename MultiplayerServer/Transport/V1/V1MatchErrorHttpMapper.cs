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
            MatchErrorCodes.JoinCodeRequired =>
                TypedResults.BadRequest(new ApiErrorResponse(failure.Code, failure.Message)),
            MatchErrorCodes.MatchNotFound =>
                TypedResults.NotFound(new ApiErrorResponse(failure.Code, failure.Message)),
            MatchErrorCodes.MatchFull =>
                TypedResults.Conflict(new ApiErrorResponse(failure.Code, failure.Message)),
            _ =>
                TypedResults.BadRequest(new ApiErrorResponse(failure.Code, failure.Message))
        };
    }

    public IResult MapSubmitMoveFailure(SubmitMoveFailure failure)
    {
        return failure.Code switch
        {
            MatchErrorCodes.MatchNotFound =>
                TypedResults.NotFound(new ApiErrorResponse(failure.Code, failure.Message)),
            MatchErrorCodes.InvalidPlayerToken =>
                TypedResults.Json(
                    new ApiErrorResponse(failure.Code, failure.Message),
                    statusCode: StatusCodes.Status403Forbidden),
            MatchErrorCodes.UnauthorizedResume =>
                TypedResults.Json(
                    new ApiErrorResponse(failure.Code, failure.Message),
                    statusCode: StatusCodes.Status403Forbidden),
            MatchErrorCodes.MatchNotReady or
            MatchErrorCodes.OutOfTurn or
            MatchErrorCodes.IllegalMove or
            MatchErrorCodes.SeatNotReconnectable or
            MatchErrorCodes.GraceExpired or
            MatchErrorCodes.MatchAlreadyEnded =>
                TypedResults.Conflict(new ApiErrorResponse(failure.Code, failure.Message)),
            MatchErrorCodes.InvalidPromotion =>
                TypedResults.BadRequest(new ApiErrorResponse(failure.Code, failure.Message)),
            _ =>
                TypedResults.BadRequest(new ApiErrorResponse(failure.Code, failure.Message))
        };
    }

    public IResult MapGetSnapshotFailure(GetMatchSnapshotFailure failure)
    {
        return failure.Code switch
        {
            MatchErrorCodes.MatchNotFound =>
                TypedResults.NotFound(new ApiErrorResponse(failure.Code, failure.Message)),
            MatchErrorCodes.InvalidPlayerToken or
            MatchErrorCodes.UnauthorizedResume =>
                TypedResults.Json(
                    new ApiErrorResponse(failure.Code, failure.Message),
                    statusCode: StatusCodes.Status403Forbidden),
            MatchErrorCodes.MatchAlreadyEnded or
            MatchErrorCodes.SeatNotReconnectable or
            MatchErrorCodes.GraceExpired =>
                TypedResults.Conflict(new ApiErrorResponse(failure.Code, failure.Message)),
            _ =>
                TypedResults.BadRequest(new ApiErrorResponse(failure.Code, failure.Message))
        };
    }
}
