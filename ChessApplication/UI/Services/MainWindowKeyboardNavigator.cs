using Avalonia.Input;

namespace Chess.UI.Services;

/// <summary>
/// MainWindowKeyboardNavigator is a concrete type within the UI/Services module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include MainWindowViewModel (UI/ViewModels).
/// Key collaborators are IMainWindowKeyboardNavigator.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MainWindowViewModel (UI/ViewModels)</para>
/// <para><b>Usage pattern:</b> View models delegate focused interaction steps to this type, which applies deterministic UI workflow logic for the given context.</para>
/// <para><b>Dependencies/Collaborators:</b> IMainWindowKeyboardNavigator.</para>
/// <para><b>Boundary:</b> This type sits in the UI/Services UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
internal sealed class MainWindowKeyboardNavigator : IMainWindowKeyboardNavigator
{
    public MainWindowKeyboardAction Resolve(Key key)
    {
        return key switch
        {
            Key.Left or Key.A => new MainWindowKeyboardAction(MainWindowKeyboardActionKind.MoveFocus, FileDelta: -1),
            Key.Right or Key.D => new MainWindowKeyboardAction(MainWindowKeyboardActionKind.MoveFocus, FileDelta: 1),
            Key.Up or Key.W => new MainWindowKeyboardAction(MainWindowKeyboardActionKind.MoveFocus, RankDelta: 1),
            Key.Down or Key.S => new MainWindowKeyboardAction(MainWindowKeyboardActionKind.MoveFocus, RankDelta: -1),
            Key.Enter or Key.Space => new MainWindowKeyboardAction(MainWindowKeyboardActionKind.CommitFocusedSquare),
            Key.Escape => new MainWindowKeyboardAction(MainWindowKeyboardActionKind.ClearSelection),
            _ => default
        };
    }
}
