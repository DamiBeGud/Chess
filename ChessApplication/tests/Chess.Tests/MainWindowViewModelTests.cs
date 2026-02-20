using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Media;
using Chess.AI;
using Chess.AppCore;
using Chess.Domain;
using Chess.Engine;
using Chess.Online;
using Chess.Persistence;
using Chess.UI.Assets;
using Chess.UI.ViewModels;
using Xunit;

namespace Chess.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Constructor_InitializesBoardStateAndStatus()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(64, viewModel.BoardSquares.Count);
        Assert.Equal("Status: In progress. Side to move: White.", viewModel.GameStatusText);
        Assert.Empty(viewModel.MoveHistoryEntries);
        Assert.True(viewModel.IsMoveHistoryEmpty);

        var e2 = FindSquare(viewModel, 4, 1);
        var e7 = FindSquare(viewModel, 4, 6);
        AssertSquareHasPieceAsset(e2, "white-pawn");
        AssertSquareHasPieceAsset(e7, "black-pawn");
        AssertNoLastMoveHighlight(viewModel);
    }

    [Fact]
    public void SelectingPiece_HighlightsLegalDestinations()
    {
        var viewModel = CreateViewModel();
        var e2 = FindSquare(viewModel, 4, 1);
        var e3 = FindSquare(viewModel, 4, 2);
        var e4 = FindSquare(viewModel, 4, 3);

        var e3BeforeSelection = e3.Background;
        var e4BeforeSelection = e4.Background;

        e2.ClickCommand.Execute(null);

        Assert.True(e3.IsLegalDestinationIndicatorVisible);
        Assert.True(e4.IsLegalDestinationIndicatorVisible);
        Assert.Equal(e3BeforeSelection, e3.Background);
        Assert.Equal(e4BeforeSelection, e4.Background);
        Assert.False(e2.IsLegalDestinationIndicatorVisible);
    }

    [Fact]
    public void LegalDestinationIndicator_UsesConfiguredCircleStyle()
    {
        var viewModel = CreateViewModel();
        var lightSquare = FindSquare(viewModel, 4, 2);
        var darkSquare = FindSquare(viewModel, 3, 2);

        Assert.InRange(lightSquare.LegalDestinationIndicatorDiameter, 12d, 48d);
        Assert.InRange(lightSquare.LegalDestinationIndicatorOpacity, 0.4d, 0.7d);
        Assert.Equal(lightSquare.LegalDestinationIndicatorBrush, darkSquare.LegalDestinationIndicatorBrush);
        Assert.NotEqual(lightSquare.Background, lightSquare.LegalDestinationIndicatorBrush);
        Assert.NotEqual(darkSquare.Background, darkSquare.LegalDestinationIndicatorBrush);
    }

    [Fact]
    public void ShowLegalMoveSuggestions_ToggleHidesAndRestoresLegalDestinationIndicators()
    {
        var viewModel = CreateViewModel();
        var e2 = FindSquare(viewModel, 4, 1);
        var e3 = FindSquare(viewModel, 4, 2);
        var e4 = FindSquare(viewModel, 4, 3);

        e2.ClickCommand.Execute(null);
        Assert.True(e3.IsLegalDestinationIndicatorVisible);
        Assert.True(e4.IsLegalDestinationIndicatorVisible);

        viewModel.ShowLegalMoveSuggestions = false;
        Assert.False(e3.IsLegalDestinationIndicatorVisible);
        Assert.False(e4.IsLegalDestinationIndicatorVisible);

        viewModel.ShowLegalMoveSuggestions = true;
        Assert.True(e3.IsLegalDestinationIndicatorVisible);
        Assert.True(e4.IsLegalDestinationIndicatorVisible);
    }

    [Fact]
    public void ClickingIllegalDestination_ShowsInvalidMoveFeedback()
    {
        var viewModel = CreateViewModel();
        var e2 = FindSquare(viewModel, 4, 1);
        var e5 = FindSquare(viewModel, 4, 4);

        e2.ClickCommand.Execute(null);
        e5.ClickCommand.Execute(null);

        Assert.Equal("Status: In progress. Side to move: White.", viewModel.GameStatusText);
        Assert.Equal("Invalid move target: e5. Legal destinations from e2: e3, e4.", viewModel.FeedbackText);
    }

    [Fact]
    public void HandleKeyboardInput_AfterInvalidMouseTarget_StartsArrowMovementFromSelectedPiece()
    {
        var viewModel = CreateViewModel();
        var e2 = FindSquare(viewModel, 4, 1);
        var e5 = FindSquare(viewModel, 4, 4);

        e2.ClickCommand.Execute(null);
        e5.ClickCommand.Execute(null);
        Assert.Equal("Invalid move target: e5. Legal destinations from e2: e3, e4.", viewModel.FeedbackText);

        Assert.True(viewModel.HandleKeyboardInput(Key.Up));
        Assert.Equal("Keyboard focus: e3.", viewModel.FocusedSquareText);
        Assert.True(viewModel.HandleKeyboardInput(Key.Up));
        Assert.Equal("Keyboard focus: e4.", viewModel.FocusedSquareText);
    }

    [Fact]
    public void ClickingEmptySquare_ShowsSelectionGuidance()
    {
        var viewModel = CreateViewModel();
        var e3 = FindSquare(viewModel, 4, 2);

        e3.ClickCommand.Execute(null);

        Assert.Equal("No piece at e3. Select one of your White pieces.", viewModel.FeedbackText);
    }

    [Fact]
    public void ClickingOpponentPieceOnWrongTurn_ShowsTurnFeedback()
    {
        var viewModel = CreateViewModel();
        var e7 = FindSquare(viewModel, 4, 6);

        e7.ClickCommand.Execute(null);

        Assert.Equal("Cannot select Black piece at e7. It is White to move.", viewModel.FeedbackText);
    }

    [Fact]
    public void ClickingPieceWithNoLegalMoves_ShowsNoLegalMovesFeedback()
    {
        var viewModel = CreateViewModel();
        var e1 = FindSquare(viewModel, 4, 0);

        e1.ClickCommand.Execute(null);

        Assert.Equal("Selected square e1 has no legal moves.", viewModel.FeedbackText);
    }

    [Fact]
    public void ClickingLegalDestination_AppliesMoveThroughSessionService()
    {
        var viewModel = CreateViewModel();
        var e2 = FindSquare(viewModel, 4, 1);
        var e4 = FindSquare(viewModel, 4, 3);

        e2.ClickCommand.Execute(null);
        e4.ClickCommand.Execute(null);

        Assert.Equal("Status: In progress. Side to move: Black.", viewModel.GameStatusText);
        Assert.Equal(string.Empty, viewModel.FeedbackText);
        Assert.Equal("Last action: White moved Pawn from e2 to e4.", viewModel.LastActionText);
        AssertSquareHasNoPieceAsset(FindSquare(viewModel, 4, 1));
        AssertSquareHasPieceAsset(FindSquare(viewModel, 4, 3), "white-pawn");
        AssertLastMoveHighlight(viewModel, "e2", "e4");
        var entry = Assert.Single(viewModel.MoveHistoryEntries);
        AssertMoveHistoryEntry(
            entry,
            expectedPrefix: "1.",
            expectedPieceType: PieceType.Pawn,
            expectedSide: PieceColor.White,
            expectedNotation: "e2-e4",
            expectedIconStem: "white-pawn",
            expectedSideColorHex: "#F7F3EA");
        Assert.False(viewModel.IsMoveHistoryEmpty);
    }

    [Fact]
    public void ConsecutiveLegalMoves_UpdateMoveHistoryWithMoveNumbers()
    {
        var viewModel = CreateViewModel();
        var e2 = FindSquare(viewModel, 4, 1);
        var e4 = FindSquare(viewModel, 4, 3);
        var e7 = FindSquare(viewModel, 4, 6);
        var e5 = FindSquare(viewModel, 4, 4);

        e2.ClickCommand.Execute(null);
        e4.ClickCommand.Execute(null);
        e7.ClickCommand.Execute(null);
        e5.ClickCommand.Execute(null);

        Assert.Equal(
            new[] { "1. e2-e4", "1. e7-e5" },
            viewModel.MoveHistoryEntries.Select(entry => entry.ToString()));
        Assert.Equal(PieceColor.White, viewModel.MoveHistoryEntries[0].Side);
        Assert.Equal(PieceColor.Black, viewModel.MoveHistoryEntries[1].Side);
        AssertLastMoveHighlight(viewModel, "e7", "e5");
        Assert.False(viewModel.IsMoveHistoryEmpty);
    }

    [Fact]
    public void Constructor_WithPreloadedMoveHistory_FormatsSpecialNotation()
    {
        var preloadedState = CreateState(
            GameStatus.InProgress,
            PieceColor.Black,
            new List<Move>
            {
                new(
                    From: new Square(4, 0),
                    To: new Square(6, 0),
                    MovedPiece: new Piece(PieceType.King, PieceColor.White, HasMoved: true),
                    IsCastling: true),
                new(
                    From: new Square(4, 7),
                    To: new Square(2, 7),
                    MovedPiece: new Piece(PieceType.King, PieceColor.Black, HasMoved: true),
                    IsCastling: true),
                new(
                    From: new Square(4, 3),
                    To: new Square(3, 4),
                    MovedPiece: new Piece(PieceType.Pawn, PieceColor.White, HasMoved: true),
                    CapturedPiece: new Piece(PieceType.Pawn, PieceColor.Black, HasMoved: true)),
                new(
                    From: new Square(1, 1),
                    To: new Square(0, 0),
                    MovedPiece: new Piece(PieceType.Pawn, PieceColor.Black, HasMoved: true),
                    CapturedPiece: new Piece(PieceType.Rook, PieceColor.White, HasMoved: true),
                    PromotionPieceType: PieceType.Queen),
                new(
                    From: new Square(4, 4),
                    To: new Square(3, 5),
                    MovedPiece: new Piece(PieceType.Pawn, PieceColor.White, HasMoved: true),
                    CapturedPiece: new Piece(PieceType.Pawn, PieceColor.Black, HasMoved: true),
                    IsEnPassant: true)
            },
            new PiecePlacement(new Square(6, 0), new Piece(PieceType.King, PieceColor.White, HasMoved: true)),
            new PiecePlacement(new Square(2, 7), new Piece(PieceType.King, PieceColor.Black, HasMoved: true)));

        var session = new StubGameSessionService(preloadedState);
        var viewModel = new MainWindowViewModel(session, CreateTestAssetResolver());

        Assert.Equal(
            new[] { "1. O-O", "1. O-O-O", "2. e4xd5", "2. b2xa1=Q", "3. e5xd6 e.p." },
            viewModel.MoveHistoryEntries.Select(entry => entry.ToString()));
        Assert.Equal(PieceType.King, viewModel.MoveHistoryEntries[0].MovedPieceType);
        Assert.Equal(PieceType.King, viewModel.MoveHistoryEntries[1].MovedPieceType);
        Assert.Equal(PieceType.Pawn, viewModel.MoveHistoryEntries[2].MovedPieceType);
        Assert.Equal(PieceType.Pawn, viewModel.MoveHistoryEntries[3].MovedPieceType);
        Assert.Equal(PieceType.Pawn, viewModel.MoveHistoryEntries[4].MovedPieceType);
        AssertLastMoveHighlight(viewModel, "e5", "d6");
        Assert.False(viewModel.IsMoveHistoryEmpty);
    }

    [Fact]
    public void Constructor_WhenMoveHistoryAssetUnavailable_UsesDeterministicAssetFallback()
    {
        var preloadedState = CreateState(
            GameStatus.InProgress,
            PieceColor.Black,
            new List<Move>
            {
                new(
                    From: new Square(1, 0),
                    To: new Square(2, 2),
                    MovedPiece: new Piece(PieceType.Knight, PieceColor.White, HasMoved: true)),
                new(
                    From: new Square(6, 7),
                    To: new Square(5, 5),
                    MovedPiece: new Piece(PieceType.Knight, PieceColor.Black, HasMoved: true))
            },
            new PiecePlacement(new Square(4, 0), new Piece(PieceType.King, PieceColor.White)),
            new PiecePlacement(new Square(4, 7), new Piece(PieceType.King, PieceColor.Black)));

        var session = new StubGameSessionService(preloadedState);
        var viewModel = new MainWindowViewModel(session, new PieceAssetResolver(_ => null));

        var whiteMove = viewModel.MoveHistoryEntries[0];
        var blackMove = viewModel.MoveHistoryEntries[1];
        Assert.True(whiteMove.HasPieceIconImage);
        Assert.True(blackMove.HasPieceIconImage);
        Assert.True(whiteMove.IsUsingDeterministicFallbackIcon);
        Assert.True(blackMove.IsUsingDeterministicFallbackIcon);
        Assert.EndsWith("/white-knight.png", whiteMove.PieceIconAssetUri, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("/black-knight.png", blackMove.PieceIconAssetUri, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("#F7F3EA", whiteMove.SideColorHex);
        Assert.Equal("#2B2B2B", blackMove.SideColorHex);
    }

    [Fact]
    public void Constructor_WithFinishedGameState_ShowsResultStatusImmediately()
    {
        var finishedState = CreateState(
            GameStatus.WhiteWin,
            PieceColor.Black,
            new PiecePlacement(new Square(4, 0), new Piece(PieceType.King, PieceColor.White)),
            new PiecePlacement(new Square(4, 7), new Piece(PieceType.King, PieceColor.Black)));

        var session = new StubGameSessionService(finishedState);
        var viewModel = new MainWindowViewModel(session, CreateTestAssetResolver());

        Assert.Equal("Status: White wins.", viewModel.GameStatusText);
    }

    [Fact]
    public void ClickingSquare_WhenGameFinished_ShowsFeedbackAndDoesNotQueryLegalMoves()
    {
        var finishedState = CreateState(
            GameStatus.Draw,
            PieceColor.White,
            new PiecePlacement(new Square(4, 0), new Piece(PieceType.King, PieceColor.White)),
            new PiecePlacement(new Square(4, 7), new Piece(PieceType.King, PieceColor.Black)));

        var session = new StubGameSessionService(finishedState);
        var viewModel = new MainWindowViewModel(session, CreateTestAssetResolver());

        var e1 = FindSquare(viewModel, 4, 0);
        e1.ClickCommand.Execute(null);

        Assert.Equal("Status: Draw.", viewModel.GameStatusText);
        Assert.Equal("Game is finished (Draw). Start a new game to continue.", viewModel.FeedbackText);
        Assert.Equal(0, session.GetLegalMovesFromCallCount);
    }

    [Fact]
    public void HandleKeyboardInput_ArrowAndEnter_AppliesMove()
    {
        var viewModel = CreateViewModel();

        Assert.Equal("Keyboard focus: e2.", viewModel.FocusedSquareText);

        Assert.True(viewModel.HandleKeyboardInput(Key.Enter));
        Assert.True(viewModel.HandleKeyboardInput(Key.Up));
        Assert.True(viewModel.HandleKeyboardInput(Key.Up));
        Assert.True(viewModel.HandleKeyboardInput(Key.Enter));

        Assert.Equal("Status: In progress. Side to move: Black.", viewModel.GameStatusText);
        Assert.Equal(string.Empty, viewModel.FeedbackText);
        Assert.Equal("Last action: White moved Pawn from e2 to e4.", viewModel.LastActionText);
        Assert.Equal("Keyboard focus: e4.", viewModel.FocusedSquareText);
    }

    [Fact]
    public void HandleKeyboardInput_AtBoardEdge_ClampsFocusWithinBoard()
    {
        var viewModel = CreateViewModel();

        for (var step = 0; step < 10; step++)
        {
            Assert.True(viewModel.HandleKeyboardInput(Key.A));
            Assert.True(viewModel.HandleKeyboardInput(Key.S));
        }

        Assert.Equal("Keyboard focus: a1.", viewModel.FocusedSquareText);
        Assert.True(viewModel.HandleKeyboardInput(Key.Left));
        Assert.True(viewModel.HandleKeyboardInput(Key.Down));
        Assert.Equal("Keyboard focus: a1.", viewModel.FocusedSquareText);
    }

    [Fact]
    public void HandleKeyboardInput_WithUnmappedKey_ReturnsFalse()
    {
        var viewModel = CreateViewModel();
        var focusedSquareTextBeforeInput = viewModel.FocusedSquareText;

        var handled = viewModel.HandleKeyboardInput(Key.F1);

        Assert.False(handled);
        Assert.Equal(focusedSquareTextBeforeInput, viewModel.FocusedSquareText);
    }

    [Fact]
    public void HandleKeyboardInput_WasdAndSpace_AppliesMoveFromFocusedSquare()
    {
        var viewModel = CreateViewModel();

        Assert.True(viewModel.HandleKeyboardInput(Key.D));
        Assert.Equal("Keyboard focus: f2.", viewModel.FocusedSquareText);

        Assert.True(viewModel.HandleKeyboardInput(Key.Space));
        Assert.True(viewModel.HandleKeyboardInput(Key.W));
        Assert.Equal("Keyboard focus: f3.", viewModel.FocusedSquareText);
        Assert.True(viewModel.HandleKeyboardInput(Key.Space));

        Assert.Equal("Status: In progress. Side to move: Black.", viewModel.GameStatusText);
        Assert.Equal(string.Empty, viewModel.FeedbackText);
        Assert.Equal("Last action: White moved Pawn from f2 to f3.", viewModel.LastActionText);
        AssertSquareHasNoPieceAsset(FindSquare(viewModel, 5, 1));
        AssertSquareHasPieceAsset(FindSquare(viewModel, 5, 2), "white-pawn");
        AssertLastMoveHighlight(viewModel, "f2", "f3");
    }

    [Fact]
    public void HandleKeyboardInput_SelectionAndFocusStaySynchronized_AcrossReselectInvalidAndEscape()
    {
        var viewModel = CreateViewModel();
        var g3 = FindSquare(viewModel, 6, 2);
        var h3 = FindSquare(viewModel, 7, 2);
        Assert.False(g3.IsLegalDestinationIndicatorVisible);
        Assert.False(h3.IsLegalDestinationIndicatorVisible);

        Assert.True(viewModel.HandleKeyboardInput(Key.Enter));
        Assert.True(viewModel.HandleKeyboardInput(Key.Right));
        Assert.True(viewModel.HandleKeyboardInput(Key.Enter));
        Assert.True(viewModel.HandleKeyboardInput(Key.Right));
        Assert.True(viewModel.HandleKeyboardInput(Key.Enter));
        Assert.True(viewModel.HandleKeyboardInput(Key.Right));
        Assert.True(viewModel.HandleKeyboardInput(Key.Enter));

        Assert.True(viewModel.HandleKeyboardInput(Key.Left));
        Assert.True(viewModel.HandleKeyboardInput(Key.Up));
        Assert.Equal("Keyboard focus: g3.", viewModel.FocusedSquareText);
        Assert.True(viewModel.HandleKeyboardInput(Key.Enter));

        Assert.Equal("Invalid move target: g3. Legal destinations from h2: h3, h4.", viewModel.FeedbackText);
        Assert.False(g3.IsLegalDestinationIndicatorVisible);
        Assert.True(h3.IsLegalDestinationIndicatorVisible);

        Assert.True(viewModel.HandleKeyboardInput(Key.Escape));
        Assert.Equal("Selection cleared.", viewModel.FeedbackText);
        Assert.Equal("Keyboard focus: g3.", viewModel.FocusedSquareText);
        Assert.False(h3.IsLegalDestinationIndicatorVisible);
    }

    [Fact]
    public void HandleKeyboardInput_Escape_ClearsSelection()
    {
        var viewModel = CreateViewModel();
        var e3 = FindSquare(viewModel, 4, 2);
        Assert.False(e3.IsLegalDestinationIndicatorVisible);

        Assert.True(viewModel.HandleKeyboardInput(Key.Enter));
        Assert.True(e3.IsLegalDestinationIndicatorVisible);
        Assert.True(viewModel.HandleKeyboardInput(Key.Escape));

        Assert.False(e3.IsLegalDestinationIndicatorVisible);
        Assert.Equal("Selection cleared.", viewModel.FeedbackText);
    }

    [Fact]
    public async Task SaveAndLoadCommands_RoundTripStateAndUpdateFeedback()
    {
        var viewModel = CreateViewModel();
        var savePath = Path.Combine(Path.GetTempPath(), $"chess-viewmodel-save-{Path.GetRandomFileName()}.json");
        viewModel.PersistenceFilePath = savePath;

        try
        {
            FindSquare(viewModel, 4, 1).ClickCommand.Execute(null);
            FindSquare(viewModel, 4, 3).ClickCommand.Execute(null);

            await viewModel.SaveGameAsync();
            Assert.True(File.Exists(savePath));
            Assert.Equal($"Last action: Saved game to {savePath}.", viewModel.LastActionText);
            Assert.Equal(string.Empty, viewModel.FeedbackText);

            viewModel.StartNewGame();
            Assert.Equal("Status: In progress. Side to move: White.", viewModel.GameStatusText);

            await viewModel.LoadGameAsync();
            Assert.Equal("Status: In progress. Side to move: Black.", viewModel.GameStatusText);
            Assert.Equal($"Last action: Loaded game from {savePath}.", viewModel.LastActionText);
            Assert.Equal(string.Empty, viewModel.FeedbackText);
            AssertSquareHasNoPieceAsset(FindSquare(viewModel, 4, 1));
            AssertSquareHasPieceAsset(FindSquare(viewModel, 4, 3), "white-pawn");
            AssertLastMoveHighlight(viewModel, "e2", "e4");
        }
        finally
        {
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }
        }
    }

    [Fact]
    public async Task LoadGameAsync_WithCorruptedFile_ShowsGracefulFeedback()
    {
        var viewModel = CreateViewModel();
        var savePath = Path.Combine(Path.GetTempPath(), $"chess-viewmodel-save-{Path.GetRandomFileName()}.json");
        viewModel.PersistenceFilePath = savePath;

        try
        {
            await File.WriteAllTextAsync(savePath, "{ invalid json");
            await viewModel.LoadGameAsync();

            Assert.StartsWith("Unable to load game: ", viewModel.FeedbackText, StringComparison.Ordinal);
            Assert.Equal("Status: In progress. Side to move: White.", viewModel.GameStatusText);
        }
        finally
        {
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }
        }
    }

    [Fact]
    public async Task PlayVsAi_AfterHumanMove_AiRespondsAutomatically()
    {
        var viewModel = CreateAiEnabledViewModel();
        viewModel.IsPlayVsAiEnabled = true;
        viewModel.AiControlledColor = PieceColor.Black;
        viewModel.AiSearchDepth = 1;

        FindSquare(viewModel, 4, 1).ClickCommand.Execute(null);
        FindSquare(viewModel, 4, 3).ClickCommand.Execute(null);

        await WaitForConditionAsync(() => viewModel.MoveHistoryEntries.Count >= 2);

        Assert.Equal("Status: In progress. Side to move: White.", viewModel.GameStatusText);
        Assert.Equal(2, viewModel.MoveHistoryEntries.Count);
        Assert.StartsWith("Last action: AI (Black) moved ", viewModel.LastActionText, StringComparison.Ordinal);
        Assert.Equal(string.Empty, viewModel.FeedbackText);
        var highlightedSquares = GetLastMoveHighlightCoordinates(viewModel);
        Assert.Equal(2, highlightedSquares.Count);
        Assert.False(highlightedSquares.SequenceEqual(new[] { "e2", "e4" }, StringComparer.Ordinal));
    }

    [Fact]
    public async Task PlayVsAi_WhenAiIsWhite_MakesOpeningMove()
    {
        var viewModel = CreateAiEnabledViewModel();
        viewModel.AiControlledColor = PieceColor.White;
        viewModel.AiSearchDepth = 1;
        viewModel.IsPlayVsAiEnabled = true;

        await WaitForConditionAsync(() => viewModel.MoveHistoryEntries.Count > 0);

        Assert.Equal("Status: In progress. Side to move: Black.", viewModel.GameStatusText);
        Assert.Single(viewModel.MoveHistoryEntries);
        Assert.StartsWith("Last action: AI (White) moved ", viewModel.LastActionText, StringComparison.Ordinal);
        Assert.Equal(2, GetLastMoveHighlightCoordinates(viewModel).Count);
    }

    [Fact]
    public void StartNewGame_ClearsLastMoveHighlight()
    {
        var viewModel = CreateViewModel();
        FindSquare(viewModel, 4, 1).ClickCommand.Execute(null);
        FindSquare(viewModel, 4, 3).ClickCommand.Execute(null);
        AssertLastMoveHighlight(viewModel, "e2", "e4");

        viewModel.StartNewGame();

        AssertNoLastMoveHighlight(viewModel);
    }

    [Fact]
    public void PlayVsAi_WhenUnavailable_DoesNotEnableAndShowsFeedback()
    {
        var viewModel = CreateViewModel();

        viewModel.IsPlayVsAiEnabled = true;

        Assert.False(viewModel.IsAiAvailable);
        Assert.False(viewModel.IsPlayVsAiEnabled);
        Assert.Equal("Play vs AI is unavailable in this configuration.", viewModel.FeedbackText);
    }

    [Fact]
    public async Task PlayVsAi_DisablingMode_CancelsInFlightAiTurn()
    {
        var session = new GameSessionService(new ChessGameEngine(), new JsonGameStateStore());
        var slowAiTurnService = new SlowCancellableAiTurnService();
        var viewModel = new MainWindowViewModel(session, CreateTestAssetResolver(), slowAiTurnService);
        viewModel.AiControlledColor = PieceColor.White;
        viewModel.IsPlayVsAiEnabled = true;

        await slowAiTurnService.Started;
        viewModel.IsPlayVsAiEnabled = false;

        await WaitForConditionAsync(() => !viewModel.IsAiThinking);

        Assert.True(slowAiTurnService.CancellationObserved);
        Assert.False(viewModel.IsPlayVsAiEnabled);
    }

    [Fact]
    public async Task PlayVsAi_Requeues_WhenAiTurnReturnsNoMoveAndStillEligible()
    {
        var session = new GameSessionService(new ChessGameEngine(), new JsonGameStateStore());
        var requeueProbeService = new RequeueProbeAiTurnService(maxRequests: 2);
        var viewModel = new MainWindowViewModel(session, CreateTestAssetResolver(), requeueProbeService);
        viewModel.AiControlledColor = PieceColor.White;
        viewModel.IsPlayVsAiEnabled = true;

        await WaitForConditionAsync(() => requeueProbeService.TryPlayTurnCallCount >= 2);

        Assert.Equal(2, requeueProbeService.TryPlayTurnCallCount);
    }

    [Fact]
    public async Task PlayVsAi_WhenAiMoveThrows_ShowsFailureFeedbackAndClearsThinkingState()
    {
        var session = new GameSessionService(new ChessGameEngine(), new JsonGameStateStore());
        var viewModel = new MainWindowViewModel(session, CreateTestAssetResolver(), new ThrowingAiTurnService());
        viewModel.AiControlledColor = PieceColor.White;
        viewModel.IsPlayVsAiEnabled = true;

        await WaitForConditionAsync(() =>
            viewModel.FeedbackText.StartsWith("AI move failed: simulated AI failure", StringComparison.Ordinal));

        Assert.False(viewModel.IsAiThinking);
    }

    [Fact]
    public async Task CreateOnlineMatchCommand_WhenTransportThrows_ShowsDeterministicFeedback()
    {
        var onlineService = new FakeOnlineMatchSessionService
        {
            CreateMatchAsyncHandler = _ => Task.FromException<OnlineOperationResult<OnlineCreatedMatch>>(new HttpRequestException("simulated transport failure"))
        };
        var viewModel = CreateOnlineViewModel(onlineService);

        Assert.True(viewModel.CreateOnlineMatchCommand.CanExecute(null));
        viewModel.CreateOnlineMatchCommand.Execute(null);

        await WaitForConditionAsync(() => !viewModel.IsOnlineOperationInProgress);

        Assert.Equal(
            "Unable to create online match. Network error. Check your connection and try again.",
            viewModel.FeedbackText);
        Assert.Equal(1, onlineService.CreateMatchCallCount);
    }

    [Fact]
    public async Task CreateOnlineMatchCommand_IsNonReentrantAndDisablesOnlineCommandsWhileBusy()
    {
        var createCompletion = new TaskCompletionSource<OnlineOperationResult<OnlineCreatedMatch>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var onlineService = new FakeOnlineMatchSessionService
        {
            CreateMatchAsyncHandler = _ => createCompletion.Task
        };
        var viewModel = CreateOnlineViewModel(onlineService);

        viewModel.CreateOnlineMatchCommand.Execute(null);
        await WaitForConditionAsync(() => viewModel.IsOnlineOperationInProgress);

        Assert.False(viewModel.CreateOnlineMatchCommand.CanExecute(null));
        Assert.False(viewModel.JoinOnlineMatchCommand.CanExecute(null));
        Assert.False(viewModel.LeaveOnlineMatchCommand.CanExecute(null));
        Assert.False(viewModel.ResyncOnlineMatchCommand.CanExecute(null));

        viewModel.CreateOnlineMatchCommand.Execute(null);
        Assert.Equal(1, onlineService.CreateMatchCallCount);

        createCompletion.SetResult(OnlineOperationResult<OnlineCreatedMatch>.Failure(new OnlineUserError("busy_test", "simulated failure", OnlineUserAction.Retry)));
        await WaitForConditionAsync(() => !viewModel.IsOnlineOperationInProgress);

        Assert.True(viewModel.CreateOnlineMatchCommand.CanExecute(null));
        Assert.True(viewModel.JoinOnlineMatchCommand.CanExecute(null));
        Assert.False(viewModel.LeaveOnlineMatchCommand.CanExecute(null));
        Assert.False(viewModel.ResyncOnlineMatchCommand.CanExecute(null));
    }

    [Fact]
    public async Task OnlineSquareClick_SubmitMoveException_ShowsDeterministicFeedback()
    {
        var onlineState = CreateState(
            GameStatus.InProgress,
            PieceColor.White,
            new PiecePlacement(new Square(4, 0), new Piece(PieceType.King, PieceColor.White)),
            new PiecePlacement(new Square(4, 7), new Piece(PieceType.King, PieceColor.Black)),
            new PiecePlacement(new Square(4, 1), new Piece(PieceType.Pawn, PieceColor.White)));
        var onlineService = new FakeOnlineMatchSessionService
        {
            IsInMatch = true,
            IsConnected = true,
            MatchId = "match-1",
            JoinCode = "ABC123",
            Seat = PieceColor.White,
            CurrentGameState = onlineState,
            SubmitMoveAsyncHandler = (_, _, _, _) => Task.FromException<OnlineOperationResult<OnlineMatchSnapshot>>(new TimeoutException("simulated timeout"))
        };
        var viewModel = CreateOnlineViewModel(onlineService);
        var e2 = FindSquare(viewModel, 4, 1);
        var e4 = FindSquare(viewModel, 4, 3);

        e2.ClickCommand.Execute(null);
        e4.ClickCommand.Execute(null);

        await WaitForConditionAsync(() => !viewModel.IsOnlineOperationInProgress);

        Assert.Equal(
            "Unable to submit online move. Network error. Check your connection and try again.",
            viewModel.FeedbackText);
        Assert.Equal(1, onlineService.SubmitMoveCallCount);
    }

    private static MainWindowViewModel CreateViewModel()
    {
        var session = new GameSessionService(new ChessGameEngine(), new JsonGameStateStore());
        return new MainWindowViewModel(session, CreateTestAssetResolver());
    }

    private static MainWindowViewModel CreateOnlineViewModel(FakeOnlineMatchSessionService onlineMatchSessionService)
    {
        var session = new GameSessionService(new ChessGameEngine(), new JsonGameStateStore());
        return new MainWindowViewModel(session, CreateTestAssetResolver(), new PassiveAiTurnService(), onlineMatchSessionService);
    }

    private static MainWindowViewModel CreateAiEnabledViewModel()
    {
        var engine = new ChessGameEngine();
        var session = new GameSessionService(engine, new JsonGameStateStore());
        var evaluator = new MaterialMobilityPositionEvaluator(engine);
        var selector = new NegamaxAiMoveSelector(engine, evaluator);
        var aiTurnService = new AiTurnService(session, selector);
        return new MainWindowViewModel(session, CreateTestAssetResolver(), aiTurnService);
    }

    private static IPieceAssetResolver CreateTestAssetResolver()
    {
        return new PieceAssetResolver(_ => CreateTestImage());
    }

    private static IImage CreateTestImage()
    {
        return new TestImage();
    }

    private static GameState CreateState(GameStatus status, PieceColor sideToMove, params PiecePlacement[] pieces)
    {
        return new GameState(
            Pieces: pieces,
            SideToMove: sideToMove,
            CastlingRights: CastlingRights.None,
            EnPassantTarget: null,
            HalfmoveClock: 0,
            FullmoveNumber: 1,
            Status: status,
            MoveHistory: [],
            PositionHistory: []);
    }

    private static GameState CreateState(
        GameStatus status,
        PieceColor sideToMove,
        IReadOnlyList<Move> moveHistory,
        params PiecePlacement[] pieces)
    {
        return new GameState(
            Pieces: pieces,
            SideToMove: sideToMove,
            CastlingRights: CastlingRights.None,
            EnPassantTarget: null,
            HalfmoveClock: 0,
            FullmoveNumber: 1,
            Status: status,
            MoveHistory: moveHistory,
            PositionHistory: []);
    }

    private static BoardSquareViewModel FindSquare(MainWindowViewModel viewModel, int file, int rank)
    {
        return viewModel.BoardSquares.Single(square => square.Square == new Square(file, rank));
    }

    private static void AssertSquareHasNoPieceAsset(BoardSquareViewModel squareViewModel)
    {
        Assert.Null(squareViewModel.PieceImage);
        Assert.Equal(string.Empty, squareViewModel.PieceAssetUri);
        Assert.False(squareViewModel.IsUsingFallbackAsset);
    }

    private static void AssertSquareHasPieceAsset(BoardSquareViewModel squareViewModel, string expectedAssetStem)
    {
        Assert.NotNull(squareViewModel.PieceImage);
        Assert.Contains($"/{expectedAssetStem}.", squareViewModel.PieceAssetUri, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertLastMoveHighlight(MainWindowViewModel viewModel, params string[] expectedCoordinates)
    {
        var expected = expectedCoordinates
            .OrderBy(coordinate => coordinate, StringComparer.Ordinal)
            .ToArray();
        var actual = GetLastMoveHighlightCoordinates(viewModel);
        Assert.Equal(expected, actual);
    }

    private static void AssertNoLastMoveHighlight(MainWindowViewModel viewModel)
    {
        Assert.Empty(GetLastMoveHighlightCoordinates(viewModel));
    }

    private static IReadOnlyList<string> GetLastMoveHighlightCoordinates(MainWindowViewModel viewModel)
    {
        return viewModel.BoardSquares
            .Where(square => square.IsLastMoveHighlighted)
            .Select(square => square.CoordinateLabel)
            .OrderBy(coordinate => coordinate, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AssertMoveHistoryEntry(
        MoveHistoryEntryViewModel entry,
        string expectedPrefix,
        PieceType expectedPieceType,
        PieceColor expectedSide,
        string expectedNotation,
        string expectedIconStem,
        string expectedSideColorHex)
    {
        Assert.Equal(expectedPrefix, entry.MovePrefix);
        Assert.Equal(expectedPieceType, entry.MovedPieceType);
        Assert.Equal(expectedSide, entry.Side);
        Assert.Equal(expectedNotation, entry.Notation);
        Assert.Equal(expectedSideColorHex, entry.SideColorHex);
        Assert.True(entry.HasPieceIconImage);
        Assert.False(entry.IsUsingDeterministicFallbackIcon);
        Assert.Contains($"/{expectedIconStem}.", entry.PieceIconAssetUri, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, int timeoutMilliseconds = 3000)
    {
        var startedAt = DateTime.UtcNow;
        while (!condition())
        {
            if ((DateTime.UtcNow - startedAt).TotalMilliseconds > timeoutMilliseconds)
            {
                throw new TimeoutException("Timed out waiting for expected condition.");
            }

            await Task.Delay(20);
        }
    }

    private sealed class StubGameSessionService : IGameSessionService
    {
        private GameState _currentGameState;

        public StubGameSessionService(GameState initialState)
        {
            _currentGameState = initialState;
        }

        public int GetLegalMovesFromCallCount { get; private set; }

        public GameState CurrentGameState => _currentGameState;

        public GameState StartNewGame()
        {
            return _currentGameState;
        }

        public IReadOnlyList<Move> GetLegalMovesFrom(Square fromSquare)
        {
            GetLegalMovesFromCallCount++;
            return [];
        }

        public bool TryMakeMove(Square fromSquare, Square toSquare, PieceType? promotionPieceType = null)
        {
            return false;
        }

        public Task SaveAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<GameState> LoadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_currentGameState);
        }
    }

    private sealed class SlowCancellableAiTurnService : IAiTurnService
    {
        private readonly TaskCompletionSource<bool> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _started.Task;

        public bool CancellationObserved { get; private set; }

        public bool CanRequestMove(PieceColor aiColor)
        {
            return true;
        }

        public async Task<Move?> TryPlayTurnAsync(
            PieceColor aiColor,
            int searchDepth,
            CancellationToken cancellationToken = default)
        {
            _started.TrySetResult(true);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                return null;
            }
            catch (OperationCanceledException)
            {
                CancellationObserved = true;
                throw;
            }
        }
    }

    private sealed class RequeueProbeAiTurnService : IAiTurnService
    {
        private readonly int _maxRequests;
        private int _tryPlayTurnCallCount;

        public RequeueProbeAiTurnService(int maxRequests)
        {
            _maxRequests = maxRequests;
        }

        public int TryPlayTurnCallCount => Volatile.Read(ref _tryPlayTurnCallCount);

        public bool CanRequestMove(PieceColor aiColor)
        {
            return TryPlayTurnCallCount < _maxRequests;
        }

        public async Task<Move?> TryPlayTurnAsync(
            PieceColor aiColor,
            int searchDepth,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _tryPlayTurnCallCount);
            await Task.Delay(30, cancellationToken);
            return null;
        }
    }

    private sealed class ThrowingAiTurnService : IAiTurnService
    {
        public bool CanRequestMove(PieceColor aiColor)
        {
            return true;
        }

        public Task<Move?> TryPlayTurnAsync(
            PieceColor aiColor,
            int searchDepth,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("simulated AI failure");
        }
    }

    private sealed class PassiveAiTurnService : IAiTurnService
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

    private sealed class FakeOnlineMatchSessionService : IOnlineMatchSessionService
    {
        private static readonly OnlineUserError DefaultFailure = new("test_error", "Not configured.", OnlineUserAction.Retry);

        public event EventHandler? SessionStateChanged;
        public event EventHandler<OnlineUserError>? SessionError;

        public bool IsInMatch { get; set; }
        public bool IsConnected { get; set; }
        public string? MatchId { get; set; }
        public string? JoinCode { get; set; }
        public PieceColor? Seat { get; set; }
        public OnlineMatchSnapshot? CurrentSnapshot { get; set; }
        public GameState? CurrentGameState { get; set; }
        public long LastSequence { get; set; }

        public int CreateMatchCallCount { get; private set; }
        public int JoinMatchCallCount { get; private set; }
        public int SubmitMoveCallCount { get; private set; }
        public int RequestResyncCallCount { get; private set; }
        public int LeaveMatchCallCount { get; private set; }

        public Func<CancellationToken, Task<OnlineOperationResult<OnlineCreatedMatch>>>? CreateMatchAsyncHandler { get; set; }
        public Func<string, CancellationToken, Task<OnlineOperationResult<OnlineJoinedMatch>>>? JoinMatchAsyncHandler { get; set; }
        public Func<Square, Square, PieceType?, CancellationToken, Task<OnlineOperationResult<OnlineMatchSnapshot>>>? SubmitMoveAsyncHandler { get; set; }
        public Func<CancellationToken, Task<OnlineOperationResult<OnlineMatchSnapshot>>>? RequestResyncAsyncHandler { get; set; }
        public Func<CancellationToken, Task>? LeaveMatchAsyncHandler { get; set; }

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
            JoinMatchCallCount++;
            if (JoinMatchAsyncHandler is not null)
            {
                return JoinMatchAsyncHandler(joinCode, cancellationToken);
            }

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
            RequestResyncCallCount++;
            if (RequestResyncAsyncHandler is not null)
            {
                return RequestResyncAsyncHandler(cancellationToken);
            }

            return Task.FromResult(OnlineOperationResult<OnlineMatchSnapshot>.Failure(DefaultFailure));
        }

        public Task SuspendRealtimeAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task LeaveMatchAsync(CancellationToken cancellationToken = default)
        {
            LeaveMatchCallCount++;
            if (LeaveMatchAsyncHandler is not null)
            {
                return LeaveMatchAsyncHandler(cancellationToken);
            }

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public void RaiseSessionStateChanged()
        {
            SessionStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseSessionError(OnlineUserError error)
        {
            SessionError?.Invoke(this, error);
        }
    }
}
