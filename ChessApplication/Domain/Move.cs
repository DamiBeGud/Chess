namespace Chess.Domain;

/// <summary>
/// Move is a record type within the Domain module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include ChessMoveGenerator (Engine), ChessMoveApplication (Engine), MoveLookupFailureReason (Engine).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> ChessMoveGenerator (Engine), ChessMoveApplication (Engine), MoveLookupFailureReason (Engine), NegamaxAiMoveSelector (AI)</para>
/// <para><b>Usage pattern:</b> Values are created or transformed by engine/application services, then propagated through persistence, online, and UI workflows as immutable state.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the chess domain model boundary and represents rule-level concepts.</para>
/// </remarks>
public sealed record Move(
    Square From,
    Square To,
    Piece MovedPiece,
    Piece? CapturedPiece = null,
    bool IsCastling = false,
    bool IsEnPassant = false,
    PieceType? PromotionPieceType = null);
