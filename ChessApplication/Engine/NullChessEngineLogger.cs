namespace Chess.Engine;

/// <summary>
/// NullChessEngineLogger is a concrete type within the Engine module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include ChessGameEngine (Engine).
/// Key collaborators are IChessEngineLogger.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> ChessGameEngine (Engine)</para>
/// <para><b>Usage pattern:</b> Callers invoke it during legal move generation, move application, attack evaluation, and game-status checks inside the engine pipeline.</para>
/// <para><b>Dependencies/Collaborators:</b> IChessEngineLogger.</para>
/// <para><b>Boundary:</b> This type sits in the rules engine boundary and participates in move evaluation or state transition logic.</para>
/// </remarks>
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
