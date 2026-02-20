namespace Chess.Domain;

/// <summary>
/// Piece is a record type within the Domain module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include ChessMoveGenerator (Engine), ChessGameStatusEvaluator (Engine), InitialPositionBuilder (Engine).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> ChessMoveGenerator (Engine), ChessGameStatusEvaluator (Engine), InitialPositionBuilder (Engine), ChessMoveApplication (Engine)</para>
/// <para><b>Usage pattern:</b> Values are created or transformed by engine/application services, then propagated through persistence, online, and UI workflows as immutable state.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the chess domain model boundary and represents rule-level concepts.</para>
/// </remarks>
public sealed record Piece(PieceType Type, PieceColor Color, bool HasMoved = false);
