using System.Collections.Generic;

namespace Chess.Domain;

/// <summary>
/// GameState is a record type within the Domain module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include ChessGameEngine (Engine), IGameEngine (Engine), ChessMoveGenerator (Engine).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> ChessGameEngine (Engine), IGameEngine (Engine), ChessMoveGenerator (Engine), ChessGameStatusEvaluator (Engine)</para>
/// <para><b>Usage pattern:</b> Values are created or transformed by engine/application services, then propagated through persistence, online, and UI workflows as immutable state.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the chess domain model boundary and represents rule-level concepts.</para>
/// </remarks>
public sealed record GameState(
    IReadOnlyList<PiecePlacement> Pieces,
    PieceColor SideToMove,
    CastlingRights CastlingRights,
    Square? EnPassantTarget,
    int HalfmoveClock,
    int FullmoveNumber,
    GameStatus Status,
    IReadOnlyList<Move> MoveHistory,
    IReadOnlyList<string>? PositionHistory = null,
    int SchemaVersion = 2);
