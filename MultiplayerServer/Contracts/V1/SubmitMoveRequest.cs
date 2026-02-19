namespace MultiplayerServer.Contracts.V1;

public sealed record SubmitMoveRequest(
    string? MatchId,
    string? PlayerToken,
    string? From,
    string? To,
    string? Promotion = null);
