using System;
using System.Collections.Generic;
using Chess.Domain;

namespace Chess.Online;

/// <summary>
/// OnlineCreateMatchResponse is a record type within the Online module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include MultiplayerServerHttpClient (Online), IOnlineMatchHttpClient (Online), OnlineSessionStateCoordinator (Online).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MultiplayerServerHttpClient (Online), IOnlineMatchHttpClient (Online), OnlineSessionStateCoordinator (Online), OnlineMatchTransportAdapter (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public sealed record OnlineCreateMatchResponse(
    string MatchId,
    string JoinCode,
    string CreatorToken);

/// <summary>
/// OnlineJoinMatchRequest is a record type within the Online module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include MultiplayerServerHttpClient (Online).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MultiplayerServerHttpClient (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public sealed record OnlineJoinMatchRequest(string? JoinCode);

/// <summary>
/// OnlineJoinMatchResponse is a record type within the Online module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include MultiplayerServerHttpClient (Online), IOnlineMatchHttpClient (Online), OnlineSessionStateCoordinator (Online).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MultiplayerServerHttpClient (Online), IOnlineMatchHttpClient (Online), OnlineSessionStateCoordinator (Online), OnlineMatchTransportAdapter (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public sealed record OnlineJoinMatchResponse(
    string MatchId,
    string Seat,
    string PlayerToken);

/// <summary>
/// OnlineSubmitMoveRequest is a record type within the Online module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include IOnlineMatchHttpClient (Online), MultiplayerServerHttpClient (Online), OnlineMatchTransportAdapter (Online).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> IOnlineMatchHttpClient (Online), MultiplayerServerHttpClient (Online), OnlineMatchTransportAdapter (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public sealed record OnlineSubmitMoveRequest(
    string? MatchId,
    string? PlayerToken,
    string? From,
    string? To,
    string? Promotion = null);

/// <summary>
/// OnlineSubmitMoveResponse is a record type within the Online module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include MultiplayerServerHttpClient (Online), IOnlineMatchHttpClient (Online), OnlineMatchTransportAdapter (Online).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MultiplayerServerHttpClient (Online), IOnlineMatchHttpClient (Online), OnlineMatchTransportAdapter (Online), IOnlineMatchTransportAdapter (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public sealed record OnlineSubmitMoveResponse(
    bool Accepted,
    OnlineMatchSnapshot Snapshot);

/// <summary>
/// OnlineSnapshotRequest is a record type within the Online module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include IOnlineMatchHttpClient (Online), MultiplayerServerHttpClient (Online), OnlineMatchTransportAdapter (Online).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> IOnlineMatchHttpClient (Online), MultiplayerServerHttpClient (Online), OnlineMatchTransportAdapter (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public sealed record OnlineSnapshotRequest(
    string? MatchId,
    string? PlayerToken);

/// <summary>
/// OnlineApiErrorResponse is a record type within the Online module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include MultiplayerServerHttpClient (Online).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MultiplayerServerHttpClient (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public sealed record OnlineApiErrorResponse(
    string Code,
    string Message);

/// <summary>
/// OnlineMatchSnapshot is a record type within the Online module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include OnlineMatchSessionService (Online), NoOpOnlineMatchSessionService (Online), OnlineSessionStateCoordinator (Online).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> OnlineMatchSessionService (Online), NoOpOnlineMatchSessionService (Online), OnlineSessionStateCoordinator (Online), OnlineRealtimeEventReducer (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public sealed record OnlineMatchSnapshot(
    string MatchId,
    string SideToMove,
    int MoveNumber,
    IReadOnlyList<string> Board,
    string Status = OnlineMatchProtocolConstants.MatchStatusInProgress,
    string? Resolution = null,
    string? WinnerSeat = null,
    OnlineMatchPresence? Presence = null);

/// <summary>
/// OnlineMatchPresence is a record type within the Online module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include OnlineMatchSnapshot (Online).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> OnlineMatchSnapshot (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public sealed record OnlineMatchPresence(
    OnlineSeatPresence Creator,
    OnlineSeatPresence Joiner);

/// <summary>
/// OnlineSeatPresence is a record type within the Online module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include OnlineMatchPresence (Online).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> OnlineMatchPresence (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public sealed record OnlineSeatPresence(
    string Seat,
    bool IsConnected,
    bool IsReserved,
    DateTimeOffset? DisconnectedUtc,
    DateTimeOffset? GraceExpiresUtc);

/// <summary>
/// OnlineMatchEventMetadata is a record type within the Online module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include OnlineMatchSnapshotSyncEvent (Online), OnlineMatchUpdatedSyncEvent (Online), OnlineMatchPresenceChangedSyncEvent (Online).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> OnlineMatchSnapshotSyncEvent (Online), OnlineMatchUpdatedSyncEvent (Online), OnlineMatchPresenceChangedSyncEvent (Online), OnlineMatchEndedSyncEvent (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public sealed record OnlineMatchEventMetadata(
    string MatchId,
    string EventId,
    long Sequence,
    DateTimeOffset OccurredUtc);

/// <summary>
/// OnlineMatchSnapshotSyncEvent is a record type within the Online module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include SignalROnlineMatchRealtimeClient (Online), OnlineRealtimeLifecycleManager (Online), IOnlineMatchRealtimeClient (Online).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> SignalROnlineMatchRealtimeClient (Online), OnlineRealtimeLifecycleManager (Online), IOnlineMatchRealtimeClient (Online), IOnlineRealtimeLifecycleManager (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public sealed record OnlineMatchSnapshotSyncEvent(
    string EventType,
    OnlineMatchEventMetadata Metadata,
    OnlineMatchSnapshot Snapshot);

/// <summary>
/// OnlineMatchUpdatedSyncEvent is a record type within the Online module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include SignalROnlineMatchRealtimeClient (Online), OnlineRealtimeLifecycleManager (Online), IOnlineMatchRealtimeClient (Online).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> SignalROnlineMatchRealtimeClient (Online), OnlineRealtimeLifecycleManager (Online), IOnlineMatchRealtimeClient (Online), IOnlineRealtimeLifecycleManager (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public sealed record OnlineMatchUpdatedSyncEvent(
    string EventType,
    OnlineMatchEventMetadata Metadata,
    OnlineMatchSnapshot Snapshot);

/// <summary>
/// OnlineMatchPresenceChangedSyncEvent is a record type within the Online module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include SignalROnlineMatchRealtimeClient (Online), OnlineRealtimeLifecycleManager (Online), IOnlineMatchRealtimeClient (Online).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> SignalROnlineMatchRealtimeClient (Online), OnlineRealtimeLifecycleManager (Online), IOnlineMatchRealtimeClient (Online), IOnlineRealtimeLifecycleManager (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public sealed record OnlineMatchPresenceChangedSyncEvent(
    string EventType,
    OnlineMatchEventMetadata Metadata,
    OnlineMatchSnapshot Snapshot,
    string Seat);

/// <summary>
/// OnlineMatchEndedSyncEvent is a record type within the Online module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include SignalROnlineMatchRealtimeClient (Online), OnlineRealtimeLifecycleManager (Online), IOnlineMatchRealtimeClient (Online).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> SignalROnlineMatchRealtimeClient (Online), OnlineRealtimeLifecycleManager (Online), IOnlineMatchRealtimeClient (Online), IOnlineRealtimeLifecycleManager (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public sealed record OnlineMatchEndedSyncEvent(
    string EventType,
    OnlineMatchEventMetadata Metadata,
    OnlineMatchSnapshot Snapshot);

/// <summary>
/// OnlineMatchErrorSyncEvent is a record type within the Online module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include SignalROnlineMatchRealtimeClient (Online), OnlineRealtimeLifecycleManager (Online), IOnlineMatchRealtimeClient (Online).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> SignalROnlineMatchRealtimeClient (Online), OnlineRealtimeLifecycleManager (Online), IOnlineMatchRealtimeClient (Online), IOnlineRealtimeLifecycleManager (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public sealed record OnlineMatchErrorSyncEvent(
    string EventType,
    OnlineMatchEventMetadata Metadata,
    string Code,
    string Message);

/// <summary>
/// OnlineMatchCredentials is a record type within the Online module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include OnlineSessionStateCoordinator (Online), IOnlineRealtimeLifecycleManager (Online), OnlineMatchSessionService (Online).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> OnlineSessionStateCoordinator (Online), IOnlineRealtimeLifecycleManager (Online), OnlineMatchSessionService (Online), OnlineRealtimeLifecycleManager (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public sealed record OnlineMatchCredentials(
    string MatchId,
    string PlayerToken,
    PieceColor Seat);

/// <summary>
/// OnlineCreatedMatch is a record type within the Online module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include OnlineMatchSessionService (Online), NoOpOnlineMatchSessionService (Online), IOnlineMatchSessionCommands (Online).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> OnlineMatchSessionService (Online), NoOpOnlineMatchSessionService (Online), IOnlineMatchSessionCommands (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public sealed record OnlineCreatedMatch(
    string MatchId,
    string JoinCode,
    PieceColor Seat);

/// <summary>
/// OnlineJoinedMatch is a record type within the Online module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include OnlineMatchSessionService (Online), NoOpOnlineMatchSessionService (Online), IOnlineMatchSessionCommands (Online).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> OnlineMatchSessionService (Online), NoOpOnlineMatchSessionService (Online), IOnlineMatchSessionCommands (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public sealed record OnlineJoinedMatch(
    string MatchId,
    PieceColor Seat);

/// <summary>
/// OnlineResumedMatch is a record type within the Online module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include OnlineMatchSessionService (Online), NoOpOnlineMatchSessionService (Online), IOnlineMatchSessionLifecycle (Online).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> OnlineMatchSessionService (Online), NoOpOnlineMatchSessionService (Online), IOnlineMatchSessionLifecycle (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public sealed record OnlineResumedMatch(
    string MatchId,
    PieceColor Seat);
