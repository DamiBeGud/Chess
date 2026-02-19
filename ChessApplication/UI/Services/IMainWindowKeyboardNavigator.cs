using Avalonia.Input;

namespace Chess.UI.Services;

internal enum MainWindowKeyboardActionKind
{
    None = 0,
    MoveFocus,
    CommitFocusedSquare,
    ClearSelection
}

internal readonly record struct MainWindowKeyboardAction(
    MainWindowKeyboardActionKind Kind,
    int FileDelta = 0,
    int RankDelta = 0);

internal interface IMainWindowKeyboardNavigator
{
    MainWindowKeyboardAction Resolve(Key key);
}
