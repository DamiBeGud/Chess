namespace Chess.Online;

/// <summary>
/// IOnlineMatchSessionService defines a contract within the Online module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include MainWindow (AppShell), MainWindowViewModel (UI/ViewModels), NoOpOnlineMatchSessionService (Online).
/// Key collaborators are Implementations include NoOpOnlineMatchSessionService (Online), OnlineMatchSessionService (Online); related base contracts include IOnlineMatchSessionReadModel, IOnlineMatchSessionCommands, IOnlineMatchSessionLifecycle.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MainWindow (AppShell), MainWindowViewModel (UI/ViewModels), NoOpOnlineMatchSessionService (Online), OnlineMatchSessionService (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include NoOpOnlineMatchSessionService (Online), OnlineMatchSessionService (Online); related base contracts include IOnlineMatchSessionReadModel, IOnlineMatchSessionCommands, IOnlineMatchSessionLifecycle.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public interface IOnlineMatchSessionService :
    IOnlineMatchSessionReadModel,
    IOnlineMatchSessionCommands,
    IOnlineMatchSessionLifecycle
{
}
