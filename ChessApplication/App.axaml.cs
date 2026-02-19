using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Chess.AI;
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
            var aiPositionEvaluator = new MaterialMobilityPositionEvaluator(gameEngine);
            var aiMoveSelector = new NegamaxAiMoveSelector(gameEngine, aiPositionEvaluator);
            var aiTurnService = new AiTurnService(gameSessionService, aiMoveSelector);
            var mainWindowViewModel = new MainWindowViewModel(gameSessionService, aiTurnService);

            desktop.MainWindow = new MainWindow(mainWindowViewModel);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
