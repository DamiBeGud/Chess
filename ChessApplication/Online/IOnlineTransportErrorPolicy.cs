using System;

namespace Chess.Online;

internal interface IOnlineTransportErrorPolicy
{
    OnlineUserError Map(Exception exception, string fallbackMessage);
}
