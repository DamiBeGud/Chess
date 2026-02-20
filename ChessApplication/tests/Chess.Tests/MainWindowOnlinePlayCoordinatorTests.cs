using System;
using System.Threading;
using System.Threading.Tasks;
using Chess.Domain;
using Chess.Online;
using Chess.UI.Assets;
using Chess.UI.Services;
using Xunit;

namespace Chess.Tests;

public sealed class MainWindowOnlinePlayCoordinatorTests
{
    [Fact]
    public void Constructor_WithNullCommandDependency_Throws()
    {
        var selectionState = new MainWindowSelectionState(new Square(4, 1));
        var textFormatter = new MainWindowTextFormatter(new PieceAssetResolver(_ => null));
        var readModel = new StubOnlineMatchSessionService();

        Assert.Throws<ArgumentNullException>(
            () => new MainWindowOnlinePlayCoordinator(readModel, null!, selectionState, textFormatter));
    }

    [Fact]
    public async Task CreateOnlineMatchAsync_Success_UpdatesContextState()
    {
        var selectionState = new MainWindowSelectionState(new Square(4, 1));
        var textFormatter = new MainWindowTextFormatter(new PieceAssetResolver(_ => null));
        var onlineService = new StubOnlineMatchSessionService
        {
            CreateMatchAsyncHandler = _ => Task.FromResult(
                OnlineOperationResult<OnlineCreatedMatch>.Success(
                    new OnlineCreatedMatch("match-1", "ABC123", PieceColor.White)))
        };
        var coordinator = new MainWindowOnlinePlayCoordinator(onlineService, onlineService, selectionState, textFormatter);
        var context = new FakeOnlinePlayContext();

        await coordinator.CreateOnlineMatchAsync(context);

        Assert.Equal(1, onlineService.CreateMatchCallCount);
        Assert.Equal(1, context.RunOnlineOperationWithBusyStateCallCount);
        Assert.Equal(1, context.CancelInFlightAiTurnCallCount);
        Assert.Equal(1, context.DisablePlayVsAiCallCount);
        Assert.Equal("Last action: Created online match match-1. Share join code ABC123.", context.LastAction);
        Assert.Equal(string.Empty, context.Feedback);
        Assert.Equal(1, context.ClearSelectionCallCount);
        Assert.Equal(1, context.RefreshBoardCallCount);
        Assert.Equal(1, context.UpdateOnlineSessionTextCallCount);
        Assert.Equal(1, context.NotifyAiAvailabilityChangedCallCount);
    }

    [Fact]
    public async Task HandleSquareClickedAsync_WhenSeatIsUnknown_ShowsDeterministicFeedback()
    {
        var selectionState = new MainWindowSelectionState(new Square(4, 1));
        var textFormatter = new MainWindowTextFormatter(new PieceAssetResolver(_ => null));
        var onlineService = new StubOnlineMatchSessionService
        {
            IsInMatch = true,
            Seat = null
        };
        var coordinator = new MainWindowOnlinePlayCoordinator(onlineService, onlineService, selectionState, textFormatter);
        var context = new FakeOnlinePlayContext
        {
            OnlineGameState = CreateOnlineGameState(PieceColor.White)
        };

        await coordinator.HandleSquareClickedAsync(new Square(4, 1), context);

        Assert.Equal("Online seat is unknown. Try reconnecting.", context.Feedback);
        Assert.Equal(0, context.RunOnlineOperationWithBusyStateCallCount);
    }

    [Fact]
    public async Task HandleSquareClickedAsync_WhenOnlineStateMissing_ShowsDeterministicFeedbackAndDoesNotSubmit()
    {
        var selectionState = new MainWindowSelectionState(new Square(4, 1));
        var textFormatter = new MainWindowTextFormatter(new PieceAssetResolver(_ => null));
        var onlineService = new StubOnlineMatchSessionService
        {
            IsInMatch = true,
            Seat = PieceColor.White
        };
        var coordinator = new MainWindowOnlinePlayCoordinator(onlineService, onlineService, selectionState, textFormatter);
        var context = new FakeOnlinePlayContext
        {
            OnlineGameState = null
        };

        await coordinator.HandleSquareClickedAsync(new Square(4, 1), context);

        Assert.Equal("Online state is not ready yet. Try resync.", context.Feedback);
        Assert.Equal(0, onlineService.SubmitMoveCallCount);
        Assert.Equal(0, context.RunOnlineOperationWithBusyStateCallCount);
    }

    [Fact]
    public async Task HandleSquareClickedAsync_WithValidOnlineState_SubmitsMoveAndUpdatesContext()
    {
        var selectionState = new MainWindowSelectionState(new Square(4, 1));
        var textFormatter = new MainWindowTextFormatter(new PieceAssetResolver(_ => null));
        var onlineService = new StubOnlineMatchSessionService
        {
            IsInMatch = true,
            Seat = PieceColor.White,
            SubmitMoveAsyncHandler = (_, _, _, _) => Task.FromResult(
                OnlineOperationResult<OnlineMatchSnapshot>.Success(
                    new OnlineMatchSnapshot(
                        MatchId: "match-1",
                        SideToMove: OnlineMatchProtocolConstants.JoinerSeat,
                        MoveNumber: 2,
                        Board:
                        [
                            "rnbqkbnr",
                            "pppppppp",
                            "........",
                            "........",
                            "....P...",
                            "........",
                            "PPPP.PPP",
                            "RNBQKBNR"
                        ])))
        };
        var coordinator = new MainWindowOnlinePlayCoordinator(onlineService, onlineService, selectionState, textFormatter);
        var context = new FakeOnlinePlayContext
        {
            OnlineGameState = CreateOnlineGameState(PieceColor.White)
        };
        var e2 = new Square(4, 1);
        var e4 = new Square(4, 3);

        await coordinator.HandleSquareClickedAsync(e2, context);
        await coordinator.HandleSquareClickedAsync(e4, context);

        Assert.Equal(1, onlineService.SubmitMoveCallCount);
        Assert.Equal(1, context.RunOnlineOperationWithBusyStateCallCount);
        Assert.Equal("Last action: Submitted online move e2 to e4.", context.LastAction);
        Assert.Equal(string.Empty, context.Feedback);
        Assert.Equal(1, context.ClearSelectionCallCount);
        Assert.Equal(1, context.RefreshBoardCallCount);
    }

    private static GameState CreateOnlineGameState(PieceColor sideToMove)
    {
        return new GameState(
            Pieces:
            [
                new PiecePlacement(new Square(4, 0), new Piece(PieceType.King, PieceColor.White)),
                new PiecePlacement(new Square(4, 7), new Piece(PieceType.King, PieceColor.Black)),
                new PiecePlacement(new Square(4, 1), new Piece(PieceType.Pawn, PieceColor.White))
            ],
            SideToMove: sideToMove,
            CastlingRights: CastlingRights.None,
            EnPassantTarget: null,
            HalfmoveClock: 0,
            FullmoveNumber: 1,
            Status: GameStatus.InProgress,
            MoveHistory: []);
    }

    private sealed class FakeOnlinePlayContext : IMainWindowOnlinePlayContext
    {
        public bool IsOnlineOperationInProgress { get; set; }

        public GameState? OnlineGameState { get; set; }

        public string Feedback { get; private set; } = string.Empty;

        public string LastAction { get; private set; } = string.Empty;

        public Square? LastFocusedSquare { get; private set; }

        public int RunOnlineOperationWithBusyStateCallCount { get; private set; }

        public int CancelInFlightAiTurnCallCount { get; private set; }

        public int DisablePlayVsAiCallCount { get; private set; }

        public int UpdateOnlineSessionTextCallCount { get; private set; }

        public int NotifyAiAvailabilityChangedCallCount { get; private set; }

        public int NotifyOnlineMatchActiveChangedCallCount { get; private set; }

        public int ClearSelectionCallCount { get; private set; }

        public int UpdateSquareHighlightsCallCount { get; private set; }

        public int RefreshBoardCallCount { get; private set; }

        public int StartNewLocalGameCallCount { get; private set; }

        public int SetFocusedSquareToDefaultCallCount { get; private set; }

        public GameState? GetOnlineGameState()
        {
            return OnlineGameState;
        }

        public void SetFocusedSquare(Square square)
        {
            LastFocusedSquare = square;
        }

        public void SetFeedback(string feedback)
        {
            Feedback = feedback;
        }

        public void SetLastAction(string lastAction)
        {
            LastAction = lastAction;
        }

        public void ClearSelection()
        {
            ClearSelectionCallCount++;
        }

        public void UpdateSquareHighlights()
        {
            UpdateSquareHighlightsCallCount++;
        }

        public void RefreshBoardFromCurrentState()
        {
            RefreshBoardCallCount++;
        }

        public async Task RunOnlineOperationWithBusyStateAsync(Func<Task> operationAsync)
        {
            RunOnlineOperationWithBusyStateCallCount++;
            await operationAsync();
        }

        public void CancelInFlightAiTurn()
        {
            CancelInFlightAiTurnCallCount++;
        }

        public void DisablePlayVsAi()
        {
            DisablePlayVsAiCallCount++;
        }

        public void UpdateOnlineSessionText()
        {
            UpdateOnlineSessionTextCallCount++;
        }

        public void NotifyAiAvailabilityChanged()
        {
            NotifyAiAvailabilityChangedCallCount++;
        }

        public void NotifyOnlineMatchActiveChanged()
        {
            NotifyOnlineMatchActiveChangedCallCount++;
        }

        public void StartNewLocalGame()
        {
            StartNewLocalGameCallCount++;
        }

        public void SetFocusedSquareToDefault()
        {
            SetFocusedSquareToDefaultCallCount++;
        }
    }

    private sealed class StubOnlineMatchSessionService : IOnlineMatchSessionReadModel, IOnlineMatchSessionCommands
    {
        private static readonly OnlineUserError DefaultFailure = new("test_error", "Not configured.", OnlineUserAction.Retry);

        public event EventHandler? SessionStateChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<OnlineUserError>? SessionError
        {
            add { }
            remove { }
        }

        public bool IsInMatch { get; set; }

        public bool IsConnected { get; set; }

        public string? MatchId { get; set; }

        public string? JoinCode { get; set; }

        public PieceColor? Seat { get; set; }

        public OnlineMatchSnapshot? CurrentSnapshot { get; set; }

        public GameState? CurrentGameState { get; set; }

        public long LastSequence { get; set; }

        public int CreateMatchCallCount { get; private set; }
        public int SubmitMoveCallCount { get; private set; }

        public Func<CancellationToken, Task<OnlineOperationResult<OnlineCreatedMatch>>>? CreateMatchAsyncHandler { get; set; }
        public Func<Square, Square, PieceType?, CancellationToken, Task<OnlineOperationResult<OnlineMatchSnapshot>>>? SubmitMoveAsyncHandler { get; set; }

        public Task<OnlineOperationResult<OnlineCreatedMatch>> CreateMatchAsync(CancellationToken cancellationToken = default)
        {
            CreateMatchCallCount++;
            if (CreateMatchAsyncHandler is not null)
            {
                return CreateMatchAsyncHandler(cancellationToken);
            }

            return Task.FromResult(OnlineOperationResult<OnlineCreatedMatch>.Failure(DefaultFailure));
        }

        public Task<OnlineOperationResult<OnlineJoinedMatch>> JoinMatchAsync(string joinCode, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OnlineOperationResult<OnlineJoinedMatch>.Failure(DefaultFailure));
        }

        public Task<OnlineOperationResult<OnlineResumedMatch>> ResumeMatchAsync(
            string matchId,
            string playerToken,
            PieceColor seat,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OnlineOperationResult<OnlineResumedMatch>.Failure(DefaultFailure));
        }

        public Task<OnlineOperationResult<OnlineMatchSnapshot>> SubmitMoveAsync(
            Square fromSquare,
            Square toSquare,
            PieceType? promotionPieceType = null,
            CancellationToken cancellationToken = default)
        {
            SubmitMoveCallCount++;
            if (SubmitMoveAsyncHandler is not null)
            {
                return SubmitMoveAsyncHandler(fromSquare, toSquare, promotionPieceType, cancellationToken);
            }

            return Task.FromResult(OnlineOperationResult<OnlineMatchSnapshot>.Failure(DefaultFailure));
        }

        public Task<OnlineOperationResult<OnlineMatchSnapshot>> RecoverAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OnlineOperationResult<OnlineMatchSnapshot>.Failure(DefaultFailure));
        }

        public Task<OnlineOperationResult<OnlineMatchSnapshot>> RequestResyncAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OnlineOperationResult<OnlineMatchSnapshot>.Failure(DefaultFailure));
        }

        public Task SuspendRealtimeAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task LeaveMatchAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
