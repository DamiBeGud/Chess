using Avalonia.Input;

namespace Chess.UI.Services;

/// <summary>
/// MainWindowKeyboardActionKind is an enumeration within the UI/Services module.
/// Its named values model a bounded set of states, options, or outcomes used by collaborators.
/// Primary production consumers include MainWindowKeyboardNavigator (UI/Services), MainWindowViewModel (UI/ViewModels), MainWindowKeyboardAction (UI/Services).
/// Its values are interpreted by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MainWindowKeyboardNavigator (UI/Services), MainWindowViewModel (UI/ViewModels), MainWindowKeyboardAction (UI/Services)</para>
/// <para><b>Usage pattern:</b> View models delegate focused interaction steps to this type, which applies deterministic UI workflow logic for the given context.</para>
/// <para><b>Dependencies/Collaborators:</b> Its values are interpreted by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the UI/Services UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
internal enum MainWindowKeyboardActionKind
{
    None = 0,
    MoveFocus,
    CommitFocusedSquare,
    ClearSelection
}

/// <summary>
/// MainWindowKeyboardAction is a record type within the UI/Services module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include MainWindowKeyboardNavigator (UI/Services), IMainWindowKeyboardNavigator (UI/Services).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MainWindowKeyboardNavigator (UI/Services), IMainWindowKeyboardNavigator (UI/Services)</para>
/// <para><b>Usage pattern:</b> View models delegate focused interaction steps to this type, which applies deterministic UI workflow logic for the given context.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the UI/Services UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
internal readonly record struct MainWindowKeyboardAction(
    MainWindowKeyboardActionKind Kind,
    int FileDelta = 0,
    int RankDelta = 0);

/// <summary>
/// IMainWindowKeyboardNavigator defines a contract within the UI/Services module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include MainWindowViewModel (UI/ViewModels), MainWindowKeyboardNavigator (UI/Services).
/// Key collaborators are Implementations include MainWindowKeyboardNavigator (UI/Services).
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MainWindowViewModel (UI/ViewModels), MainWindowKeyboardNavigator (UI/Services)</para>
/// <para><b>Usage pattern:</b> View models delegate focused interaction steps to this type, which applies deterministic UI workflow logic for the given context.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include MainWindowKeyboardNavigator (UI/Services).</para>
/// <para><b>Boundary:</b> This type sits in the UI/Services UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
internal interface IMainWindowKeyboardNavigator
{
    MainWindowKeyboardAction Resolve(Key key);
}
