using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Chess.UI.ViewModels;

namespace Chess;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(InputElement.KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel, true);
    }

    public MainWindow(MainWindowViewModel viewModel)
        : this()
    {
        System.ArgumentNullException.ThrowIfNull(viewModel);
        DataContext = viewModel;
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var isBoardSquareSource = e.Source is Control { DataContext: BoardSquareViewModel };

        if ((e.Key is Key.Enter or Key.Space)
            && !isBoardSquareSource
            && e.Source is Button)
        {
            return;
        }

        if (viewModel.HandleKeyboardInput(e.Key))
        {
            e.Handled = true;
        }
    }
}
