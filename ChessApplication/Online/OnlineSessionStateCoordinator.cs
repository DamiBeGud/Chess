using System;
using Chess.Domain;

namespace Chess.Online;

internal sealed class OnlineSessionStateCoordinator
{
    private readonly IOnlineSnapshotGameStateMapper _snapshotMapper;
    private readonly OnlineRealtimeEventReducer _reducer;

    internal OnlineSessionStateCoordinator(
        IOnlineSnapshotGameStateMapper snapshotMapper,
        OnlineRealtimeEventReducer reducer)
    {
        _snapshotMapper = snapshotMapper ?? throw new ArgumentNullException(nameof(snapshotMapper));
        _reducer = reducer ?? throw new ArgumentNullException(nameof(reducer));
    }

    public OnlineMatchCredentials? Credentials { get; private set; }

    public string? JoinCode { get; private set; }

    public OnlineMatchSnapshot? CurrentSnapshot { get; private set; }

    public GameState? CurrentGameState { get; private set; }

    public long LastSequence { get; private set; }

    public bool IsResyncInFlight { get; private set; }

    public void SetCreatedCredentials(OnlineCreateMatchResponse created)
    {
        ArgumentNullException.ThrowIfNull(created);

        Credentials = new OnlineMatchCredentials(created.MatchId, created.CreatorToken, PieceColor.White);
        JoinCode = created.JoinCode;
    }

    public bool TrySetJoinedCredentials(OnlineJoinMatchResponse joined, out PieceColor seat)
    {
        ArgumentNullException.ThrowIfNull(joined);

        if (!TryParseSeat(joined.Seat, out seat))
        {
            return false;
        }

        Credentials = new OnlineMatchCredentials(joined.MatchId, joined.PlayerToken, seat);
        JoinCode = null;
        return true;
    }

    public void SetResumedCredentials(string matchId, string playerToken, PieceColor seat)
    {
        Credentials = new OnlineMatchCredentials(matchId, playerToken, seat);
        JoinCode = null;
    }

    public void Reset()
    {
        Credentials = null;
        JoinCode = null;
        LastSequence = 0;
        IsResyncInFlight = false;
        CurrentSnapshot = null;
        CurrentGameState = null;
    }

    public OnlineUserError? TryApplySnapshot(OnlineMatchSnapshot snapshot)
    {
        try
        {
            CurrentSnapshot = snapshot;
            CurrentGameState = _snapshotMapper.Map(snapshot);
            return null;
        }
        catch (Exception)
        {
            return new OnlineUserError(
                "invalid_snapshot",
                "Received an invalid snapshot from the server.",
                OnlineUserAction.RequestResync);
        }
    }

    public OnlineRealtimeApplyResult ApplyRealtimeSnapshot(long sequence, OnlineMatchSnapshot snapshot, out OnlineUserError? applyError)
    {
        var reduction = _reducer.ReduceSnapshot(CurrentSnapshot, LastSequence, sequence, snapshot);
        applyError = null;

        if (reduction.Status is OnlineRealtimeApplyStatus.Applied)
        {
            LastSequence = reduction.UpdatedSequence;
            applyError = TryApplySnapshot(snapshot);
        }

        return reduction;
    }

    public OnlineRealtimeApplyResult ApplyRealtimeErrorMetadata(long sequence)
    {
        var reduction = _reducer.ReduceMetadataOnly(LastSequence, sequence);
        if (reduction.Status is OnlineRealtimeApplyStatus.Applied)
        {
            LastSequence = reduction.UpdatedSequence;
        }

        return reduction;
    }

    public bool TryBeginResync()
    {
        if (IsResyncInFlight)
        {
            return false;
        }

        IsResyncInFlight = true;
        return true;
    }

    public void EndResync()
    {
        IsResyncInFlight = false;
    }

    private static bool TryParseSeat(string seatValue, out PieceColor seat)
    {
        if (string.Equals(seatValue, OnlineMatchProtocolConstants.CreatorSeat, StringComparison.Ordinal))
        {
            seat = PieceColor.White;
            return true;
        }

        if (string.Equals(seatValue, OnlineMatchProtocolConstants.JoinerSeat, StringComparison.Ordinal))
        {
            seat = PieceColor.Black;
            return true;
        }

        seat = default;
        return false;
    }
}
