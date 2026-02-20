using System;
using Microsoft.AspNetCore.Http.Connections.Client;

namespace Chess.Online;

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
