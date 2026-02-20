using System;

namespace Chess.Engine;

[Flags]

/// <summary>
/// EngineLogCategory is an enumeration within the Engine module.
/// Its named values model a bounded set of states, options, or outcomes used by collaborators.
/// Primary production consumers include MoveLookupFailureReason (Engine), ChessGameEngine (Engine), EngineLogEntry (Engine).
/// Its values are interpreted by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MoveLookupFailureReason (Engine), ChessGameEngine (Engine), EngineLogEntry (Engine), ChessGameEngineOptions (Engine)</para>
/// <para><b>Usage pattern:</b> Callers invoke it during legal move generation, move application, attack evaluation, and game-status checks inside the engine pipeline.</para>
/// <para><b>Dependencies/Collaborators:</b> Its values are interpreted by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the rules engine boundary and participates in move evaluation or state transition logic.</para>
/// </remarks>
public enum EngineLogCategory
{
    None = 0,
    MoveValidation = 1 << 0,
    SpecialMoves = 1 << 1,
    GameStatus = 1 << 2,
    All = MoveValidation | SpecialMoves | GameStatus
}
