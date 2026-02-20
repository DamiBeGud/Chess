using System;
using Chess.Domain;

namespace Chess.Online;

/// <summary>
/// IOnlineMatchSessionReadModel defines a contract within the Online module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include MainWindowViewModel (UI/ViewModels), MainWindowOnlinePlayCoordinator (UI/Services), IOnlineMatchSessionService (Online).
/// Key collaborators are Implementations include IOnlineMatchSessionService (Online).
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MainWindowViewModel (UI/ViewModels), MainWindowOnlinePlayCoordinator (UI/Services), IOnlineMatchSessionService (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include IOnlineMatchSessionService (Online).</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public interface IOnlineMatchSessionReadModel
{
    event EventHandler? SessionStateChanged;
    event EventHandler<OnlineUserError>? SessionError;

    bool IsInMatch { get; }
    bool IsConnected { get; }
    string? MatchId { get; }
    string? JoinCode { get; }
    PieceColor? Seat { get; }
    OnlineMatchSnapshot? CurrentSnapshot { get; }
    GameState? CurrentGameState { get; }
    long LastSequence { get; }
}
