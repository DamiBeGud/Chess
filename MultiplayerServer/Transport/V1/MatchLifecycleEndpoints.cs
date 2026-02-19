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

    public static Ok<CreateMatchResponse> CreateMatch(ICreateMatchUseCase lifecycleService)
    {
        return TypedResults.Ok(lifecycleService.CreateMatch());
    }

    public static Results<Ok<JoinMatchResponse>, BadRequest<ApiErrorResponse>, NotFound<ApiErrorResponse>, Conflict<ApiErrorResponse>>
        JoinMatch(
            JoinMatchRequest request,
            IJoinMatchUseCase lifecycleService,
            IMatchErrorHttpMapper errorHttpMapper)
    {
        var result = lifecycleService.JoinMatch(request.JoinCode);
        return result switch
        {
            JoinMatchSucceeded success => TypedResults.Ok(success.Response),
            JoinMatchFailed failed => errorHttpMapper.MapJoinFailure(failed.Error),
            _ => throw new InvalidOperationException($"Unsupported join outcome type: {result.GetType().Name}")
        };
    }

    public static IResult SubmitMove(
        SubmitMoveRequest request,
        ISubmitMoveUseCase lifecycleService,
        IMatchErrorHttpMapper errorHttpMapper)
    {
        var result = lifecycleService.SubmitMove(
            request.MatchId,
            request.PlayerToken,
            request.From,
            request.To,
            request.Promotion);
        return result switch
        {
            SubmitMoveSucceeded success => TypedResults.Ok(success.Response),
            SubmitMoveFailed failed => errorHttpMapper.MapSubmitMoveFailure(failed.Error),
            _ => throw new InvalidOperationException($"Unsupported submit-move outcome type: {result.GetType().Name}")
        };
    }
}
