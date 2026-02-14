using Avalonia.Controls;
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
}
