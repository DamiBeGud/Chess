using System;
using System.IO;
using System.Threading.Tasks;
using Chess.AppCore;
using Chess.Domain;
using Chess.Engine;
using Chess.Persistence;
using Chess.UI.Services;
using Xunit;

namespace Chess.Tests;

public sealed class MainWindowPersistenceCoordinatorTests
{
    [Fact]
    public async Task SaveGameAsync_WithValidPath_SavesAndSetsLastAction()
    {
        var session = new GameSessionService(new ChessGameEngine(), new JsonGameStateStore());
        var coordinator = new MainWindowPersistenceCoordinator(session);
        var context = new FakePersistenceContext();
        var savePath = Path.Combine(Path.GetTempPath(), $"chess-persistence-{Path.GetRandomFileName()}.json");

        try
        {
            await coordinator.SaveGameAsync(savePath, context);

            Assert.True(File.Exists(savePath));
            Assert.Equal($"Last action: Saved game to {savePath}.", context.LastAction);
            Assert.Equal(string.Empty, context.Feedback);
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
    public async Task LoadGameAsync_WhenOnlineMatchIsActive_ShowsGuardFeedback()
    {
        var session = new GameSessionService(new ChessGameEngine(), new JsonGameStateStore());
        var coordinator = new MainWindowPersistenceCoordinator(session);
        var context = new FakePersistenceContext
        {
            IsOnlineMatchActive = true
        };

        await coordinator.LoadGameAsync(
            persistenceFilePath: "ignored.json",
            defaultFocusSquare: new Square(4, 1),
            context: context);

        Assert.Equal("Loading local files is disabled while an online match is active.", context.Feedback);
        Assert.Equal(0, context.CancelInFlightAiTurnCallCount);
    }

    private sealed class FakePersistenceContext : IMainWindowPersistenceContext
    {
        public bool IsOnlineMatchActive { get; set; }

        public string Feedback { get; private set; } = string.Empty;

        public string LastAction { get; private set; } = string.Empty;

        public int CancelInFlightAiTurnCallCount { get; private set; }

        public int ClearSelectionCallCount { get; private set; }

        public int RefreshBoardCallCount { get; private set; }

        public int QueueAiTurnCallCount { get; private set; }

        public Square? LastFocusedSquare { get; private set; }

        public void SetFeedback(string feedback)
        {
            Feedback = feedback;
        }

        public void SetLastAction(string lastAction)
        {
            LastAction = lastAction;
        }

        public void CancelInFlightAiTurn()
        {
            CancelInFlightAiTurnCallCount++;
        }

        public void ClearSelection()
        {
            ClearSelectionCallCount++;
        }

        public void SetFocusedSquare(Square square)
        {
            LastFocusedSquare = square;
        }

        public void RefreshBoardFromCurrentState()
        {
            RefreshBoardCallCount++;
        }

        public void QueueAiTurnIfNeeded()
        {
            QueueAiTurnCallCount++;
        }
    }
}
