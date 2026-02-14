using Avalonia.Controls;
using Avalonia.Input;
using Chess.UI.ViewModels;

namespace Chess;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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

        if (viewModel.HandleKeyboardInput(e.Key))
        {
            e.Handled = true;
        }
    }
}
