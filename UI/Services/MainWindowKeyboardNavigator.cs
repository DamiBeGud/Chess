using Avalonia.Input;

namespace Chess.UI.Services;

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
