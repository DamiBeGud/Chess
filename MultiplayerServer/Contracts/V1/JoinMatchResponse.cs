namespace MultiplayerServer.Contracts.V1;

public sealed record JoinMatchResponse(
    string MatchId,
    string Seat,
    string PlayerToken);
