namespace Chess.Engine;

public interface IChessEngineLogger
{
    bool IsEnabled { get; }
    void Log(in EngineLogEntry entry);
}
