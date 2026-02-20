using System;
using Chess.AppCore;
using Chess.Domain;

namespace Chess.UI.Services;

internal sealed class MainWindowLocalPlayCoordinator : IMainWindowLocalPlayCoordinator
{
    private readonly IGameSessionService _gameSessionService;
    private readonly IMainWindowSelectionState _selectionState;
    private readonly IMainWindowTextFormatter _textFormatter;

    internal MainWindowLocalPlayCoordinator(
        IGameSessionService gameSessionService,
        IMainWindowSelectionState selectionState,
        IMainWindowTextFormatter textFormatter)
    {
        _gameSessionService = gameSessionService ?? throw new ArgumentNullException(nameof(gameSessionService));
        _selectionState = selectionState ?? throw new ArgumentNullException(nameof(selectionState));
        _textFormatter = textFormatter ?? throw new ArgumentNullException(nameof(textFormatter));
    }

    public void HandleSquareClicked(Square square, IMainWindowLocalPlayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.SetFocusedSquare(square);
        var currentState = _gameSessionService.CurrentGameState;

        if (currentState.Status != GameStatus.InProgress)
        {
            context.SetFeedback($"Game is finished ({currentState.Status}). Start a new game to continue.");
            return;
        }

        if (context.IsHumanInputBlockedByAiTurn())
        {
            context.SetFeedback(context.BuildAiThinkingFeedback());
            context.QueueAiTurnIfNeeded();
            return;
        }

        if (_selectionState.SelectedSquare is null)
        {
            TrySelectSquare(square, currentState, context);
            return;
        }

        var selectedSquare = _selectionState.SelectedSquare.Value;
        if (selectedSquare == square)
        {
            context.ClearSelection();
            context.SetFeedback(string.Empty);
            return;
        }

        if (_selectionState.IsLegalDestination(square))
        {
            ExecuteMove(selectedSquare, square, currentState, context);
            return;
        }

        if (TryGetPieceAt(currentState, square, out var pieceAtSquare) && pieceAtSquare!.Color == currentState.SideToMove)
        {
            SelectSquareAndSetFeedback(square, context);
            return;
        }

        context.SetFeedback(
            _textFormatter.BuildInvalidMoveTargetFeedback(
                square,
                selectedSquare,
                _selectionState.LegalDestinationSquares));
    }

    private void TrySelectSquare(Square square, GameState currentState, IMainWindowLocalPlayContext context)
    {
        if (!TryGetPieceAt(currentState, square, out var piece))
        {
            context.SetFeedback(_textFormatter.BuildNoPieceSelectionFeedback(square, currentState.SideToMove));
            return;
        }

        if (piece!.Color != currentState.SideToMove)
        {
            context.SetFeedback(_textFormatter.BuildOpponentPieceSelectionFeedback(piece, square, currentState.SideToMove));
            return;
        }

        SelectSquareAndSetFeedback(square, context);
    }

    private void SelectSquareAndSetFeedback(Square square, IMainWindowLocalPlayContext context)
    {
        var legalMoves = _gameSessionService.GetLegalMovesFrom(square);
        var hasLegalMoves = _selectionState.SelectSquare(square, legalMoves);
        context.UpdateSquareHighlights();
        context.SetFeedback(hasLegalMoves
            ? string.Empty
            : _textFormatter.BuildNoLegalMovesFeedback(square));
    }

    private void ExecuteMove(
        Square fromSquare,
        Square toSquare,
        GameState previousState,
        IMainWindowLocalPlayContext context)
    {
        if (_gameSessionService.TryMakeMove(fromSquare, toSquare))
        {
            PieceType? movedPieceType = TryGetPieceAt(previousState, fromSquare, out var movingPiece)
                ? movingPiece!.Type
                : null;
            context.SetLastAction(_textFormatter.BuildHumanMoveLastAction(previousState, fromSquare, toSquare, movedPieceType));
            context.SetFeedback(string.Empty);
        }
        else
        {
            context.SetFeedback(_textFormatter.BuildMoveRejectedFeedback(fromSquare, toSquare));
        }

        context.ClearSelection();
        context.RefreshBoardFromCurrentState();
        context.QueueAiTurnIfNeeded();
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
