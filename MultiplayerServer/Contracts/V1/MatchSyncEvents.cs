namespace MultiplayerServer.Contracts.V1;

public sealed record MatchSyncEventMetadata(
    string MatchId,
    string EventId,
    long Sequence,
    DateTimeOffset OccurredUtc);

public sealed record MatchSnapshotSyncEvent(
    string EventType,
    MatchSyncEventMetadata Metadata,
    MatchSnapshotResponse Snapshot);

public sealed record MatchUpdatedSyncEvent(
    string EventType,
    MatchSyncEventMetadata Metadata,
    MatchSnapshotResponse Snapshot);

public sealed record MatchPresenceChangedSyncEvent(
    string EventType,
    MatchSyncEventMetadata Metadata,
    MatchSnapshotResponse Snapshot,
    string Seat);

public sealed record MatchEndedSyncEvent(
    string EventType,
    MatchSyncEventMetadata Metadata,
    MatchSnapshotResponse Snapshot);

public sealed record MatchErrorSyncEvent(
    string EventType,
    MatchSyncEventMetadata Metadata,
    string Code,
    string Message);
