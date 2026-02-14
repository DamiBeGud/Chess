using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Chess.AppCore;
using Chess.Engine;
using Chess.Persistence;
using Chess.UI.ViewModels;

namespace Chess;

public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var gameEngine = new ChessGameEngine();
            var gameStateStore = new JsonGameStateStore();
            var gameSessionService = new GameSessionService(gameEngine, gameStateStore);
            var mainWindowViewModel = new MainWindowViewModel(gameSessionService);

            desktop.MainWindow = new MainWindow(mainWindowViewModel);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
