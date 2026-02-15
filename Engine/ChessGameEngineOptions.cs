namespace Chess.Engine;

public sealed record ChessGameEngineOptions(
    IChessEngineLogger? Logger = null,
    EngineLogCategory LogCategories = EngineLogCategory.None);
