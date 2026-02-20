using System;

namespace Chess.Online;

/// <summary>
/// IOnlineTransportErrorPolicy defines a contract within the Online module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include OnlineRealtimeLifecycleManager (Online), OnlineTransportErrorPolicy (Online).
/// Key collaborators are Implementations include OnlineTransportErrorPolicy (Online).
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> OnlineRealtimeLifecycleManager (Online), OnlineTransportErrorPolicy (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include OnlineTransportErrorPolicy (Online).</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
internal interface IOnlineTransportErrorPolicy
{
    OnlineUserError Map(Exception exception, string fallbackMessage);
}
