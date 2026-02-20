using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Input;
using Chess.AppCore;
using Chess.Domain;
using Chess.Online;
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
    private readonly IOnlineMatchSessionService _onlineMatchSessionService;
    private readonly IReadOnlyList<BoardSquareViewModel> _boardSquares;
    private readonly AsyncRelayCommand _createOnlineMatchCommand;
    private readonly AsyncRelayCommand _joinOnlineMatchCommand;
    private readonly AsyncRelayCommand _leaveOnlineMatchCommand;
    private readonly AsyncRelayCommand _resyncOnlineMatchCommand;
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
    private string _onlineJoinCode = string.Empty;
    private string _onlineSessionText = "Online: not connected.";
    private bool _isOnlineOperationInProgress;

    public MainWindowViewModel(IGameSessionService gameSessionService)
        : this(gameSessionService, new PieceAssetResolver(), new NoOpAiTurnService(), NoOpOnlineMatchSessionService.Instance)
    {
    }

    public MainWindowViewModel(IGameSessionService gameSessionService, IAiTurnService aiTurnService)
        : this(gameSessionService, new PieceAssetResolver(), aiTurnService, NoOpOnlineMatchSessionService.Instance)
    {
    }

    public MainWindowViewModel(IGameSessionService gameSessionService, IPieceAssetResolver pieceAssetResolver)
        : this(gameSessionService, pieceAssetResolver, new NoOpAiTurnService(), NoOpOnlineMatchSessionService.Instance)
    {
    }

    public MainWindowViewModel(
        IGameSessionService gameSessionService,
        IPieceAssetResolver pieceAssetResolver,
        IAiTurnService aiTurnService)
        : this(gameSessionService, pieceAssetResolver, aiTurnService, NoOpOnlineMatchSessionService.Instance)
    {
    }

    public MainWindowViewModel(
        IGameSessionService gameSessionService,
        IPieceAssetResolver pieceAssetResolver,
        IAiTurnService aiTurnService,
        IOnlineMatchSessionService onlineMatchSessionService)
        : this(
            gameSessionService,
            pieceAssetResolver,
            aiTurnService,
            new MainWindowSelectionState(DefaultKeyboardFocusSquare),
            new MainWindowTextFormatter(pieceAssetResolver),
            new MainWindowKeyboardNavigator(),
            new MainWindowAiTurnCoordinator(aiTurnService),
            onlineMatchSessionService)
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
        : this(
            gameSessionService,
            pieceAssetResolver,
            aiTurnService,
            selectionState,
            textFormatter,
            keyboardNavigator,
            aiTurnCoordinator,
            NoOpOnlineMatchSessionService.Instance)
    {
    }

    internal MainWindowViewModel(
        IGameSessionService gameSessionService,
        IPieceAssetResolver pieceAssetResolver,
        IAiTurnService aiTurnService,
        IMainWindowSelectionState selectionState,
        IMainWindowTextFormatter textFormatter,
        IMainWindowKeyboardNavigator keyboardNavigator,
        IMainWindowAiTurnCoordinator aiTurnCoordinator,
        IOnlineMatchSessionService onlineMatchSessionService)
    {
        ArgumentNullException.ThrowIfNull(gameSessionService);
        ArgumentNullException.ThrowIfNull(pieceAssetResolver);
        ArgumentNullException.ThrowIfNull(aiTurnService);
        ArgumentNullException.ThrowIfNull(selectionState);
        ArgumentNullException.ThrowIfNull(textFormatter);
        ArgumentNullException.ThrowIfNull(keyboardNavigator);
        ArgumentNullException.ThrowIfNull(aiTurnCoordinator);
        ArgumentNullException.ThrowIfNull(onlineMatchSessionService);

        _gameSessionService = gameSessionService;
        _pieceAssetResolver = pieceAssetResolver;
        _aiTurnService = aiTurnService;
        _selectionState = selectionState;
        _textFormatter = textFormatter;
        _keyboardNavigator = keyboardNavigator;
        _aiTurnCoordinator = aiTurnCoordinator;
        _onlineMatchSessionService = onlineMatchSessionService;

        var squares = BuildBoardSquares();
        _boardSquares = new ReadOnlyCollection<BoardSquareViewModel>(squares);

        NewGameCommand = new RelayCommand(StartNewGame);
        SaveGameCommand = new RelayCommand(async () => await SaveGameAsync());
        LoadGameCommand = new RelayCommand(async () => await LoadGameAsync());
        _createOnlineMatchCommand = new AsyncRelayCommand(
            CreateOnlineMatchAsync,
            exception => HandleOnlineCommandException(OnlineOperation.CreateMatch, exception),
            CanCreateOrJoinOnlineMatch);
        _joinOnlineMatchCommand = new AsyncRelayCommand(
            JoinOnlineMatchAsync,
            exception => HandleOnlineCommandException(OnlineOperation.JoinMatch, exception),
            CanCreateOrJoinOnlineMatch);
        _leaveOnlineMatchCommand = new AsyncRelayCommand(
            LeaveOnlineMatchAsync,
            exception => HandleOnlineCommandException(OnlineOperation.LeaveMatch, exception),
            CanLeaveOrResyncOnlineMatch);
        _resyncOnlineMatchCommand = new AsyncRelayCommand(
            ResyncOnlineMatchAsync,
            exception => HandleOnlineCommandException(OnlineOperation.ResyncMatch, exception),
            CanLeaveOrResyncOnlineMatch);
        CreateOnlineMatchCommand = _createOnlineMatchCommand;
        JoinOnlineMatchCommand = _joinOnlineMatchCommand;
        LeaveOnlineMatchCommand = _leaveOnlineMatchCommand;
        ResyncOnlineMatchCommand = _resyncOnlineMatchCommand;

        _onlineMatchSessionService.SessionStateChanged += OnOnlineSessionStateChanged;
        _onlineMatchSessionService.SessionError += OnOnlineSessionError;
        UpdateOnlineSessionText();
        NotifyOnlineCommandCanExecuteChanged();

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

    public bool IsAiAvailable => _aiTurnService is not NoOpAiTurnService && !IsOnlineMatchActive;

    public bool IsOnlineMatchActive => _onlineMatchSessionService.IsInMatch;

    public bool IsOnlineOperationInProgress => _isOnlineOperationInProgress;

    public ICommand NewGameCommand { get; }

    public ICommand SaveGameCommand { get; }

    public ICommand LoadGameCommand { get; }

    public ICommand CreateOnlineMatchCommand { get; }

    public ICommand JoinOnlineMatchCommand { get; }

    public ICommand LeaveOnlineMatchCommand { get; }

    public ICommand ResyncOnlineMatchCommand { get; }

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
            if (value && IsOnlineMatchActive)
            {
                FeedbackText = "Play vs AI is disabled while an online match is active.";
                return;
            }

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

    public string OnlineJoinCode
    {
        get => _onlineJoinCode;
        set
        {
            var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
            if (_onlineJoinCode == normalized)
            {
                return;
            }

            _onlineJoinCode = normalized;
            OnPropertyChanged();
        }
    }

    public string OnlineSessionText
    {
        get => _onlineSessionText;
        private set
        {
            if (_onlineSessionText == value)
            {
                return;
            }

            _onlineSessionText = value;
            OnPropertyChanged();
        }
    }

    public void StartNewGame()
    {
        if (IsOnlineMatchActive)
        {
            FeedbackText = "Leave the online match before starting a local game.";
            return;
        }

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
        if (IsOnlineMatchActive)
        {
            FeedbackText = "Saving local files is disabled while an online match is active.";
            return;
        }

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
        if (IsOnlineMatchActive)
        {
            FeedbackText = "Loading local files is disabled while an online match is active.";
            return;
        }

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
                if (!IsOnlineMatchActive && IsHumanInputBlockedByAiTurn())
                {
                    FeedbackText = BuildAiThinkingFeedback();
                    QueueAiTurnIfNeeded();
                    return true;
                }

                ExecuteSquareClick(_selectionState.FocusedSquare);
                return true;
            case MainWindowKeyboardActionKind.ClearSelection:
                ClearSelection();
                FeedbackText = "Selection cleared.";
                return true;
            default:
                return false;
        }
    }

    private void ExecuteSquareClick(Square square)
    {
        var squareViewModel = _boardSquares[GetBoardSquareIndex(square)];
        var clickCommand = squareViewModel.ClickCommand;

        if (!clickCommand.CanExecute(null))
        {
            return;
        }

        clickCommand.Execute(null);
    }

    private async Task OnSquareClickedAsync(Square square)
    {
        if (IsOnlineMatchActive)
        {
            await OnOnlineSquareClickedAsync(square);
            return;
        }

        OnLocalSquareClicked(square);
    }

    private void OnLocalSquareClicked(Square square)
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

    private async Task OnOnlineSquareClickedAsync(Square square)
    {
        SetFocusedSquare(square);

        if (_isOnlineOperationInProgress)
        {
            return;
        }

        var currentState = GetDisplayGameState();
        if (currentState is null)
        {
            FeedbackText = "Online state is not ready yet. Try resync.";
            return;
        }

        if (currentState.Status != GameStatus.InProgress)
        {
            FeedbackText = $"Game is finished ({currentState.Status}). Leave the match to start a new one.";
            return;
        }

        if (_onlineMatchSessionService.Seat is not PieceColor localSeat)
        {
            FeedbackText = "Online seat is unknown. Try reconnecting.";
            return;
        }

        if (_selectionState.SelectedSquare is null)
        {
            if (!TryGetPieceAt(currentState, square, out var piece))
            {
                FeedbackText = _textFormatter.BuildNoPieceSelectionFeedback(square, localSeat);
                return;
            }

            if (piece!.Color != localSeat)
            {
                FeedbackText = _textFormatter.BuildOpponentPieceSelectionFeedback(piece, square, localSeat);
                return;
            }

            if (currentState.SideToMove != localSeat)
            {
                FeedbackText = "Waiting for opponent move.";
                return;
            }

            _selectionState.SelectSquareWithoutLegalDestinations(square);
            UpdateSquareHighlights();
            FeedbackText = string.Empty;
            return;
        }

        var selectedSquare = _selectionState.SelectedSquare.Value;
        if (selectedSquare == square)
        {
            ClearSelection();
            FeedbackText = string.Empty;
            return;
        }

        await RunOnlineOperationWithBusyStateAsync(
            async () =>
            {
                var submitResult = await _onlineMatchSessionService.SubmitMoveAsync(selectedSquare, square);
                if (!submitResult.IsSuccess)
                {
                    FeedbackText = submitResult.Error?.Message ?? "Move submission failed.";
                    return;
                }

                LastActionText = $"Last action: Submitted online move {_textFormatter.ToCoordinate(selectedSquare)} to {_textFormatter.ToCoordinate(square)}.";
                FeedbackText = string.Empty;
                ClearSelection();
                RefreshBoardFromCurrentState();
            });
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
        if (IsOnlineMatchActive)
        {
            _selectionState.SelectSquareWithoutLegalDestinations(square);
            UpdateSquareHighlights();
            return true;
        }

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
        var currentState = GetDisplayGameState() ?? _gameSessionService.CurrentGameState;
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
            GetDisplayGameState() ?? _gameSessionService.CurrentGameState);
    }

    private string BuildAiThinkingFeedback()
    {
        return $"AI ({AiControlledColor}) is thinking...";
    }

    private void QueueAiTurnIfNeeded()
    {
        if (IsOnlineMatchActive)
        {
            return;
        }

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

    private bool BeginOnlineOperation()
    {
        if (_isOnlineOperationInProgress)
        {
            return false;
        }

        _isOnlineOperationInProgress = true;
        OnPropertyChanged(nameof(IsOnlineOperationInProgress));
        NotifyOnlineCommandCanExecuteChanged();
        return true;
    }

    private void EndOnlineOperation()
    {
        if (!_isOnlineOperationInProgress)
        {
            return;
        }

        _isOnlineOperationInProgress = false;
        OnPropertyChanged(nameof(IsOnlineOperationInProgress));
        NotifyOnlineCommandCanExecuteChanged();
    }

    private async Task RunOnlineOperationWithBusyStateAsync(Func<Task> operationAsync)
    {
        if (!BeginOnlineOperation())
        {
            return;
        }

        try
        {
            await operationAsync();
        }
        finally
        {
            EndOnlineOperation();
        }
    }

    private bool CanCreateOrJoinOnlineMatch()
    {
        return !_isOnlineOperationInProgress && !IsOnlineMatchActive;
    }

    private bool CanLeaveOrResyncOnlineMatch()
    {
        return !_isOnlineOperationInProgress && IsOnlineMatchActive;
    }

    private void NotifyOnlineCommandCanExecuteChanged()
    {
        _createOnlineMatchCommand.NotifyCanExecuteChanged();
        _joinOnlineMatchCommand.NotifyCanExecuteChanged();
        _leaveOnlineMatchCommand.NotifyCanExecuteChanged();
        _resyncOnlineMatchCommand.NotifyCanExecuteChanged();
    }

    private GameState? GetDisplayGameState()
    {
        if (_onlineMatchSessionService.IsInMatch && _onlineMatchSessionService.CurrentGameState is not null)
        {
            return _onlineMatchSessionService.CurrentGameState;
        }

        return _gameSessionService.CurrentGameState;
    }

    private Task CreateOnlineMatchAsync()
    {
        return RunOnlineOperationWithBusyStateAsync(
            async () =>
            {
                _aiTurnCoordinator.CancelInFlightAiTurn();
                IsPlayVsAiEnabled = false;

                var result = await _onlineMatchSessionService.CreateMatchAsync();
                if (!result.IsSuccess)
                {
                    FeedbackText = result.Error?.Message ?? "Unable to create online match.";
                    return;
                }

                var created = result.Value!;
                LastActionText = $"Last action: Created online match {created.MatchId}. Share join code {created.JoinCode}.";
                FeedbackText = string.Empty;
                ClearSelection();
                RefreshBoardFromCurrentState();
                UpdateOnlineSessionText();
                OnPropertyChanged(nameof(IsAiAvailable));
            });
    }

    private Task JoinOnlineMatchAsync()
    {
        return RunOnlineOperationWithBusyStateAsync(
            async () =>
            {
                var joinCode = OnlineJoinCode?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(joinCode))
                {
                    FeedbackText = "Join code is required.";
                    return;
                }

                _aiTurnCoordinator.CancelInFlightAiTurn();
                IsPlayVsAiEnabled = false;

                var result = await _onlineMatchSessionService.JoinMatchAsync(joinCode);
                if (!result.IsSuccess)
                {
                    FeedbackText = result.Error?.Message ?? "Unable to join online match.";
                    return;
                }

                var joined = result.Value!;
                LastActionText = $"Last action: Joined online match {joined.MatchId} as {joined.Seat}.";
                FeedbackText = string.Empty;
                ClearSelection();
                RefreshBoardFromCurrentState();
                UpdateOnlineSessionText();
                OnPropertyChanged(nameof(IsAiAvailable));
            });
    }

    private Task LeaveOnlineMatchAsync()
    {
        return RunOnlineOperationWithBusyStateAsync(
            async () =>
            {
                await _onlineMatchSessionService.LeaveMatchAsync();
                _gameSessionService.StartNewGame();
                ClearSelection();
                SetFocusedSquare(DefaultKeyboardFocusSquare);
                RefreshBoardFromCurrentState();
                LastActionText = "Last action: Left online match and started a local game.";
                FeedbackText = string.Empty;
                UpdateOnlineSessionText();
                OnPropertyChanged(nameof(IsOnlineMatchActive));
                OnPropertyChanged(nameof(IsAiAvailable));
            });
    }

    private Task ResyncOnlineMatchAsync()
    {
        return RunOnlineOperationWithBusyStateAsync(
            async () =>
            {
                var result = await _onlineMatchSessionService.RequestResyncAsync();
                if (!result.IsSuccess)
                {
                    FeedbackText = result.Error?.Message ?? "Unable to resync online match.";
                    return;
                }

                LastActionText = "Last action: Resynced online match state.";
                FeedbackText = string.Empty;
                ClearSelection();
                RefreshBoardFromCurrentState();
                UpdateOnlineSessionText();
            });
    }

    private void OnOnlineSessionStateChanged(object? sender, EventArgs e)
    {
        RefreshBoardFromCurrentState();
        UpdateOnlineSessionText();
        OnPropertyChanged(nameof(IsOnlineMatchActive));
        OnPropertyChanged(nameof(IsAiAvailable));
        NotifyOnlineCommandCanExecuteChanged();
    }

    private void OnOnlineSessionError(object? sender, OnlineUserError error)
    {
        FeedbackText = error.Message;
        UpdateOnlineSessionText();
    }

    private void UpdateOnlineSessionText()
    {
        if (!_onlineMatchSessionService.IsInMatch)
        {
            OnlineSessionText = "Online: not connected.";
            return;
        }

        var matchId = _onlineMatchSessionService.MatchId ?? "(unknown)";
        var seatText = _onlineMatchSessionService.Seat?.ToString() ?? "(unknown)";
        var connectionText = _onlineMatchSessionService.IsConnected ? "connected" : "disconnected";
        var joinCodeText = string.IsNullOrWhiteSpace(_onlineMatchSessionService.JoinCode)
            ? string.Empty
            : $" Join code: {_onlineMatchSessionService.JoinCode}.";

        OnlineSessionText = $"Online: {seatText} in match {matchId} ({connectionText}).{joinCodeText}";
    }

    private void HandleOnlineCommandException(OnlineOperation operation, Exception exception)
    {
        FeedbackText = MapOnlineCommandException(operation, exception);
    }

    private static string MapOnlineCommandException(OnlineOperation operation, Exception exception)
    {
        if (exception is OperationCanceledException)
        {
            return BuildOnlineOperationMessage(operation, "Request was canceled.");
        }

        if (exception is HttpRequestException or TimeoutException or IOException)
        {
            return BuildOnlineOperationMessage(operation, "Network error. Check your connection and try again.");
        }

        return BuildOnlineOperationMessage(operation, "Unexpected error. Try again.");
    }

    private static string BuildOnlineOperationMessage(OnlineOperation operation, string detail)
    {
        return operation switch
        {
            OnlineOperation.CreateMatch => $"Unable to create online match. {detail}",
            OnlineOperation.JoinMatch => $"Unable to join online match. {detail}",
            OnlineOperation.LeaveMatch => $"Unable to leave online match. {detail}",
            OnlineOperation.ResyncMatch => $"Unable to resync online match. {detail}",
            OnlineOperation.SubmitMove => $"Unable to submit online move. {detail}",
            _ => $"Online operation failed. {detail}"
        };
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
                var clickCommand = new AsyncRelayCommand(
                    () => OnSquareClickedAsync(square),
                    exception => HandleOnlineCommandException(OnlineOperation.SubmitMove, exception));
                var squareViewModel = new BoardSquareViewModel(
                    square,
                    clickCommand,
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

    private static int GetBoardSquareIndex(Square square)
    {
        return ((7 - square.Rank) * 8) + square.File;
    }

    private enum OnlineOperation
    {
        CreateMatch,
        JoinMatch,
        LeaveMatch,
        ResyncMatch,
        SubmitMove
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
