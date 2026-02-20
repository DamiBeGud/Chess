using System;

namespace Chess.Online;

public enum OnlineUserAction
{
    None = 0,
    Retry = 1,
    WaitForOpponent = 2,
    RequestResync = 3,
    Reconnect = 4,
    StartNewMatch = 5
}

public sealed record OnlineTransportError(
    string Code,
    string Message,
    int? StatusCode = null);

public sealed record OnlineUserError(
    string Code,
    string Message,
    OnlineUserAction RecommendedAction,
    bool IsTerminal = false);

public interface IOnlineErrorMapper
{
    OnlineUserError Map(OnlineTransportError error);
}

public sealed class OnlineErrorMapper : IOnlineErrorMapper
{
    public OnlineUserError Map(OnlineTransportError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error.Code switch
        {
            OnlineMatchProtocolConstants.ErrorJoinCodeRequired =>
                new OnlineUserError(error.Code, "Join code is required.", OnlineUserAction.Retry),
            OnlineMatchProtocolConstants.ErrorMatchIdRequired =>
                new OnlineUserError(error.Code, "Match ID is required.", OnlineUserAction.Retry),
            OnlineMatchProtocolConstants.ErrorPlayerTokenRequired =>
                new OnlineUserError(error.Code, "Session credentials are incomplete. Rejoin the match.", OnlineUserAction.Reconnect),
            OnlineMatchProtocolConstants.ErrorMoveCoordinatesRequired =>
                new OnlineUserError(error.Code, "Move coordinates are required.", OnlineUserAction.Retry),
            OnlineMatchProtocolConstants.ErrorInvalidPromotion =>
                new OnlineUserError(error.Code, "Promotion piece must be one of Q, R, B, or N.", OnlineUserAction.Retry),
            OnlineMatchProtocolConstants.ErrorInvalidPlayerToken =>
                new OnlineUserError(error.Code, "Session is no longer valid. Rejoin the match.", OnlineUserAction.Reconnect),
            OnlineMatchProtocolConstants.ErrorUnauthorizedResume =>
                new OnlineUserError(error.Code, "Session token does not match this seat.", OnlineUserAction.Reconnect),
            OnlineMatchProtocolConstants.ErrorMatchNotFound =>
                new OnlineUserError(error.Code, "Match was not found.", OnlineUserAction.StartNewMatch, IsTerminal: true),
            OnlineMatchProtocolConstants.ErrorMatchNotReady =>
                new OnlineUserError(error.Code, "Waiting for the opponent to join.", OnlineUserAction.WaitForOpponent),
            OnlineMatchProtocolConstants.ErrorMatchFull =>
                new OnlineUserError(error.Code, "This match already has two players.", OnlineUserAction.StartNewMatch, IsTerminal: true),
            OnlineMatchProtocolConstants.ErrorOutOfTurn =>
                new OnlineUserError(error.Code, "It is not your turn yet.", OnlineUserAction.WaitForOpponent),
            OnlineMatchProtocolConstants.ErrorIllegalMove =>
                new OnlineUserError(error.Code, "The server rejected that move as illegal.", OnlineUserAction.Retry),
            OnlineMatchProtocolConstants.ErrorSeatNotReconnectable =>
                new OnlineUserError(error.Code, "This seat can no longer be resumed.", OnlineUserAction.StartNewMatch, IsTerminal: true),
            OnlineMatchProtocolConstants.ErrorGraceExpired =>
                new OnlineUserError(error.Code, "Reconnect grace period expired.", OnlineUserAction.StartNewMatch, IsTerminal: true),
            OnlineMatchProtocolConstants.ErrorMatchAlreadyEnded =>
                new OnlineUserError(error.Code, "Match already ended.", OnlineUserAction.StartNewMatch, IsTerminal: true),
            OnlineMatchProtocolConstants.ErrorTransportNotSubscribed =>
                new OnlineUserError(error.Code, "Realtime subscription was lost. Resyncing is required.", OnlineUserAction.RequestResync),
            _ => MapFallback(error)
        };
    }

    private static OnlineUserError MapFallback(OnlineTransportError error)
    {
        if (error.StatusCode is 403)
        {
            return new OnlineUserError(error.Code, "Authorization failed for this match session.", OnlineUserAction.Reconnect);
        }

        if (error.StatusCode is 404)
        {
            return new OnlineUserError(error.Code, "Match no longer exists.", OnlineUserAction.StartNewMatch, IsTerminal: true);
        }

        if (error.StatusCode is 409)
        {
            return new OnlineUserError(error.Code, "Match state conflict. Request a resync.", OnlineUserAction.RequestResync);
        }

        return new OnlineUserError(error.Code, string.IsNullOrWhiteSpace(error.Message) ? "Online request failed." : error.Message, OnlineUserAction.Retry);
    }
}
