namespace Chess.Domain;

/// <summary>
/// PiecePlacement is a record type within the Domain module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include InitialPositionBuilder (Engine), OnlineSnapshotGameStateMapper (Online), ChessEngineBoard (Engine).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> InitialPositionBuilder (Engine), OnlineSnapshotGameStateMapper (Online), ChessEngineBoard (Engine), GameState (Domain)</para>
/// <para><b>Usage pattern:</b> Values are created or transformed by engine/application services, then propagated through persistence, online, and UI workflows as immutable state.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the chess domain model boundary and represents rule-level concepts.</para>
/// </remarks>
public sealed record PiecePlacement(Square Square, Piece Piece);
