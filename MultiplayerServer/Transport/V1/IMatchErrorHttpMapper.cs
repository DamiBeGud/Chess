using Microsoft.AspNetCore.Http.HttpResults;
using MultiplayerServer.Application.Matches;
using MultiplayerServer.Contracts.V1;

namespace MultiplayerServer.Transport.V1;

public interface IMatchErrorHttpMapper
{
    Results<Ok<JoinMatchResponse>, BadRequest<ApiErrorResponse>, NotFound<ApiErrorResponse>, Conflict<ApiErrorResponse>>
        MapJoinFailure(JoinMatchFailure failure);

    IResult MapSubmitMoveFailure(SubmitMoveFailure failure);
}
