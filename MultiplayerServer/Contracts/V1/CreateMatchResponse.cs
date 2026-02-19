namespace MultiplayerServer.Contracts.V1;

public sealed record CreateMatchResponse(
    string MatchId,
    string JoinCode,
    string CreatorToken);
