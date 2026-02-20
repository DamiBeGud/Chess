using System;
using Microsoft.AspNetCore.SignalR;

namespace Chess.Online;

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
