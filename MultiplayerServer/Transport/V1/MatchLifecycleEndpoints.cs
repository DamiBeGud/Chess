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
}
