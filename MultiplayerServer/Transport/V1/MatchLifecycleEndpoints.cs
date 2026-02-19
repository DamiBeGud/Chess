using Microsoft.AspNetCore.Http.HttpResults;
using MultiplayerServer.Application.Matches;
using MultiplayerServer.Contracts.V1;
using MultiplayerServer.Hubs.V1;

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
        var created = lifecycleService.CreateMatch();
        return TypedResults.Ok(new CreateMatchResponse(created.MatchId, created.JoinCode, created.CreatorToken));
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
            null => throw new InvalidOperationException("Join outcome must not be null."),
            JoinMatchSucceeded { Response: { } response } when IsValidJoinSuccess(response) => TypedResults.Ok(
                new JoinMatchResponse(response.MatchId, response.Seat, response.PlayerToken)),
            JoinMatchSucceeded => throw new InvalidOperationException("Join success outcome must include a valid response payload."),
            JoinMatchFailed { Error: { } error } => errorHttpMapper.MapJoinFailure(error),
            JoinMatchFailed => throw new InvalidOperationException("Join failure outcome must include an error payload."),
            _ => throw new InvalidOperationException($"Unsupported join outcome type: {result.GetType().Name}")
        };
    }

    public static async Task<IResult> SubmitMove(
        SubmitMoveRequest request,
        ISubmitMoveUseCase lifecycleService,
        IMatchErrorHttpMapper errorHttpMapper,
        IMatchSyncDispatchGate dispatchGate,
        IMatchSyncPublisher syncPublisher,
        IMatchSyncEventIdGenerator eventIdGenerator,
        CancellationToken cancellationToken)
    {
        var result = lifecycleService.SubmitMove(
            request.MatchId,
            request.PlayerToken,
            request.From,
            request.To,
            request.Promotion);
        switch (result)
        {
            case null:
                throw new InvalidOperationException("Submit-move outcome must not be null.");
            case SubmitMoveSucceeded { Response: { Snapshot: { } snapshot } } when IsValidSnapshot(snapshot):
            {
                await using var dispatchLease = await dispatchGate.AcquireAsync(snapshot.MatchId, CancellationToken.None);
                await syncPublisher.PublishMatchUpdatedAsync(
                    snapshot,
                    eventIdGenerator.Generate(),
                    CancellationToken.None);

                return TypedResults.Ok(
                    new SubmitMoveResponse(
                        true,
                        MatchContractMapper.ToContractSnapshot(snapshot)));
            }
            case SubmitMoveSucceeded:
                throw new InvalidOperationException(
                    "Submit-move success outcome must include a valid snapshot payload.");
            case SubmitMoveFailed { Error: { } error }:
                return errorHttpMapper.MapSubmitMoveFailure(error);
            case SubmitMoveFailed:
                throw new InvalidOperationException(
                    "Submit-move failure outcome must include an error payload.");
            default:
                throw new InvalidOperationException($"Unsupported submit-move outcome type: {result.GetType().Name}");
        }
    }

    private static bool IsValidJoinSuccess(JoinMatchSuccess response)
    {
        return !string.IsNullOrWhiteSpace(response.MatchId) &&
               !string.IsNullOrWhiteSpace(response.PlayerToken) &&
               (string.Equals(response.Seat, MatchSeats.Creator, StringComparison.Ordinal) ||
                string.Equals(response.Seat, MatchSeats.Joiner, StringComparison.Ordinal));
    }

    private static bool IsValidSnapshot(MatchSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.MatchId) ||
            (!string.Equals(snapshot.SideToMove, MatchSeats.Creator, StringComparison.Ordinal) &&
             !string.Equals(snapshot.SideToMove, MatchSeats.Joiner, StringComparison.Ordinal)) ||
            snapshot.MoveNumber < 1 ||
            snapshot.Board is null ||
            snapshot.Board.Count != 8)
        {
            return false;
        }

        foreach (var row in snapshot.Board)
        {
            if (string.IsNullOrWhiteSpace(row) || row.Length != 8)
            {
                return false;
            }

            foreach (var piece in row)
            {
                if (!IsValidBoardSymbol(piece))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsValidBoardSymbol(char piece)
    {
        return piece is '.' or
            'P' or 'R' or 'N' or 'B' or 'Q' or 'K' or
            'p' or 'r' or 'n' or 'b' or 'q' or 'k';
    }
}
