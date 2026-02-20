using System;
using System.Threading.Tasks;
using Chess.Domain;
using Chess.Online;

namespace Chess.UI.Services;

internal sealed class MainWindowOnlinePlayCoordinator : IMainWindowOnlinePlayCoordinator
{
    private readonly IOnlineMatchSessionService _onlineMatchSessionService;
    private readonly IMainWindowSelectionState _selectionState;
    private readonly IMainWindowTextFormatter _textFormatter;

    internal MainWindowOnlinePlayCoordinator(
        IOnlineMatchSessionService onlineMatchSessionService,
        IMainWindowSelectionState selectionState,
        IMainWindowTextFormatter textFormatter)
    {
        _onlineMatchSessionService = onlineMatchSessionService ?? throw new ArgumentNullException(nameof(onlineMatchSessionService));
        _selectionState = selectionState ?? throw new ArgumentNullException(nameof(selectionState));
        _textFormatter = textFormatter ?? throw new ArgumentNullException(nameof(textFormatter));
    }

    public async Task HandleSquareClickedAsync(Square square, IMainWindowOnlinePlayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.SetFocusedSquare(square);

        if (context.IsOnlineOperationInProgress)
        {
            return;
        }

        var currentState = context.GetOnlineGameState();
        if (currentState is null)
        {
            context.SetFeedback("Online state is not ready yet. Try resync.");
            return;
        }

        if (currentState.Status != GameStatus.InProgress)
        {
            context.SetFeedback($"Game is finished ({currentState.Status}). Leave the match to start a new one.");
            return;
        }

        if (_onlineMatchSessionService.Seat is not PieceColor localSeat)
        {
            context.SetFeedback("Online seat is unknown. Try reconnecting.");
            return;
        }

        if (_selectionState.SelectedSquare is null)
        {
            if (!TryGetPieceAt(currentState, square, out var piece))
            {
                context.SetFeedback(_textFormatter.BuildNoPieceSelectionFeedback(square, localSeat));
                return;
            }

            if (piece!.Color != localSeat)
            {
                context.SetFeedback(_textFormatter.BuildOpponentPieceSelectionFeedback(piece, square, localSeat));
                return;
            }

            if (currentState.SideToMove != localSeat)
            {
                context.SetFeedback("Waiting for opponent move.");
                return;
            }

            _selectionState.SelectSquareWithoutLegalDestinations(square);
            context.UpdateSquareHighlights();
            context.SetFeedback(string.Empty);
            return;
        }

        var selectedSquare = _selectionState.SelectedSquare.Value;
        if (selectedSquare == square)
        {
            context.ClearSelection();
            context.SetFeedback(string.Empty);
            return;
        }

        await context.RunOnlineOperationWithBusyStateAsync(
            async () =>
            {
                var submitResult = await _onlineMatchSessionService.SubmitMoveAsync(selectedSquare, square);
                if (!submitResult.IsSuccess)
                {
                    context.SetFeedback(submitResult.Error?.Message ?? "Move submission failed.");
                    return;
                }

                context.SetLastAction(
                    $"Last action: Submitted online move {_textFormatter.ToCoordinate(selectedSquare)} to {_textFormatter.ToCoordinate(square)}.");
                context.SetFeedback(string.Empty);
                context.ClearSelection();
                context.RefreshBoardFromCurrentState();
            });
    }

    public Task CreateOnlineMatchAsync(IMainWindowOnlinePlayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.RunOnlineOperationWithBusyStateAsync(
            async () =>
            {
                context.CancelInFlightAiTurn();
                context.DisablePlayVsAi();

                var result = await _onlineMatchSessionService.CreateMatchAsync();
                if (!result.IsSuccess)
                {
                    context.SetFeedback(result.Error?.Message ?? "Unable to create online match.");
                    return;
                }

                var created = result.Value!;
                context.SetLastAction($"Last action: Created online match {created.MatchId}. Share join code {created.JoinCode}.");
                context.SetFeedback(string.Empty);
                context.ClearSelection();
                context.RefreshBoardFromCurrentState();
                context.UpdateOnlineSessionText();
                context.NotifyAiAvailabilityChanged();
            });
    }

    public Task JoinOnlineMatchAsync(string? onlineJoinCode, IMainWindowOnlinePlayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.RunOnlineOperationWithBusyStateAsync(
            async () =>
            {
                var joinCode = onlineJoinCode?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(joinCode))
                {
                    context.SetFeedback("Join code is required.");
                    return;
                }

                context.CancelInFlightAiTurn();
                context.DisablePlayVsAi();

                var result = await _onlineMatchSessionService.JoinMatchAsync(joinCode);
                if (!result.IsSuccess)
                {
                    context.SetFeedback(result.Error?.Message ?? "Unable to join online match.");
                    return;
                }

                var joined = result.Value!;
                context.SetLastAction($"Last action: Joined online match {joined.MatchId} as {joined.Seat}.");
                context.SetFeedback(string.Empty);
                context.ClearSelection();
                context.RefreshBoardFromCurrentState();
                context.UpdateOnlineSessionText();
                context.NotifyAiAvailabilityChanged();
            });
    }

    public Task LeaveOnlineMatchAsync(IMainWindowOnlinePlayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.RunOnlineOperationWithBusyStateAsync(
            async () =>
            {
                await _onlineMatchSessionService.LeaveMatchAsync();
                context.StartNewLocalGame();
                context.ClearSelection();
                context.SetFocusedSquareToDefault();
                context.RefreshBoardFromCurrentState();
                context.SetLastAction("Last action: Left online match and started a local game.");
                context.SetFeedback(string.Empty);
                context.UpdateOnlineSessionText();
                context.NotifyOnlineMatchActiveChanged();
                context.NotifyAiAvailabilityChanged();
            });
    }

    public Task ResyncOnlineMatchAsync(IMainWindowOnlinePlayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.RunOnlineOperationWithBusyStateAsync(
            async () =>
            {
                var result = await _onlineMatchSessionService.RequestResyncAsync();
                if (!result.IsSuccess)
                {
                    context.SetFeedback(result.Error?.Message ?? "Unable to resync online match.");
                    return;
                }

                context.SetLastAction("Last action: Resynced online match state.");
                context.SetFeedback(string.Empty);
                context.ClearSelection();
                context.RefreshBoardFromCurrentState();
                context.UpdateOnlineSessionText();
            });
    }

    private static bool TryGetPieceAt(GameState gameState, Square square, out Piece? piece)
    {
        foreach (var placement in gameState.Pieces)
        {
            if (placement.Square != square)
            {
                continue;
            }

            piece = placement.Piece;
            return true;
        }

        piece = null;
        return false;
    }
}
