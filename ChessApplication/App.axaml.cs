using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Chess.AI;
using Chess.AppCore;
using Chess.Engine;
using Chess.Online;
using Chess.Persistence;
using Chess.UI.Assets;
using Chess.UI.ViewModels;
using System.Net.Http;

namespace Chess;

/// <summary>
/// App is a concrete type within the AppShell module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include Program (AppShell).
/// Key collaborators are Application.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> Program (AppShell)</para>
/// <para><b>Usage pattern:</b> Startup code constructs and wires this type during application initialization and desktop-lifetime setup.</para>
/// <para><b>Dependencies/Collaborators:</b> Application.</para>
/// <para><b>Boundary:</b> This type sits in the application shell boundary and participates in startup or desktop lifetime wiring.</para>
/// </remarks>
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
            var multiplayerOptions = MultiplayerServerOptions.FromEnvironment();
            var gameEngine = new ChessGameEngine();
            var gameStateStore = new JsonGameStateStore();
            var gameSessionService = new GameSessionService(gameEngine, gameStateStore);
            var aiPositionEvaluator = new MaterialMobilityPositionEvaluator(gameEngine);
            var aiMoveSelector = new NegamaxAiMoveSelector(gameEngine, aiPositionEvaluator);
            var aiTurnService = new AiTurnService(gameSessionService, aiMoveSelector);

            var multiplayerHttpClient = new HttpClient
            {
                BaseAddress = multiplayerOptions.BaseUri
            };
            var onlineErrorMapper = new OnlineErrorMapper();
            var onlineHttpClient = new MultiplayerServerHttpClient(multiplayerHttpClient, onlineErrorMapper);
            var realtimeFactory = new SignalROnlineMatchRealtimeClientFactory(multiplayerOptions.BaseUri);
            var snapshotMapper = new OnlineSnapshotGameStateMapper();
            var realtimeReducer = new OnlineRealtimeEventReducer();
            var onlineMatchSessionService = new OnlineMatchSessionService(
                onlineHttpClient,
                onlineErrorMapper,
                realtimeFactory,
                snapshotMapper,
                realtimeReducer);

            var mainWindowViewModel = new MainWindowViewModel(
                gameSessionService,
                new PieceAssetResolver(),
                aiTurnService,
                onlineMatchSessionService);

            // MainWindow owns online session lifecycle and disposes it once during window/app shutdown.
            desktop.MainWindow = new MainWindow(mainWindowViewModel, onlineMatchSessionService);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
