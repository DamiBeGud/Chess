using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Chess.AppCore;
using Chess.Domain;
using Chess.UI.Commands;

namespace Chess.UI.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IGameSessionService _gameSessionService;
    private readonly IReadOnlyList<BoardSquareViewModel> _boardSquares;
    private readonly HashSet<Square> _legalDestinationSquares = [];
    private Square? _selectedSquare;
    private string _gameStatusText = string.Empty;
    private string _lastActionText = string.Empty;
    private string _feedbackText = string.Empty;

    public MainWindowViewModel(IGameSessionService gameSessionService)
    {
        System.ArgumentNullException.ThrowIfNull(gameSessionService);
        _gameSessionService = gameSessionService;

        var squares = BuildBoardSquares();
        _boardSquares = new ReadOnlyCollection<BoardSquareViewModel>(squares);

        NewGameCommand = new RelayCommand(StartNewGame);
        StartNewGame();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title => "Chess";

    public IReadOnlyList<BoardSquareViewModel> BoardSquares => _boardSquares;

    public string GameStatusText
    {
        get => _gameStatusText;
        private set
        {
            if (_gameStatusText == value)
            {
                return;
            }

            _gameStatusText = value;
            OnPropertyChanged();
        }
    }

    public string LastActionText
    {
        get => _lastActionText;
        private set
        {
            if (_lastActionText == value)
            {
                return;
            }

            _lastActionText = value;
            OnPropertyChanged();
        }
    }

    public string FeedbackText
    {
        get => _feedbackText;
        private set
        {
            if (_feedbackText == value)
            {
                return;
            }

            _feedbackText = value;
            OnPropertyChanged();
        }
    }

    public ICommand NewGameCommand { get; }

    public void StartNewGame()
    {
        _gameSessionService.StartNewGame();
        ClearSelection();
        FeedbackText = string.Empty;
        LastActionText = "Last action: Started a new game.";
        RefreshBoardFromCurrentState();
    }

    private void OnSquareClicked(Square square)
    {
        var currentState = _gameSessionService.CurrentGameState;

        if (currentState.Status != GameStatus.InProgress)
        {
            FeedbackText = $"Game is finished ({currentState.Status}). Start a new game to continue.";
            return;
        }

        if (_selectedSquare is null)
        {
            TrySelectSquare(square, currentState);
            return;
        }

        var selectedSquare = _selectedSquare.Value;

        if (selectedSquare == square)
        {
            ClearSelection();
            FeedbackText = string.Empty;
            return;
        }

        if (_legalDestinationSquares.Contains(square))
        {
            ExecuteMove(selectedSquare, square, currentState);
            return;
        }

        if (TryGetPieceAt(currentState, square, out var pieceAtSquare) && pieceAtSquare!.Color == currentState.SideToMove)
        {
            SelectSquare(square);
            FeedbackText = string.Empty;
            return;
        }

        FeedbackText = $"Invalid move target: {ToCoordinate(square)}.";
    }

    private void TrySelectSquare(Square square, GameState currentState)
    {
        if (!TryGetPieceAt(currentState, square, out var piece))
        {
            FeedbackText = $"No piece at {ToCoordinate(square)}.";
            return;
        }

        if (piece!.Color != currentState.SideToMove)
        {
            FeedbackText = $"It is {currentState.SideToMove} to move.";
            return;
        }

        SelectSquare(square);
        FeedbackText = string.Empty;
    }

    private void ExecuteMove(Square fromSquare, Square toSquare, GameState previousState)
    {
        if (_gameSessionService.TryMakeMove(fromSquare, toSquare))
        {
            var movedPieceName = TryGetPieceAt(previousState, fromSquare, out var movingPiece)
                ? movingPiece!.Type.ToString()
                : "Piece";

            LastActionText = $"Last action: {previousState.SideToMove} moved {movedPieceName} from {ToCoordinate(fromSquare)} to {ToCoordinate(toSquare)}.";
            FeedbackText = string.Empty;
        }
        else
        {
            FeedbackText = $"Move rejected: {ToCoordinate(fromSquare)} -> {ToCoordinate(toSquare)}.";
        }

        ClearSelection();
        RefreshBoardFromCurrentState();
    }

    private void SelectSquare(Square square)
    {
        _selectedSquare = square;
        _legalDestinationSquares.Clear();

        foreach (var move in _gameSessionService.GetLegalMovesFrom(square))
        {
            _legalDestinationSquares.Add(move.To);
        }

        UpdateSquareHighlights();
    }

    private void ClearSelection()
    {
        _selectedSquare = null;
        _legalDestinationSquares.Clear();
        UpdateSquareHighlights();
    }

    private void RefreshBoardFromCurrentState()
    {
        var currentState = _gameSessionService.CurrentGameState;
        var piecesBySquare = currentState.Pieces.ToDictionary(placement => placement.Square, placement => placement.Piece);

        foreach (var squareViewModel in _boardSquares)
        {
            squareViewModel.SetPiece(piecesBySquare.TryGetValue(squareViewModel.Square, out var piece) ? piece : null);
        }

        GameStatusText = BuildGameStatusText(currentState);
    }

    private void UpdateSquareHighlights()
    {
        foreach (var squareViewModel in _boardSquares)
        {
            var isSelected = _selectedSquare is not null && squareViewModel.Square == _selectedSquare.Value;
            var isLegalDestination = _legalDestinationSquares.Contains(squareViewModel.Square);

            squareViewModel.SetSelected(isSelected);
            squareViewModel.SetLegalDestination(isLegalDestination);
        }
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

    private List<BoardSquareViewModel> BuildBoardSquares()
    {
        var boardSquares = new List<BoardSquareViewModel>(64);

        for (var rank = 7; rank >= 0; rank--)
        {
            for (var file = 0; file < 8; file++)
            {
                var square = new Square(file, rank);
                var squareViewModel = new BoardSquareViewModel(square, new RelayCommand(() => OnSquareClicked(square)));
                boardSquares.Add(squareViewModel);
            }
        }

        return boardSquares;
    }

    private static string ToCoordinate(Square square)
    {
        return $"{(char)('a' + square.File)}{square.Rank + 1}";
    }

    private static string BuildGameStatusText(GameState gameState)
    {
        return gameState.Status switch
        {
            GameStatus.InProgress => $"Status: In progress. Side to move: {gameState.SideToMove}.",
            GameStatus.WhiteWin => "Status: White wins.",
            GameStatus.BlackWin => "Status: Black wins.",
            GameStatus.Draw => "Status: Draw.",
            _ => $"Status: {gameState.Status}."
        };
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
