using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Chess.AppCore;
using Chess.Domain;
using Chess.Engine;
using Chess.Persistence;
using Chess.UI.ViewModels;
using Xunit;

namespace Chess.Tests;

public sealed class MainWindowUiIntegrationTests
{
    private const double LayoutTolerance = 6d;

    [AvaloniaFact]
    public void MainWindow_RendersBoardAndInitialStatus()
    {
        var window = CreateWindow(CreateSessionService());

        try
        {
            window.Show();

            var boardButtons = GetBoardSquareButtons(window);
            Assert.Equal(64, boardButtons.Count);
            Assert.Equal("Status: In progress. Side to move: White.", GetGameStatusText(window));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SelectingOwnPiece_HighlightsLegalDestinations()
    {
        var window = CreateWindow(CreateSessionService());

        try
        {
            window.Show();

            var e2 = FindSquareButton(window, "e2");
            var e3 = FindSquareButton(window, "e3");
            var e4 = FindSquareButton(window, "e4");

            var e3Before = e3.Background;
            var e4Before = e4.Background;

            Click(e2);

            Assert.True(GetSquareViewModel(e3).IsLegalDestinationIndicatorVisible);
            Assert.True(GetSquareViewModel(e4).IsLegalDestinationIndicatorVisible);
            Assert.Equal(e3Before, e3.Background);
            Assert.Equal(e4Before, e4.Background);
            Assert.False(GetSquareViewModel(e2).IsLegalDestinationIndicatorVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ShowLegalMoveSuggestionsCheckBox_TogglesIndicatorVisibility()
    {
        var window = CreateWindow(CreateSessionService());

        try
        {
            window.Show();

            var toggle = FindShowLegalMoveSuggestionsCheckBox(window);
            var e2 = FindSquareButton(window, "e2");
            var e3 = FindSquareButton(window, "e3");
            var e4 = FindSquareButton(window, "e4");

            Click(e2);
            Assert.True(GetSquareViewModel(e3).IsLegalDestinationIndicatorVisible);
            Assert.True(GetSquareViewModel(e4).IsLegalDestinationIndicatorVisible);

            toggle.IsChecked = false;
            Assert.False(GetSquareViewModel(e3).IsLegalDestinationIndicatorVisible);
            Assert.False(GetSquareViewModel(e4).IsLegalDestinationIndicatorVisible);

            toggle.IsChecked = true;
            Assert.True(GetSquareViewModel(e3).IsLegalDestinationIndicatorVisible);
            Assert.True(GetSquareViewModel(e4).IsLegalDestinationIndicatorVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void IllegalDestination_IsRejectedAndStatusRemainsUnchanged()
    {
        var window = CreateWindow(CreateSessionService());

        try
        {
            window.Show();

            Click(FindSquareButton(window, "e2"));
            Click(FindSquareButton(window, "e5"));

            Assert.Equal("Status: In progress. Side to move: White.", GetGameStatusText(window));
            Assert.Equal("Invalid move target: e5. Legal destinations from e2: e3, e4.", GetFeedbackText(window));
            Assert.Equal("Last action: Started a new game.", GetLastActionText(window));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void LegalMove_UpdatesBoardAndStatus()
    {
        var window = CreateWindow(CreateSessionService());

        try
        {
            window.Show();
            window.Focus();

            var e2 = FindSquareButton(window, "e2");
            var e4 = FindSquareButton(window, "e4");

            Click(e2);
            Click(e4);

            Assert.Equal("Status: In progress. Side to move: Black.", GetGameStatusText(window));
            Assert.Equal("Last action: White moved Pawn from e2 to e4.", GetLastActionText(window));
            Assert.Equal(string.Empty, GetFeedbackText(window));
            AssertSquareHasNoPieceAsset(e2);
            AssertSquareHasPieceAsset(e4, "white-pawn");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SidePanel_RendersStatusLastActionAndMoveHistory()
    {
        var window = CreateWindow(CreateSessionService());

        try
        {
            window.Show();

            var sidePanel = window.FindControl<Border>("SidePanelBorder");
            var emptyHistoryText = window.FindControl<TextBlock>("MoveHistoryEmptyTextBlock");

            Assert.NotNull(sidePanel);
            Assert.Equal("Status: In progress. Side to move: White.", GetGameStatusText(window));
            Assert.Equal("Last action: Started a new game.", GetLastActionText(window));
            Assert.Empty(GetMoveHistoryEntries(window));
            Assert.NotNull(emptyHistoryText);
            Assert.True(emptyHistoryText!.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task SaveLoadControls_FromGui_RestoreRoundTripState()
    {
        var window = CreateWindow(CreateSessionService());
        var savePath = Path.Combine(Path.GetTempPath(), $"chess-ui-save-{Path.GetRandomFileName()}.json");

        try
        {
            window.Show();
            window.Focus();

            var viewModel = Assert.IsType<MainWindowViewModel>(window.DataContext);
            viewModel.PersistenceFilePath = savePath;

            Click(FindSquareButton(window, "e2"));
            Click(FindSquareButton(window, "e4"));
            Assert.Equal("Status: In progress. Side to move: Black.", GetGameStatusText(window));

            Click(FindSaveGameButton(window));
            await WaitForConditionAsync(() => GetLastActionText(window) == $"Last action: Saved game to {savePath}.");
            Assert.True(File.Exists(savePath));
            Assert.Equal(string.Empty, GetFeedbackText(window));

            Click(FindStartNewGameButton(window));
            Assert.Equal("Status: In progress. Side to move: White.", GetGameStatusText(window));

            Click(FindLoadGameButton(window));
            await WaitForConditionAsync(() => GetLastActionText(window) == $"Last action: Loaded game from {savePath}.");

            var e2 = FindSquareButton(window, "e2");
            var e4 = FindSquareButton(window, "e4");
            AssertSquareHasNoPieceAsset(e2);
            AssertSquareHasPieceAsset(e4, "white-pawn");
            Assert.Equal("Status: In progress. Side to move: Black.", GetGameStatusText(window));
            Assert.Equal(string.Empty, GetFeedbackText(window));
        }
        finally
        {
            window.Close();

            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }
        }
    }

    [AvaloniaFact]
    public void MoveHistory_UpdatesAfterLegalMovesWithNumbering()
    {
        var window = CreateWindow(CreateSessionService());

        try
        {
            window.Show();
            window.Focus();

            Click(FindSquareButton(window, "e2"));
            Click(FindSquareButton(window, "e4"));
            Click(FindSquareButton(window, "e7"));
            Click(FindSquareButton(window, "e5"));
            window.UpdateLayout();

            Assert.Equal(
                new[] { "1. e2-e4", "1... e7-e5" },
                GetMoveHistoryEntries(window));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void MoveHistoryScrollViewer_BecomesScrollableForLongHistory()
    {
        var longHistoryState = CreateStateWithMoveHistory(moveCount: 120);
        var window = CreateWindow(new StubGameSessionService(longHistoryState));

        try
        {
            window.Show();
            window.UpdateLayout();

            var scrollViewer = window.FindControl<ScrollViewer>("MoveHistoryScrollViewer");
            Assert.NotNull(scrollViewer);

            var moveHistoryEntries = GetMoveHistoryEntries(window);
            Assert.Equal(120, moveHistoryEntries.Count);
            Assert.True(scrollViewer!.Extent.Height > scrollViewer.Viewport.Height);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void KeyboardInput_Wasd_UpdatesFocusedSquareThroughWindowKeyDown()
    {
        var window = CreateWindow(CreateSessionService());

        try
        {
            window.Show();
            window.Focus();
            PressKey(window, Key.W);
            Assert.Equal("Keyboard focus: e3.", GetFocusedSquareText(window));
            PressKey(window, Key.D);
            Assert.Equal("Keyboard focus: f3.", GetFocusedSquareText(window));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void KeyboardInput_Arrows_UpdatesFocusedSquareThroughWindowKeyDown()
    {
        var window = CreateWindow(CreateSessionService());

        try
        {
            window.Show();
            window.Focus();
            PressKey(window, Key.Up);
            Assert.Equal("Keyboard focus: e3.", GetFocusedSquareText(window));
            PressKey(window, Key.Left);
            Assert.Equal("Keyboard focus: d3.", GetFocusedSquareText(window));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void KeyboardInput_ArrowsFromSameBoardSource_MoveFocusCumulatively()
    {
        var window = CreateWindow(CreateSessionService());

        try
        {
            window.Show();
            window.Focus();
            var sourceButton = FindSquareButton(window, "e2");

            PressKey(window, Key.Up, sourceButton);
            Assert.Equal("Keyboard focus: e3.", GetFocusedSquareText(window));
            PressKey(window, Key.Up, sourceButton);
            Assert.Equal("Keyboard focus: e4.", GetFocusedSquareText(window));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void KeyboardInput_WasdFromSameBoardSource_MoveFocusCumulatively()
    {
        var window = CreateWindow(CreateSessionService());

        try
        {
            window.Show();
            window.Focus();
            var sourceButton = FindSquareButton(window, "e2");

            PressKey(window, Key.D, sourceButton);
            Assert.Equal("Keyboard focus: f2.", GetFocusedSquareText(window));
            PressKey(window, Key.D, sourceButton);
            Assert.Equal("Keyboard focus: g2.", GetFocusedSquareText(window));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void KeyboardInput_Enter_FromStaleBoardSource_CommitsMoveFromFocusedSquare()
    {
        var window = CreateWindow(CreateSessionService());

        try
        {
            window.Show();
            window.Focus();

            var sourceButton = FindSquareButton(window, "e2");
            var e4 = FindSquareButton(window, "e4");

            PressKey(window, Key.Enter, sourceButton);
            PressKey(window, Key.Up, sourceButton);
            PressKey(window, Key.Up, sourceButton);
            Assert.Equal("Keyboard focus: e4.", GetFocusedSquareText(window));
            PressKey(window, Key.Enter, sourceButton);

            Assert.Equal("Status: In progress. Side to move: Black.", GetGameStatusText(window));
            Assert.Equal("Last action: White moved Pawn from e2 to e4.", GetLastActionText(window));
            Assert.Equal(string.Empty, GetFeedbackText(window));
            AssertSquareHasNoPieceAsset(sourceButton);
            AssertSquareHasPieceAsset(e4, "white-pawn");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void KeyboardInput_Space_FromStaleBoardSource_CommitsMoveFromFocusedSquare()
    {
        var window = CreateWindow(CreateSessionService());

        try
        {
            window.Show();
            window.Focus();

            var sourceButton = FindSquareButton(window, "f2");
            var f3 = FindSquareButton(window, "f3");

            PressKey(window, Key.Right);
            Assert.Equal("Keyboard focus: f2.", GetFocusedSquareText(window));
            PressKey(window, Key.Space, sourceButton);
            PressKey(window, Key.Up, sourceButton);
            Assert.Equal("Keyboard focus: f3.", GetFocusedSquareText(window));
            PressKey(window, Key.Space, sourceButton);

            Assert.Equal("Status: In progress. Side to move: Black.", GetGameStatusText(window));
            Assert.Equal("Last action: White moved Pawn from f2 to f3.", GetLastActionText(window));
            Assert.Equal(string.Empty, GetFeedbackText(window));
            AssertSquareHasNoPieceAsset(sourceButton);
            AssertSquareHasPieceAsset(f3, "white-pawn");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void KeyboardInput_Enter_OnStartNewGameButton_ResetsGameWithoutApplyingFocusedSquareAction()
    {
        var window = CreateWindow(CreateSessionService());

        try
        {
            window.Show();
            window.Focus();

            var startNewGameButton = FindStartNewGameButton(window);
            var e2 = FindSquareButton(window, "e2");
            var e4 = FindSquareButton(window, "e4");

            Click(e2);
            Click(e4);
            Assert.Equal("Status: In progress. Side to move: Black.", GetGameStatusText(window));
            AssertSquareHasNoPieceAsset(e2);
            AssertSquareHasPieceAsset(e4, "white-pawn");

            startNewGameButton.Focus();
            PressKeyOnHeadlessWindow(window, Key.Enter, PhysicalKey.Enter);
            ReleaseKeyOnHeadlessWindow(window, Key.Enter, PhysicalKey.Enter);

            Assert.Equal("Status: In progress. Side to move: White.", GetGameStatusText(window));
            Assert.Equal("Last action: Started a new game.", GetLastActionText(window));
            Assert.Equal(string.Empty, GetFeedbackText(window));
            AssertSquareHasPieceAsset(e2, "white-pawn");
            AssertSquareHasNoPieceAsset(e4);
            Assert.Equal("Keyboard focus: e2.", GetFocusedSquareText(window));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void KeyboardInput_Space_OnStartNewGameButton_ResetsGameWithoutApplyingFocusedSquareAction()
    {
        var window = CreateWindow(CreateSessionService());

        try
        {
            window.Show();
            window.Focus();

            var startNewGameButton = FindStartNewGameButton(window);
            var e2 = FindSquareButton(window, "e2");
            var e4 = FindSquareButton(window, "e4");

            Click(e2);
            Click(e4);
            Assert.Equal("Status: In progress. Side to move: Black.", GetGameStatusText(window));
            AssertSquareHasNoPieceAsset(e2);
            AssertSquareHasPieceAsset(e4, "white-pawn");

            startNewGameButton.Focus();
            PressKeyOnHeadlessWindow(window, Key.Space, PhysicalKey.Space);
            ReleaseKeyOnHeadlessWindow(window, Key.Space, PhysicalKey.Space);

            Assert.Equal("Status: In progress. Side to move: White.", GetGameStatusText(window));
            Assert.Equal("Last action: Started a new game.", GetLastActionText(window));
            Assert.Equal(string.Empty, GetFeedbackText(window));
            AssertSquareHasPieceAsset(e2, "white-pawn");
            AssertSquareHasNoPieceAsset(e4);
            Assert.Equal("Keyboard focus: e2.", GetFocusedSquareText(window));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void KeyboardInput_Escape_ClearsSelectionFeedback()
    {
        var window = CreateWindow(CreateSessionService());

        try
        {
            window.Show();
            window.Focus();

            var e3 = FindSquareButton(window, "e3");
            Assert.False(GetSquareViewModel(e3).IsLegalDestinationIndicatorVisible);
            Click(FindSquareButton(window, "e2"));

            Assert.True(GetSquareViewModel(e3).IsLegalDestinationIndicatorVisible);

            PressKey(window, Key.Escape);

            Assert.Equal("Selection cleared.", GetFeedbackText(window));
            Assert.Equal("Keyboard focus: e2.", GetFocusedSquareText(window));
            Assert.False(GetSquareViewModel(e3).IsLegalDestinationIndicatorVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void KeyboardInput_Enter_OnSelectedFocusedSquare_ClearsSelection()
    {
        var window = CreateWindow(CreateSessionService());

        try
        {
            window.Show();
            window.Focus();

            var e3 = FindSquareButton(window, "e3");
            Assert.False(GetSquareViewModel(e3).IsLegalDestinationIndicatorVisible);
            Click(FindSquareButton(window, "e2"));
            Assert.True(GetSquareViewModel(e3).IsLegalDestinationIndicatorVisible);

            PressKey(window, Key.Enter);

            Assert.False(GetSquareViewModel(e3).IsLegalDestinationIndicatorVisible);
            Assert.Equal(string.Empty, GetFeedbackText(window));
            Assert.Equal("Keyboard focus: e2.", GetFocusedSquareText(window));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void BoardLayout_RemainsSquareWithSquareCells_DuringResize()
    {
        var window = CreateWindow(CreateSessionService());

        try
        {
            window.Show();
            window.Focus();

            foreach (var (width, height) in new[] { (580d, 920d), (1024d, 640d), (760d, 760d) })
            {
                ResizeWindow(window, width, height);

                var boardButtons = GetBoardSquareButtons(window);
                var boardBounds = GetAggregateBounds(boardButtons, window);
                Assert.InRange(Math.Abs(boardBounds.Width - boardBounds.Height), 0d, 0.75d);

                foreach (var boardButton in boardButtons)
                {
                    var bounds = GetBoundsRelativeToWindow(boardButton, window);
                    Assert.InRange(Math.Abs(bounds.Width - bounds.Height), 0d, 0.75d);
                }
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void BoardLayout_ShowsEdgeCoordinates_InStandardOrientation()
    {
        var window = CreateWindow(CreateSessionService());

        try
        {
            window.Show();

            var fileLabels = GetOrderedEdgeLabels(window, "FileCoordinatesItemsControl", sortByHorizontalAxis: true);
            var rankLabels = GetOrderedEdgeLabels(window, "RankCoordinatesItemsControl", sortByHorizontalAxis: false);

            Assert.Equal(new[] { "a", "b", "c", "d", "e", "f", "g", "h" }, fileLabels);
            Assert.Equal(new[] { "8", "7", "6", "5", "4", "3", "2", "1" }, rankLabels);
            AssertEdgeCoordinatesAdjacentToBoard(window);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void BoardLayout_HasNoInterCellGaps_DuringResize()
    {
        var window = CreateWindow(CreateSessionService());

        try
        {
            window.Show();
            window.Focus();

            foreach (var (width, height) in new[] { (600d, 900d), (980d, 620d), (680d, 680d) })
            {
                ResizeWindow(window, width, height);
                AssertNoInterCellGaps(window);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SidePanelLayout_StacksBelowBoardOnNarrowWindows()
    {
        var window = CreateWindow(CreateSessionService());

        try
        {
            window.Show();
            window.Focus();

            ResizeWindow(window, 1200d, 820d);
            var boardContainerBorder = FindBoardContainerBorder(window);
            var sidePanelBorder = FindSidePanelBorder(window);

            Assert.Equal(0, Grid.GetRow(sidePanelBorder));
            Assert.Equal(1, Grid.GetColumn(sidePanelBorder));
            var boardBoundsWide = GetBoundsRelativeToWindow(boardContainerBorder, window);
            var sidePanelBoundsWide = GetBoundsRelativeToWindow(sidePanelBorder, window);
            Assert.True(sidePanelBoundsWide.Left >= boardBoundsWide.Right - LayoutTolerance);
            Assert.InRange(Math.Abs(sidePanelBoundsWide.Top - boardBoundsWide.Top), 0d, LayoutTolerance);

            ResizeWindow(window, 760d, 940d);
            Assert.Equal(0, Grid.GetRow(boardContainerBorder));
            Assert.Equal(0, Grid.GetColumn(boardContainerBorder));
            Assert.Equal(1, Grid.GetRow(sidePanelBorder));
            Assert.Equal(0, Grid.GetColumn(sidePanelBorder));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PieceAssets_RenderOnLightAndDarkSquares()
    {
        var window = CreateWindow(CreateSessionService());

        try
        {
            window.Show();

            var d1 = FindSquareButton(window, "d1");
            var e1 = FindSquareButton(window, "e1");

            AssertSquareUsesSvgPrimaryAsset(d1, "white-queen");
            AssertSquareUsesSvgPrimaryAsset(e1, "white-king");
            Assert.NotEqual(d1.Background, e1.Background);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void FinishedGameState_ShowsResultAndBlocksMoveInteraction()
    {
        var finishedState = new GameState(
            Pieces:
            [
                new PiecePlacement(new Square(4, 0), new Piece(PieceType.King, PieceColor.White)),
                new PiecePlacement(new Square(4, 7), new Piece(PieceType.King, PieceColor.Black))
            ],
            SideToMove: PieceColor.White,
            CastlingRights: CastlingRights.None,
            EnPassantTarget: null,
            HalfmoveClock: 0,
            FullmoveNumber: 1,
            Status: GameStatus.Draw,
            MoveHistory: []);

        var stubSessionService = new StubGameSessionService(finishedState);
        var window = CreateWindow(stubSessionService);

        try
        {
            window.Show();

            Assert.Equal("Status: Draw.", GetGameStatusText(window));

            Click(FindSquareButton(window, "e1"));

            Assert.Equal("Game is finished (Draw). Start a new game to continue.", GetFeedbackText(window));
            Assert.Equal(0, stubSessionService.GetLegalMovesFromCallCount);
        }
        finally
        {
            window.Close();
        }
    }

    private static MainWindow CreateWindow(IGameSessionService sessionService)
    {
        var viewModel = new MainWindowViewModel(sessionService);
        return new MainWindow(viewModel);
    }

    private static GameSessionService CreateSessionService()
    {
        return new GameSessionService(new ChessGameEngine(), new JsonGameStateStore());
    }

    private static IReadOnlyList<Button> GetBoardSquareButtons(Window window)
    {
        return window.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.Classes.Contains("board-square"))
            .ToArray();
    }

    private static Button FindSquareButton(Window window, string coordinate)
    {
        return GetBoardSquareButtons(window)
            .Single(button => string.Equals(button.Tag?.ToString(), coordinate, StringComparison.OrdinalIgnoreCase));
    }

    private static Button FindStartNewGameButton(Window window)
    {
        return window.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => string.Equals(button.Content?.ToString(), "Start New Game", StringComparison.Ordinal));
    }

    private static Button FindSaveGameButton(Window window)
    {
        return Assert.IsType<Button>(window.FindControl<Button>("SaveGameButton"));
    }

    private static Button FindLoadGameButton(Window window)
    {
        return Assert.IsType<Button>(window.FindControl<Button>("LoadGameButton"));
    }

    private static CheckBox FindShowLegalMoveSuggestionsCheckBox(Window window)
    {
        return Assert.IsType<CheckBox>(window.FindControl<CheckBox>("ShowLegalMoveSuggestionsCheckBox"));
    }

    private static Border FindBoardContainerBorder(Window window)
    {
        return Assert.IsType<Border>(window.FindControl<Border>("BoardContainerBorder"));
    }

    private static Border FindSidePanelBorder(Window window)
    {
        return Assert.IsType<Border>(window.FindControl<Border>("SidePanelBorder"));
    }

    private static BoardSquareViewModel GetSquareViewModel(Button squareButton)
    {
        return Assert.IsType<BoardSquareViewModel>(squareButton.DataContext);
    }

    private static void AssertSquareHasNoPieceAsset(Button squareButton)
    {
        var squareViewModel = GetSquareViewModel(squareButton);
        Assert.Null(squareViewModel.PieceImage);
        Assert.Equal(string.Empty, squareViewModel.PieceAssetUri);
    }

    private static void AssertSquareHasPieceAsset(Button squareButton, string expectedAssetStem)
    {
        var squareViewModel = GetSquareViewModel(squareButton);
        Assert.NotNull(squareViewModel.PieceImage);
        Assert.Contains($"/{expectedAssetStem}.", squareViewModel.PieceAssetUri, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertSquareUsesSvgPrimaryAsset(Button squareButton, string expectedAssetStem)
    {
        var squareViewModel = GetSquareViewModel(squareButton);
        Assert.NotNull(squareViewModel.PieceImage);
        Assert.EndsWith($"/{expectedAssetStem}.svg", squareViewModel.PieceAssetUri, StringComparison.OrdinalIgnoreCase);
        Assert.False(squareViewModel.IsUsingFallbackAsset);
    }

    private static void Click(Button button)
    {
        Assert.NotNull(button.Command);
        Assert.True(button.Command!.CanExecute(button.CommandParameter));
        button.Command.Execute(button.CommandParameter);
    }

    private static void PressKey(Window window, Key key, object? source = null)
    {
        var keyEvent = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = key,
            KeyModifiers = KeyModifiers.None
        };

        if (source is not null)
        {
            keyEvent.Source = source;
        }

        if (source is InputElement inputElement)
        {
            inputElement.RaiseEvent(keyEvent);
            return;
        }

        window.RaiseEvent(keyEvent);
    }

    private static void PressKeyOnHeadlessWindow(Window window, Key key, PhysicalKey physicalKey)
    {
        window.KeyPress(key, RawInputModifiers.None, physicalKey, GetKeySymbol(key));
    }

    private static void ReleaseKeyOnHeadlessWindow(Window window, Key key, PhysicalKey physicalKey)
    {
        window.KeyRelease(key, RawInputModifiers.None, physicalKey, GetKeySymbol(key));
    }

    private static string GetKeySymbol(Key key)
    {
        return key == Key.Space ? " " : string.Empty;
    }

    private static void ResizeWindow(Window window, double width, double height)
    {
        window.Width = width;
        window.Height = height;
        window.UpdateLayout();
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, int timeoutMilliseconds = 3000)
    {
        var startedAt = DateTime.UtcNow;

        while (!condition())
        {
            if ((DateTime.UtcNow - startedAt).TotalMilliseconds > timeoutMilliseconds)
            {
                throw new TimeoutException("Timed out waiting for expected UI condition.");
            }

            await Task.Delay(20);
        }
    }

    private static Rect GetBoundsRelativeToWindow(Control control, Window window)
    {
        var topLeft = control.TranslatePoint(new Point(0, 0), window);
        var bottomRight = control.TranslatePoint(
            new Point(control.Bounds.Width, control.Bounds.Height),
            window);

        Assert.True(topLeft.HasValue);
        Assert.True(bottomRight.HasValue);

        var x = topLeft!.Value.X;
        var y = topLeft.Value.Y;
        var width = bottomRight!.Value.X - x;
        var height = bottomRight.Value.Y - y;

        return new Rect(x, y, width, height);
    }

    private static Rect GetAggregateBounds(IEnumerable<Control> controls, Window window)
    {
        var bounds = controls.Select(control => GetBoundsRelativeToWindow(control, window)).ToArray();
        Assert.NotEmpty(bounds);

        var minX = bounds.Min(rect => rect.X);
        var minY = bounds.Min(rect => rect.Y);
        var maxX = bounds.Max(rect => rect.Right);
        var maxY = bounds.Max(rect => rect.Bottom);
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    private static IReadOnlyList<string> GetOrderedEdgeLabels(Window window, string itemsControlName, bool sortByHorizontalAxis)
    {
        var itemsControl = window.FindControl<ItemsControl>(itemsControlName);
        Assert.NotNull(itemsControl);

        var labels = itemsControl!.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(textBlock => !string.IsNullOrWhiteSpace(textBlock.Text))
            .Select(textBlock =>
            {
                var topLeft = textBlock.TranslatePoint(new Point(0, 0), window);
                Assert.True(topLeft.HasValue);
                return new LabelSnapshot(textBlock.Text!, topLeft!.Value.X, topLeft.Value.Y);
            })
            .OrderBy(label => sortByHorizontalAxis ? label.X : label.Y)
            .Select(label => label.Text)
            .ToArray();

        return labels;
    }

    private static void AssertEdgeCoordinatesAdjacentToBoard(Window window)
    {
        var boardItemsControl = window.FindControl<ItemsControl>("BoardItemsControl");
        var fileCoordinatesItemsControl = window.FindControl<ItemsControl>("FileCoordinatesItemsControl");
        var rankCoordinatesItemsControl = window.FindControl<ItemsControl>("RankCoordinatesItemsControl");

        Assert.NotNull(boardItemsControl);
        Assert.NotNull(fileCoordinatesItemsControl);
        Assert.NotNull(rankCoordinatesItemsControl);

        var boardBounds = GetBoundsRelativeToWindow(boardItemsControl!, window);
        var fileBounds = GetBoundsRelativeToWindow(fileCoordinatesItemsControl!, window);
        var rankBounds = GetBoundsRelativeToWindow(rankCoordinatesItemsControl!, window);

        var rankToBoardGap = boardBounds.Left - rankBounds.Right;
        var boardToFileGap = fileBounds.Top - boardBounds.Bottom;

        Assert.InRange(rankToBoardGap, 0d, LayoutTolerance);
        Assert.InRange(boardToFileGap, 0d, LayoutTolerance);
        Assert.InRange(Math.Abs(rankBounds.Top - boardBounds.Top), 0d, LayoutTolerance);
        Assert.InRange(Math.Abs(rankBounds.Bottom - boardBounds.Bottom), 0d, LayoutTolerance);
        Assert.InRange(Math.Abs(fileBounds.Left - boardBounds.Left), 0d, LayoutTolerance);
        Assert.InRange(Math.Abs(fileBounds.Right - boardBounds.Right), 0d, LayoutTolerance);
    }

    private static void AssertNoInterCellGaps(Window window)
    {
        var squareBoundsByCoordinate = GetBoardSquareButtons(window)
            .ToDictionary(
                button => button.Tag?.ToString() ?? string.Empty,
                button => GetBoundsRelativeToWindow(button, window),
                StringComparer.OrdinalIgnoreCase);

        Assert.Equal(64, squareBoundsByCoordinate.Count);

        for (var rank = 1; rank <= 8; rank++)
        {
            for (var file = 0; file < 7; file++)
            {
                var leftCoordinate = ToCoordinate(file, rank);
                var rightCoordinate = ToCoordinate(file + 1, rank);
                var left = squareBoundsByCoordinate[leftCoordinate];
                var right = squareBoundsByCoordinate[rightCoordinate];
                var horizontalGap = right.X - left.Right;
                Assert.InRange(Math.Abs(horizontalGap), 0d, 0.75d);
            }
        }

        for (var rank = 1; rank < 8; rank++)
        {
            for (var file = 0; file < 8; file++)
            {
                var lowerCoordinate = ToCoordinate(file, rank);
                var upperCoordinate = ToCoordinate(file, rank + 1);
                var lower = squareBoundsByCoordinate[lowerCoordinate];
                var upper = squareBoundsByCoordinate[upperCoordinate];
                var verticalGap = lower.Y - upper.Bottom;
                Assert.InRange(Math.Abs(verticalGap), 0d, 0.75d);
            }
        }
    }

    private static string ToCoordinate(int file, int rank)
    {
        return $"{(char)('a' + file)}{rank}";
    }

    private static IReadOnlyList<string> GetMoveHistoryEntries(Window window)
    {
        var itemsControl = window.FindControl<ItemsControl>("MoveHistoryItemsControl");
        Assert.NotNull(itemsControl);

        var items = Assert.IsAssignableFrom<IEnumerable>(itemsControl!.ItemsSource ?? Array.Empty<string>());
        return items
            .Cast<object?>()
            .Select(item => item?.ToString())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Cast<string>()
            .ToArray();
    }

    private static GameState CreateStateWithMoveHistory(int moveCount)
    {
        var moveHistory = new List<Move>(moveCount);

        for (var index = 0; index < moveCount; index++)
        {
            var isWhiteMove = index % 2 == 0;
            var color = isWhiteMove ? PieceColor.White : PieceColor.Black;
            var fromRank = isWhiteMove ? 1 : 6;
            var toRank = isWhiteMove ? 2 : 5;
            var file = index % 8;

            moveHistory.Add(
                new Move(
                    From: new Square(file, fromRank),
                    To: new Square(file, toRank),
                    MovedPiece: new Piece(PieceType.Pawn, color, HasMoved: true)));
        }

        return new GameState(
            Pieces:
            [
                new PiecePlacement(new Square(4, 0), new Piece(PieceType.King, PieceColor.White)),
                new PiecePlacement(new Square(4, 7), new Piece(PieceType.King, PieceColor.Black))
            ],
            SideToMove: moveCount % 2 == 0 ? PieceColor.White : PieceColor.Black,
            CastlingRights: CastlingRights.None,
            EnPassantTarget: null,
            HalfmoveClock: 0,
            FullmoveNumber: 1,
            Status: GameStatus.InProgress,
            MoveHistory: moveHistory);
    }

    private static string GetGameStatusText(Window window)
    {
        var textBlock = window.FindControl<TextBlock>("GameStatusTextBlock");
        return textBlock?.Text ?? string.Empty;
    }

    private static string GetFeedbackText(Window window)
    {
        var textBlock = window.FindControl<TextBlock>("FeedbackTextBlock");
        return textBlock?.Text ?? string.Empty;
    }

    private static string GetLastActionText(Window window)
    {
        var textBlock = window.FindControl<TextBlock>("LastActionTextBlock");
        return textBlock?.Text ?? string.Empty;
    }

    private static string GetFocusedSquareText(Window window)
    {
        var textBlock = window.FindControl<TextBlock>("FocusedSquareTextBlock");
        return textBlock?.Text ?? string.Empty;
    }

    private readonly record struct LabelSnapshot(string Text, double X, double Y);

    private sealed class StubGameSessionService : IGameSessionService
    {
        private readonly GameState _gameState;

        public StubGameSessionService(GameState gameState)
        {
            _gameState = gameState;
        }

        public int GetLegalMovesFromCallCount { get; private set; }

        public GameState StartNewGame()
        {
            return _gameState;
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
            return Task.FromResult(_gameState);
        }

        public GameState CurrentGameState => _gameState;
    }
}
