namespace MultiplayerServer.Contracts.V1;

public sealed record GetMatchSnapshotRequest(
    string? MatchId,
    string? PlayerToken);
