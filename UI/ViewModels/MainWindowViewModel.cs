using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Input;
using Chess.AppCore;
using Chess.Domain;
using Chess.UI.Assets;
using Chess.UI.Commands;

namespace Chess.UI.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private static readonly Square DefaultKeyboardFocusSquare = new(4, 1);
    private static readonly IReadOnlyList<string> DefaultFileCoordinates = BuildFileCoordinates();
    private static readonly IReadOnlyList<string> DefaultRankCoordinates = BuildRankCoordinates();

    private readonly IGameSessionService _gameSessionService;
    private readonly IPieceAssetResolver _pieceAssetResolver;
    private readonly IReadOnlyList<BoardSquareViewModel> _boardSquares;
    private readonly HashSet<Square> _legalDestinationSquares = [];
    private Square? _selectedSquare;
    private Square _focusedSquare = DefaultKeyboardFocusSquare;
    private string _gameStatusText = string.Empty;
    private string _lastActionText = string.Empty;
    private string _feedbackText = string.Empty;
    private string _focusedSquareText = string.Empty;
    private IReadOnlyList<string> _moveHistoryEntries = Array.Empty<string>();
    private string _persistenceFilePath = BuildDefaultPersistenceFilePath();

    public MainWindowViewModel(IGameSessionService gameSessionService)
        : this(gameSessionService, new PieceAssetResolver())
    {
    }

    public MainWindowViewModel(IGameSessionService gameSessionService, IPieceAssetResolver pieceAssetResolver)
    {
        System.ArgumentNullException.ThrowIfNull(gameSessionService);
        System.ArgumentNullException.ThrowIfNull(pieceAssetResolver);
        _gameSessionService = gameSessionService;
        _pieceAssetResolver = pieceAssetResolver;

        var squares = BuildBoardSquares();
        _boardSquares = new ReadOnlyCollection<BoardSquareViewModel>(squares);

        NewGameCommand = new RelayCommand(StartNewGame);
        SaveGameCommand = new RelayCommand(async () => await SaveGameAsync());
        LoadGameCommand = new RelayCommand(async () => await LoadGameAsync());
        StartNewGame();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title => "Chess";

    public IReadOnlyList<BoardSquareViewModel> BoardSquares => _boardSquares;

    public IReadOnlyList<string> FileCoordinates => DefaultFileCoordinates;

    public IReadOnlyList<string> RankCoordinates => DefaultRankCoordinates;

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

    public string FocusedSquareText
    {
        get => _focusedSquareText;
        private set
        {
            if (_focusedSquareText == value)
            {
                return;
            }

            _focusedSquareText = value;
            OnPropertyChanged();
        }
    }

    public string KeyboardHintText => "Keyboard: Arrow keys move focus, Enter/Space select or move, Esc clears selection.";

    public IReadOnlyList<string> MoveHistoryEntries
    {
        get => _moveHistoryEntries;
        private set
        {
            if (ReferenceEquals(_moveHistoryEntries, value))
            {
                return;
            }

            _moveHistoryEntries = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsMoveHistoryEmpty));
        }
    }

    public bool IsMoveHistoryEmpty => MoveHistoryEntries.Count == 0;

    public ICommand NewGameCommand { get; }

    public ICommand SaveGameCommand { get; }

    public ICommand LoadGameCommand { get; }

    public string PersistenceFilePath
    {
        get => _persistenceFilePath;
        set
        {
            if (_persistenceFilePath == value)
            {
                return;
            }

            _persistenceFilePath = value;
            OnPropertyChanged();
        }
    }

    public void StartNewGame()
    {
        _gameSessionService.StartNewGame();
        ClearSelection();
        SetFocusedSquare(DefaultKeyboardFocusSquare);
        FeedbackText = string.Empty;
        LastActionText = "Last action: Started a new game.";
        RefreshBoardFromCurrentState();
    }

    public async Task SaveGameAsync(CancellationToken cancellationToken = default)
    {
        var filePath = PersistenceFilePath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            FeedbackText = "Save file path is required.";
            return;
        }

        try
        {
            await _gameSessionService.SaveAsync(filePath, cancellationToken);
            LastActionText = $"Last action: Saved game to {filePath}.";
            FeedbackText = string.Empty;
        }
        catch (Exception exception) when (
            exception is InvalidDataException
            or UnauthorizedAccessException
            or IOException
            or ArgumentException)
        {
            FeedbackText = $"Unable to save game: {exception.Message}";
        }
    }

    public async Task LoadGameAsync(CancellationToken cancellationToken = default)
    {
        var filePath = PersistenceFilePath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            FeedbackText = "Save file path is required.";
            return;
        }

        try
        {
            await _gameSessionService.LoadAsync(filePath, cancellationToken);
            ClearSelection();
            SetFocusedSquare(DefaultKeyboardFocusSquare);
            LastActionText = $"Last action: Loaded game from {filePath}.";
            FeedbackText = string.Empty;
            RefreshBoardFromCurrentState();
        }
        catch (Exception exception) when (
            exception is InvalidDataException
            or UnauthorizedAccessException
            or IOException
            or ArgumentException)
        {
            FeedbackText = $"Unable to load game: {exception.Message}";
        }
    }

    public bool HandleKeyboardInput(Key key)
    {
        switch (key)
        {
            case Key.Left:
            case Key.A:
                MoveFocusedSquare(-1, 0);
                return true;
            case Key.Right:
            case Key.D:
                MoveFocusedSquare(1, 0);
                return true;
            case Key.Up:
            case Key.W:
                MoveFocusedSquare(0, 1);
                return true;
            case Key.Down:
            case Key.S:
                MoveFocusedSquare(0, -1);
                return true;
            case Key.Enter:
            case Key.Space:
                OnSquareClicked(_focusedSquare);
                return true;
            case Key.Escape:
                ClearSelection();
                FeedbackText = "Selection cleared.";
                return true;
            default:
                return false;
        }
    }

    private void OnSquareClicked(Square square)
    {
        SetFocusedSquare(square);
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
            SelectSquareAndSetFeedback(square);
            return;
        }

        FeedbackText = BuildInvalidMoveTargetFeedback(square, selectedSquare);
    }

    private void TrySelectSquare(Square square, GameState currentState)
    {
        if (!TryGetPieceAt(currentState, square, out var piece))
        {
            FeedbackText = $"No piece at {ToCoordinate(square)}. Select one of your {currentState.SideToMove} pieces.";
            return;
        }

        if (piece!.Color != currentState.SideToMove)
        {
            FeedbackText = $"Cannot select {piece.Color} piece at {ToCoordinate(square)}. It is {currentState.SideToMove} to move.";
            return;
        }

        SelectSquareAndSetFeedback(square);
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

    private bool SelectSquare(Square square)
    {
        _selectedSquare = square;
        _legalDestinationSquares.Clear();

        foreach (var move in _gameSessionService.GetLegalMovesFrom(square))
        {
            _legalDestinationSquares.Add(move.To);
        }

        UpdateSquareHighlights();
        return _legalDestinationSquares.Count > 0;
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
        MoveHistoryEntries = BuildMoveHistoryEntries(currentState.MoveHistory);
    }

    private void UpdateSquareHighlights()
    {
        foreach (var squareViewModel in _boardSquares)
        {
            var isSelected = _selectedSquare is not null && squareViewModel.Square == _selectedSquare.Value;
            var isLegalDestination = _legalDestinationSquares.Contains(squareViewModel.Square);
            var isKeyboardFocused = squareViewModel.Square == _focusedSquare;

            squareViewModel.SetSelected(isSelected);
            squareViewModel.SetLegalDestination(isLegalDestination);
            squareViewModel.SetKeyboardFocused(isKeyboardFocused);
        }
    }

    private void MoveFocusedSquare(int fileDelta, int rankDelta)
    {
        var nextFile = Math.Clamp(_focusedSquare.File + fileDelta, 0, 7);
        var nextRank = Math.Clamp(_focusedSquare.Rank + rankDelta, 0, 7);
        SetFocusedSquare(new Square(nextFile, nextRank));
    }

    private void SetFocusedSquare(Square square)
    {
        var nextFocusedSquareText = $"Keyboard focus: {ToCoordinate(square)}.";

        if (_focusedSquare == square && FocusedSquareText == nextFocusedSquareText)
        {
            return;
        }

        _focusedSquare = square;
        FocusedSquareText = nextFocusedSquareText;
        UpdateSquareHighlights();
    }

    private void SelectSquareAndSetFeedback(Square square)
    {
        var hasLegalMoves = SelectSquare(square);
        FeedbackText = hasLegalMoves
            ? string.Empty
            : $"Selected square {ToCoordinate(square)} has no legal moves.";
    }

    private string BuildInvalidMoveTargetFeedback(Square targetSquare, Square selectedSquare)
    {
        var legalDestinationsText = _legalDestinationSquares.Count == 0
            ? "none"
            : string.Join(", ", _legalDestinationSquares
                .Select(ToCoordinate)
                .OrderBy(coordinate => coordinate));

        return $"Invalid move target: {ToCoordinate(targetSquare)}. Legal destinations from {ToCoordinate(selectedSquare)}: {legalDestinationsText}.";
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
                var squareViewModel = new BoardSquareViewModel(
                    square,
                    new RelayCommand(() => OnSquareClicked(square)),
                    _pieceAssetResolver);
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

    private static IReadOnlyList<string> BuildMoveHistoryEntries(IReadOnlyList<Move> moveHistory)
    {
        if (moveHistory.Count == 0)
        {
            return Array.Empty<string>();
        }

        var entries = new List<string>(moveHistory.Count);

        for (var index = 0; index < moveHistory.Count; index++)
        {
            var moveNumber = (index / 2) + 1;
            var movePrefix = index % 2 == 0 ? $"{moveNumber}. " : $"{moveNumber}... ";
            entries.Add($"{movePrefix}{BuildMoveNotation(moveHistory[index])}");
        }

        return entries;
    }

    private static string BuildMoveNotation(Move move)
    {
        if (move.IsCastling)
        {
            return move.To.File > move.From.File ? "O-O" : "O-O-O";
        }

        var separator = move.CapturedPiece is not null || move.IsEnPassant ? "x" : "-";
        var notation = $"{ToCoordinate(move.From)}{separator}{ToCoordinate(move.To)}";

        if (move.PromotionPieceType is not null)
        {
            notation = $"{notation}={ToPromotionSymbol(move.PromotionPieceType.Value)}";
        }

        if (move.IsEnPassant)
        {
            notation = $"{notation} e.p.";
        }

        return notation;
    }

    private static string ToPromotionSymbol(PieceType pieceType)
    {
        return pieceType switch
        {
            PieceType.Queen => "Q",
            PieceType.Rook => "R",
            PieceType.Bishop => "B",
            PieceType.Knight => "N",
            PieceType.King => "K",
            PieceType.Pawn => "P",
            _ => pieceType.ToString()
        };
    }

    private static IReadOnlyList<string> BuildFileCoordinates()
    {
        return Enumerable.Range(0, 8)
            .Select(file => ((char)('a' + file)).ToString())
            .ToArray();
    }

    private static IReadOnlyList<string> BuildRankCoordinates()
    {
        return Enumerable.Range(0, 8)
            .Select(offset => (8 - offset).ToString())
            .ToArray();
    }

    private static string BuildDefaultPersistenceFilePath()
    {
        var localAppDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var baseDirectory = string.IsNullOrWhiteSpace(localAppDataDirectory)
            ? Environment.CurrentDirectory
            : localAppDataDirectory;

        return Path.Combine(baseDirectory, "Chess", "saved-game.json");
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
