namespace Chess.Engine;

public sealed class NullChessEngineLogger : IChessEngineLogger
{
    public static NullChessEngineLogger Instance { get; } = new();

    private NullChessEngineLogger()
    {
    }

    public bool IsEnabled => false;

    public void Log(in EngineLogEntry entry)
    {
    }
}
