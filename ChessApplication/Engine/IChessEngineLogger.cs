namespace Chess.Engine;

/// <summary>
/// IChessEngineLogger defines a contract within the Engine module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include ChessGameEngine (Engine), NullChessEngineLogger (Engine), ChessGameEngineOptions (Engine).
/// Key collaborators are Implementations include NullChessEngineLogger (Engine).
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> ChessGameEngine (Engine), NullChessEngineLogger (Engine), ChessGameEngineOptions (Engine)</para>
/// <para><b>Usage pattern:</b> Callers invoke it during legal move generation, move application, attack evaluation, and game-status checks inside the engine pipeline.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include NullChessEngineLogger (Engine).</para>
/// <para><b>Boundary:</b> This type sits in the rules engine boundary and participates in move evaluation or state transition logic.</para>
/// </remarks>
public interface IChessEngineLogger
{
    bool IsEnabled { get; }
    void Log(in EngineLogEntry entry);
}
