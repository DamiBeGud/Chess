namespace Chess.Engine;

/// <summary>
/// ChessGameEngineOptions is a record type within the Engine module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include ChessGameEngine (Engine).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> ChessGameEngine (Engine)</para>
/// <para><b>Usage pattern:</b> Callers invoke it during legal move generation, move application, attack evaluation, and game-status checks inside the engine pipeline.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the rules engine boundary and participates in move evaluation or state transition logic.</para>
/// </remarks>
public sealed record ChessGameEngineOptions(
    IChessEngineLogger? Logger = null,
    EngineLogCategory LogCategories = EngineLogCategory.None);
