using System;
using Microsoft.AspNetCore.Http.Connections.Client;

namespace Chess.Online;

/// <summary>
/// SignalROnlineMatchRealtimeClientFactory is a concrete type within the Online module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include App (AppShell).
/// Key collaborators are Uri, HttpConnectionOptions, IOnlineMatchRealtimeClientFactory.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> App (AppShell)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> Uri, HttpConnectionOptions, IOnlineMatchRealtimeClientFactory.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public sealed class SignalROnlineMatchRealtimeClientFactory : IOnlineMatchRealtimeClientFactory
{
    private readonly Uri _baseUri;
    private readonly Action<HttpConnectionOptions>? _configureConnection;

    public SignalROnlineMatchRealtimeClientFactory(
        Uri baseUri,
        Action<HttpConnectionOptions>? configureConnection = null)
    {
        _baseUri = baseUri ?? throw new ArgumentNullException(nameof(baseUri));
        _configureConnection = configureConnection;
    }

    public IOnlineMatchRealtimeClient CreateClient()
    {
        return new SignalROnlineMatchRealtimeClient(_baseUri, _configureConnection);
    }
}
