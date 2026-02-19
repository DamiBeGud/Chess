namespace MultiplayerServer.Contracts.V1;

public sealed record SubmitMoveResponse(
    bool Accepted,
    MatchSnapshotResponse Snapshot);
