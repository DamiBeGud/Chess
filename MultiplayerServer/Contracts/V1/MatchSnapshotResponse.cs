namespace MultiplayerServer.Contracts.V1;

public sealed record MatchSnapshotResponse(
    string MatchId,
    string SideToMove,
    int MoveNumber,
    IReadOnlyList<string> Board);
