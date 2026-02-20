using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Chess.AppCore;
using Chess.Domain;

namespace Chess.UI.Services;

/// <summary>
/// MainWindowPersistenceCoordinator is a concrete type within the UI/Services module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include MainWindowViewModel (UI/ViewModels).
/// Key collaborators are IGameSessionService, IMainWindowPersistenceCoordinator.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MainWindowViewModel (UI/ViewModels)</para>
/// <para><b>Usage pattern:</b> View models delegate focused interaction steps to this type, which applies deterministic UI workflow logic for the given context.</para>
/// <para><b>Dependencies/Collaborators:</b> IGameSessionService, IMainWindowPersistenceCoordinator.</para>
/// <para><b>Boundary:</b> This type sits in the UI/Services UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
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
