using System;

namespace Chess.Engine;

[Flags]
public enum EngineLogCategory
{
    None = 0,
    MoveValidation = 1 << 0,
    SpecialMoves = 1 << 1,
    GameStatus = 1 << 2,
    All = MoveValidation | SpecialMoves | GameStatus
}
