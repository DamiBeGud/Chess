using System;
using Chess.Domain;

namespace Chess.Online;

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
