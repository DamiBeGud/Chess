using Chess.Domain;

namespace Chess.Online;

/// <summary>
/// IOnlineSnapshotGameStateMapper defines a contract within the Online module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include OnlineSessionStateCoordinator (Online), OnlineMatchSessionService (Online), OnlineSnapshotGameStateMapper (Online).
/// Key collaborators are Implementations include OnlineSnapshotGameStateMapper (Online).
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> OnlineSessionStateCoordinator (Online), OnlineMatchSessionService (Online), OnlineSnapshotGameStateMapper (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include OnlineSnapshotGameStateMapper (Online).</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public interface IOnlineSnapshotGameStateMapper
{
    GameState Map(OnlineMatchSnapshot snapshot);
}
