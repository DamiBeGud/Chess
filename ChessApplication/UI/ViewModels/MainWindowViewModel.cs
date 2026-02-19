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
using Chess.UI.Services;

namespace Chess.UI.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private static readonly Square DefaultKeyboardFocusSquare = new(4, 1);
    private static readonly IReadOnlyList<string> DefaultFileCoordinates = BuildFileCoordinates();
    private static readonly IReadOnlyList<string> DefaultRankCoordinates = BuildRankCoordinates();
    private static readonly IReadOnlyList<PieceColor> DefaultAiColors =
    [
        PieceColor.Black,
        PieceColor.White
    ];
    private static readonly IReadOnlyList<int> DefaultAiSearchDepths = [1, 2];

    private readonly IGameSessionService _gameSessionService;
    private readonly IAiTurnService _aiTurnService;
    private readonly IPieceAssetResolver _pieceAssetResolver;
    private readonly IMainWindowSelectionState _selectionState;
    private readonly IMainWindowTextFormatter _textFormatter;
    private readonly IMainWindowKeyboardNavigator _keyboardNavigator;
    private readonly IMainWindowAiTurnCoordinator _aiTurnCoordinator;
    private readonly IReadOnlyList<BoardSquareViewModel> _boardSquares;
    private string _gameStatusText = string.Empty;
    private string _lastActionText = string.Empty;
    private string _feedbackText = string.Empty;
    private string _focusedSquareText = string.Empty;
    private IReadOnlyList<MoveHistoryEntryViewModel> _moveHistoryEntries = Array.Empty<MoveHistoryEntryViewModel>();
    private string _persistenceFilePath = BuildDefaultPersistenceFilePath();
    private bool _showLegalMoveSuggestions = true;
    private bool _isPlayVsAiEnabled;
    private PieceColor _aiControlledColor = PieceColor.Black;
    private int _aiSearchDepth = 2;

    public MainWindowViewModel(IGameSessionService gameSessionService)
        : this(gameSessionService, new PieceAssetResolver(), new NoOpAiTurnService())
    {
    }

    public MainWindowViewModel(IGameSessionService gameSessionService, IAiTurnService aiTurnService)
        : this(gameSessionService, new PieceAssetResolver(), aiTurnService)
    {
    }

    public MainWindowViewModel(IGameSessionService gameSessionService, IPieceAssetResolver pieceAssetResolver)
        : this(gameSessionService, pieceAssetResolver, new NoOpAiTurnService())
    {
    }

    public MainWindowViewModel(
        IGameSessionService gameSessionService,
        IPieceAssetResolver pieceAssetResolver,
        IAiTurnService aiTurnService)
        : this(
            gameSessionService,
            pieceAssetResolver,
            aiTurnService,
            new MainWindowSelectionState(DefaultKeyboardFocusSquare),
            new MainWindowTextFormatter(pieceAssetResolver),
            new MainWindowKeyboardNavigator(),
            new MainWindowAiTurnCoordinator(aiTurnService))
    {
    }

    internal MainWindowViewModel(
        IGameSessionService gameSessionService,
        IPieceAssetResolver pieceAssetResolver,
        IAiTurnService aiTurnService,
        IMainWindowSelectionState selectionState,
        IMainWindowTextFormatter textFormatter,
        IMainWindowKeyboardNavigator keyboardNavigator,
        IMainWindowAiTurnCoordinator aiTurnCoordinator)
    {
        ArgumentNullException.ThrowIfNull(gameSessionService);
        ArgumentNullException.ThrowIfNull(pieceAssetResolver);
        ArgumentNullException.ThrowIfNull(aiTurnService);
        ArgumentNullException.ThrowIfNull(selectionState);
        ArgumentNullException.ThrowIfNull(textFormatter);
        ArgumentNullException.ThrowIfNull(keyboardNavigator);
        ArgumentNullException.ThrowIfNull(aiTurnCoordinator);

        _gameSessionService = gameSessionService;
        _pieceAssetResolver = pieceAssetResolver;
        _aiTurnService = aiTurnService;
        _selectionState = selectionState;
        _textFormatter = textFormatter;
        _keyboardNavigator = keyboardNavigator;
        _aiTurnCoordinator = aiTurnCoordinator;

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

    public bool ShowLegalMoveSuggestions
    {
        get => _showLegalMoveSuggestions;
        set
        {
            if (_showLegalMoveSuggestions == value)
            {
                return;
            }

            _showLegalMoveSuggestions = value;
            OnPropertyChanged();
            UpdateSquareHighlights();
        }
    }

    public IReadOnlyList<MoveHistoryEntryViewModel> MoveHistoryEntries
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

    public IReadOnlyList<PieceColor> AvailableAiColors => DefaultAiColors;

    public IReadOnlyList<int> AvailableAiSearchDepths => DefaultAiSearchDepths;

    public bool IsAiThinking => _aiTurnCoordinator.IsAiTurnInProgress;

    public bool IsAiAvailable => _aiTurnService is not NoOpAiTurnService;

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

    public bool IsPlayVsAiEnabled
    {
        get => _isPlayVsAiEnabled;
        set
        {
            if (value && !IsAiAvailable)
            {
                FeedbackText = "Play vs AI is unavailable in this configuration.";
                return;
            }

            if (_isPlayVsAiEnabled == value)
            {
                return;
            }

            if (!value)
            {
                _aiTurnCoordinator.CancelInFlightAiTurn();
            }

            _isPlayVsAiEnabled = value;
            OnPropertyChanged();

            if (_isPlayVsAiEnabled)
            {
                QueueAiTurnIfNeeded();
            }
        }
    }

    public PieceColor AiControlledColor
    {
        get => _aiControlledColor;
        set
        {
            if (_aiControlledColor == value)
            {
                return;
            }

            _aiControlledColor = value;
            OnPropertyChanged();

            if (IsPlayVsAiEnabled)
            {
                _aiTurnCoordinator.CancelInFlightAiTurn();
                QueueAiTurnIfNeeded();
            }
        }
    }

    public int AiSearchDepth
    {
        get => _aiSearchDepth;
        set
        {
            var boundedDepth = Math.Clamp(value, 1, 2);
            if (_aiSearchDepth == boundedDepth)
            {
                return;
            }

            _aiSearchDepth = boundedDepth;
            OnPropertyChanged();
        }
    }

    public void StartNewGame()
    {
        _aiTurnCoordinator.CancelInFlightAiTurn();
        _gameSessionService.StartNewGame();
        ClearSelection();
        SetFocusedSquare(DefaultKeyboardFocusSquare);
        FeedbackText = string.Empty;
        LastActionText = "Last action: Started a new game.";
        RefreshBoardFromCurrentState();
        QueueAiTurnIfNeeded();
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
            _aiTurnCoordinator.CancelInFlightAiTurn();
            await _gameSessionService.LoadAsync(filePath, cancellationToken);
            ClearSelection();
            SetFocusedSquare(DefaultKeyboardFocusSquare);
            LastActionText = $"Last action: Loaded game from {filePath}.";
            FeedbackText = string.Empty;
            RefreshBoardFromCurrentState();
            QueueAiTurnIfNeeded();
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
        var action = _keyboardNavigator.Resolve(key);

        switch (action.Kind)
        {
            case MainWindowKeyboardActionKind.MoveFocus:
                MoveFocusedSquare(action.FileDelta, action.RankDelta);
                return true;
            case MainWindowKeyboardActionKind.CommitFocusedSquare:
                if (IsHumanInputBlockedByAiTurn())
                {
                    FeedbackText = BuildAiThinkingFeedback();
                    QueueAiTurnIfNeeded();
                    return true;
                }

                OnSquareClicked(_selectionState.FocusedSquare);
                return true;
            case MainWindowKeyboardActionKind.ClearSelection:
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

        if (IsHumanInputBlockedByAiTurn())
        {
            FeedbackText = BuildAiThinkingFeedback();
            QueueAiTurnIfNeeded();
            return;
        }

        if (_selectionState.SelectedSquare is null)
        {
            TrySelectSquare(square, currentState);
            return;
        }

        var selectedSquare = _selectionState.SelectedSquare.Value;
        if (selectedSquare == square)
        {
            ClearSelection();
            FeedbackText = string.Empty;
            return;
        }

        if (_selectionState.IsLegalDestination(square))
        {
            ExecuteMove(selectedSquare, square, currentState);
            return;
        }

        if (TryGetPieceAt(currentState, square, out var pieceAtSquare) && pieceAtSquare!.Color == currentState.SideToMove)
        {
            SelectSquareAndSetFeedback(square);
            return;
        }

        FeedbackText = _textFormatter.BuildInvalidMoveTargetFeedback(
            square,
            selectedSquare,
            _selectionState.LegalDestinationSquares);
    }

    private void TrySelectSquare(Square square, GameState currentState)
    {
        if (!TryGetPieceAt(currentState, square, out var piece))
        {
            FeedbackText = _textFormatter.BuildNoPieceSelectionFeedback(square, currentState.SideToMove);
            return;
        }

        if (piece!.Color != currentState.SideToMove)
        {
            FeedbackText = _textFormatter.BuildOpponentPieceSelectionFeedback(piece, square, currentState.SideToMove);
            return;
        }

        SelectSquareAndSetFeedback(square);
    }

    private void ExecuteMove(Square fromSquare, Square toSquare, GameState previousState)
    {
        if (_gameSessionService.TryMakeMove(fromSquare, toSquare))
        {
            PieceType? movedPieceType = TryGetPieceAt(previousState, fromSquare, out var movingPiece)
                ? movingPiece!.Type
                : null;
            LastActionText = _textFormatter.BuildHumanMoveLastAction(previousState, fromSquare, toSquare, movedPieceType);
            FeedbackText = string.Empty;
        }
        else
        {
            FeedbackText = _textFormatter.BuildMoveRejectedFeedback(fromSquare, toSquare);
        }

        ClearSelection();
        RefreshBoardFromCurrentState();
        QueueAiTurnIfNeeded();
    }

    private bool SelectSquare(Square square)
    {
        var legalMoves = _gameSessionService.GetLegalMovesFrom(square);
        var hasLegalMoves = _selectionState.SelectSquare(square, legalMoves);
        UpdateSquareHighlights();
        return hasLegalMoves;
    }

    private void ClearSelection()
    {
        _selectionState.ClearSelection();
        UpdateSquareHighlights();
    }

    private void RefreshBoardFromCurrentState()
    {
        var currentState = _gameSessionService.CurrentGameState;
        var piecesBySquare = currentState.Pieces.ToDictionary(placement => placement.Square, placement => placement.Piece);
        var hasLastMove = TryGetLastMoveSquares(currentState.MoveHistory, out var lastMoveFromSquare, out var lastMoveToSquare);

        foreach (var squareViewModel in _boardSquares)
        {
            squareViewModel.SetPiece(piecesBySquare.TryGetValue(squareViewModel.Square, out var piece) ? piece : null);
            squareViewModel.SetLastMoveHighlighted(
                hasLastMove
                && (squareViewModel.Square == lastMoveFromSquare || squareViewModel.Square == lastMoveToSquare));
        }

        GameStatusText = _textFormatter.BuildGameStatusText(currentState);
        MoveHistoryEntries = _textFormatter.BuildMoveHistoryEntries(currentState.MoveHistory);
    }

    private void UpdateSquareHighlights()
    {
        _selectionState.ApplyHighlights(_boardSquares, _showLegalMoveSuggestions);
    }

    private bool IsHumanInputBlockedByAiTurn()
    {
        return _aiTurnCoordinator.IsHumanInputBlockedByAiTurn(
            IsPlayVsAiEnabled,
            AiControlledColor,
            _gameSessionService.CurrentGameState);
    }

    private string BuildAiThinkingFeedback()
    {
        return $"AI ({AiControlledColor}) is thinking...";
    }

    private void QueueAiTurnIfNeeded()
    {
        _aiTurnCoordinator.QueueAiTurnIfNeeded(
            isPlayVsAiEnabledProvider: () => IsPlayVsAiEnabled,
            aiControlledColorProvider: () => AiControlledColor,
            aiSearchDepthProvider: () => AiSearchDepth,
            aiThinkingFeedbackProvider: BuildAiThinkingFeedback,
            setFeedback: feedback => FeedbackText = feedback,
            setLastActionFromAiMove: aiMove => LastActionText = _textFormatter.BuildAiMoveLastAction(AiControlledColor, aiMove),
            clearSelectionAndRefreshBoard: () =>
            {
                ClearSelection();
                RefreshBoardFromCurrentState();
            },
            notifyIsAiThinkingChanged: () => OnPropertyChanged(nameof(IsAiThinking)));
    }

    private void MoveFocusedSquare(int fileDelta, int rankDelta)
    {
        _selectionState.MoveFocusedSquare(fileDelta, rankDelta);
        FocusedSquareText = _textFormatter.BuildFocusedSquareText(_selectionState.FocusedSquare);
        UpdateSquareHighlights();
    }

    private void SetFocusedSquare(Square square)
    {
        _selectionState.SetFocusedSquare(square);
        FocusedSquareText = _textFormatter.BuildFocusedSquareText(_selectionState.FocusedSquare);
        UpdateSquareHighlights();
    }

    private void SelectSquareAndSetFeedback(Square square)
    {
        var hasLegalMoves = SelectSquare(square);
        FeedbackText = hasLegalMoves
            ? string.Empty
            : _textFormatter.BuildNoLegalMovesFeedback(square);
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

    private static bool TryGetLastMoveSquares(IReadOnlyList<Move> moveHistory, out Square fromSquare, out Square toSquare)
    {
        if (moveHistory.Count == 0)
        {
            fromSquare = default;
            toSquare = default;
            return false;
        }

        var lastMove = moveHistory[^1];
        fromSquare = lastMove.From;
        toSquare = lastMove.To;
        return true;
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

    private sealed class NoOpAiTurnService : IAiTurnService
    {
        public bool CanRequestMove(PieceColor aiColor)
        {
            return false;
        }

        public Task<Move?> TryPlayTurnAsync(
            PieceColor aiColor,
            int searchDepth,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Move?>(null);
        }
    }
}
