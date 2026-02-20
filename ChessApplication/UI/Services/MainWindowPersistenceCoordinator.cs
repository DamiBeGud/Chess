using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Chess.AppCore;
using Chess.Domain;

namespace Chess.UI.Services;

internal sealed class MainWindowPersistenceCoordinator : IMainWindowPersistenceCoordinator
{
    private readonly IGameSessionService _gameSessionService;

    internal MainWindowPersistenceCoordinator(IGameSessionService gameSessionService)
    {
        _gameSessionService = gameSessionService ?? throw new ArgumentNullException(nameof(gameSessionService));
    }

    public async Task SaveGameAsync(
        string? persistenceFilePath,
        IMainWindowPersistenceContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.IsOnlineMatchActive)
        {
            context.SetFeedback("Saving local files is disabled while an online match is active.");
            return;
        }

        var filePath = persistenceFilePath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            context.SetFeedback("Save file path is required.");
            return;
        }

        try
        {
            await _gameSessionService.SaveAsync(filePath, cancellationToken);
            context.SetLastAction($"Last action: Saved game to {filePath}.");
            context.SetFeedback(string.Empty);
        }
        catch (Exception exception) when (
            exception is InvalidDataException
            or UnauthorizedAccessException
            or IOException
            or ArgumentException)
        {
            context.SetFeedback($"Unable to save game: {exception.Message}");
        }
    }

    public async Task LoadGameAsync(
        string? persistenceFilePath,
        Square defaultFocusSquare,
        IMainWindowPersistenceContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.IsOnlineMatchActive)
        {
            context.SetFeedback("Loading local files is disabled while an online match is active.");
            return;
        }

        var filePath = persistenceFilePath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            context.SetFeedback("Save file path is required.");
            return;
        }

        try
        {
            context.CancelInFlightAiTurn();
            await _gameSessionService.LoadAsync(filePath, cancellationToken);
            context.ClearSelection();
            context.SetFocusedSquare(defaultFocusSquare);
            context.SetLastAction($"Last action: Loaded game from {filePath}.");
            context.SetFeedback(string.Empty);
            context.RefreshBoardFromCurrentState();
            context.QueueAiTurnIfNeeded();
        }
        catch (Exception exception) when (
            exception is InvalidDataException
            or UnauthorizedAccessException
            or IOException
            or ArgumentException)
        {
            context.SetFeedback($"Unable to load game: {exception.Message}");
        }
    }
}
