using System;
using Microsoft.AspNetCore.SignalR;

namespace Chess.Online;

/// <summary>
/// OnlineTransportErrorPolicy is a concrete type within the Online module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include OnlineMatchSessionService (Online).
/// Key collaborators are IOnlineErrorMapper, IOnlineTransportErrorPolicy.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> OnlineMatchSessionService (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> IOnlineErrorMapper, IOnlineTransportErrorPolicy.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
internal sealed class OnlineTransportErrorPolicy : IOnlineTransportErrorPolicy
{
    private readonly IOnlineErrorMapper _errorMapper;

    internal OnlineTransportErrorPolicy(IOnlineErrorMapper errorMapper)
    {
        _errorMapper = errorMapper ?? throw new ArgumentNullException(nameof(errorMapper));
    }

    public OnlineUserError Map(Exception exception, string fallbackMessage)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var transportError = ToTransportError(exception, fallbackMessage);
        return _errorMapper.Map(transportError);
    }

    private static OnlineTransportError ToTransportError(Exception exception, string fallbackMessage)
    {
        if (exception is HubException hubException
            && TryParseHubExceptionMessage(hubException.Message, out var code, out var message))
        {
            return new OnlineTransportError(code, message);
        }

        var messageValue = string.IsNullOrWhiteSpace(exception.Message)
            ? fallbackMessage
            : exception.Message;
        return new OnlineTransportError("transport_error", messageValue);
    }

    private static bool TryParseHubExceptionMessage(
        string? hubMessage,
        out string code,
        out string message)
    {
        code = string.Empty;
        message = string.Empty;

        if (string.IsNullOrWhiteSpace(hubMessage))
        {
            return false;
        }

        var separatorIndex = hubMessage.IndexOf(':');
        if (separatorIndex <= 0 || separatorIndex == hubMessage.Length - 1)
        {
            return false;
        }

        code = hubMessage[..separatorIndex].Trim();
        message = hubMessage[(separatorIndex + 1)..].Trim();
        return !string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(message);
    }
}
