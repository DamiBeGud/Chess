using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Chess.AppCore;
using Chess.Domain;
using Chess.UI.Commands;

namespace Chess.UI.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IGameSessionService _gameSessionService;
    private string _gameSummary = string.Empty;

    public MainWindowViewModel(IGameSessionService gameSessionService)
    {
        System.ArgumentNullException.ThrowIfNull(gameSessionService);
        _gameSessionService = gameSessionService;
        NewGameCommand = new RelayCommand(StartNewGame);
        StartNewGame();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title => "Chess";

    public string GameSummary
    {
        get => _gameSummary;
        private set
        {
            if (_gameSummary == value)
            {
                return;
            }

            _gameSummary = value;
            OnPropertyChanged();
        }
    }

    public ICommand NewGameCommand { get; }

    public void StartNewGame()
    {
        var gameState = _gameSessionService.StartNewGame();
        GameSummary = BuildGameSummary(gameState);
    }

    private static string BuildGameSummary(GameState gameState)
    {
        return $"New game ready. Side to move: {gameState.SideToMove}. Pieces on board: {gameState.Pieces.Count}.";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
