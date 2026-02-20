using System;
using System.Threading;
using System.Threading.Tasks;
using Chess.Domain;

namespace Chess.UI.Services;

/// <summary>
/// IMainWindowPersistenceContext defines a contract within the UI/Services module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include OnlineOperation (UI/ViewModels), IMainWindowPersistenceCoordinator (UI/Services), MainWindowPersistenceCoordinator (UI/Services).
/// Key collaborators are Implementations include MainWindowViewModel (UI/ViewModels).
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> OnlineOperation (UI/ViewModels), IMainWindowPersistenceCoordinator (UI/Services), MainWindowPersistenceCoordinator (UI/Services), MainWindowViewModel (UI/ViewModels)</para>
/// <para><b>Usage pattern:</b> View models delegate focused interaction steps to this type, which applies deterministic UI workflow logic for the given context.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include MainWindowViewModel (UI/ViewModels).</para>
/// <para><b>Boundary:</b> This type sits in the UI/Services UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
internal interface IMainWindowPersistenceContext
{
    bool IsOnlineMatchActive { get; }

    void SetFeedback(string feedback);

    void SetLastAction(string lastAction);

    void CancelInFlightAiTurn();

    void ClearSelection();

    void SetFocusedSquare(Square square);

    void RefreshBoardFromCurrentState();

    void QueueAiTurnIfNeeded();
}

/// <summary>
/// IMainWindowPersistenceCoordinator defines a contract within the UI/Services module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include MainWindowViewModel (UI/ViewModels), MainWindowPersistenceCoordinator (UI/Services).
/// Key collaborators are Implementations include MainWindowPersistenceCoordinator (UI/Services).
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MainWindowViewModel (UI/ViewModels), MainWindowPersistenceCoordinator (UI/Services)</para>
/// <para><b>Usage pattern:</b> View models delegate focused interaction steps to this type, which applies deterministic UI workflow logic for the given context.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include MainWindowPersistenceCoordinator (UI/Services).</para>
/// <para><b>Boundary:</b> This type sits in the UI/Services UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
internal interface IMainWindowPersistenceCoordinator
{
    Task SaveGameAsync(
        string? persistenceFilePath,
        IMainWindowPersistenceContext context,
        CancellationToken cancellationToken = default);

    Task LoadGameAsync(
        string? persistenceFilePath,
        Square defaultFocusSquare,
        IMainWindowPersistenceContext context,
        CancellationToken cancellationToken = default);
}
