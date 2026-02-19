using Microsoft.AspNetCore.Http.HttpResults;
using MultiplayerServer.Application.Matches;
using MultiplayerServer.Contracts.V1;

namespace MultiplayerServer.Transport.V1;

public static class MatchLifecycleEndpoints
{
    public static RouteGroupBuilder MapMatchLifecycleEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost(ServerRouteConventions.ApiV1Matches, CreateMatch);
        group.MapPost(ServerRouteConventions.ApiV1MatchesJoin, JoinMatch);
        group.MapPost(ServerRouteConventions.ApiV1MatchesMoves, SubmitMove);
        return group;
    }

    public static Ok<CreateMatchResponse> CreateMatch(IMatchLifecycleService lifecycleService)
    {
        return TypedResults.Ok(lifecycleService.CreateMatch());
    }

    public static Results<Ok<JoinMatchResponse>, BadRequest<ApiErrorResponse>, NotFound<ApiErrorResponse>, Conflict<ApiErrorResponse>>
        JoinMatch(JoinMatchRequest request, IMatchLifecycleService lifecycleService)
    {
        var result = lifecycleService.JoinMatch(request.JoinCode);
        if (result.IsSuccess)
        {
            return TypedResults.Ok(result.Response!);
        }

        var failure = result.Failure!;
        return failure.Code switch
        {
            MatchProtocolConstants.ErrorJoinCodeRequired => TypedResults.BadRequest(new ApiErrorResponse(failure.Code, failure.Message)),
            MatchProtocolConstants.ErrorMatchNotFound => TypedResults.NotFound(new ApiErrorResponse(failure.Code, failure.Message)),
            MatchProtocolConstants.ErrorMatchFull => TypedResults.Conflict(new ApiErrorResponse(failure.Code, failure.Message)),
            _ => TypedResults.BadRequest(new ApiErrorResponse(failure.Code, failure.Message))
        };
    }

    public static IResult SubmitMove(SubmitMoveRequest request, IMatchLifecycleService lifecycleService)
    {
        var result = lifecycleService.SubmitMove(
            request.MatchId,
            request.PlayerToken,
            request.From,
            request.To,
            request.Promotion);
        if (result.IsSuccess)
        {
            return TypedResults.Ok(result.Response!);
        }

        var failure = result.Failure!;
        return failure.Code switch
        {
            MatchProtocolConstants.ErrorMatchNotFound =>
                TypedResults.NotFound(new ApiErrorResponse(failure.Code, failure.Message)),
            MatchProtocolConstants.ErrorInvalidPlayerToken =>
                TypedResults.Json(new ApiErrorResponse(failure.Code, failure.Message), statusCode: StatusCodes.Status403Forbidden),
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
