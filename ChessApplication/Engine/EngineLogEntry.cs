using Chess.Domain;

namespace Chess.Engine;

/// <summary>
/// EngineLogEntry is a record type within the Engine module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include MoveLookupFailureReason (Engine), IChessEngineLogger (Engine), NullChessEngineLogger (Engine).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MoveLookupFailureReason (Engine), IChessEngineLogger (Engine), NullChessEngineLogger (Engine)</para>
/// <para><b>Usage pattern:</b> Callers invoke it during legal move generation, move application, attack evaluation, and game-status checks inside the engine pipeline.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the rules engine boundary and participates in move evaluation or state transition logic.</para>
/// </remarks>
public readonly record struct EngineLogEntry(
    EngineLogCategory Category,
    string EventName,
    string Message,
    Square? From = null,
    Square? To = null,
    PieceColor? SideToMove = null,
    PieceType? PieceType = null,
    PieceType? PromotionPieceType = null,
    GameStatus? GameStatus = null);
