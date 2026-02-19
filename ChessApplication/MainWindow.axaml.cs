using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Chess.UI.ViewModels;

namespace Chess;

public partial class MainWindow : Window
{
    private const double NarrowLayoutBreakpointWidth = 980d;
    private bool _isNarrowLayout;

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(InputElement.KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel, true);
        SizeChanged += OnWindowSizeChanged;
        LayoutUpdated += OnWindowLayoutUpdated;
        UpdateResponsiveLayout(GetResponsiveWidth(Bounds.Width));
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

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateResponsiveLayout(GetResponsiveWidth(e.NewSize.Width));
    }

    private void OnWindowLayoutUpdated(object? sender, EventArgs e)
    {
        UpdateResponsiveLayout(GetResponsiveWidth(Bounds.Width));
    }

    private double GetResponsiveWidth(double fallbackWidth)
    {
        return !double.IsNaN(Width) && Width > 0 ? Width : fallbackWidth;
    }

    private void UpdateResponsiveLayout(double windowWidth)
    {
        var shouldUseNarrowLayout = windowWidth < NarrowLayoutBreakpointWidth;
        if (_isNarrowLayout == shouldUseNarrowLayout)
        {
            return;
        }

        _isNarrowLayout = shouldUseNarrowLayout;

        if (_isNarrowLayout)
        {
            MainContentGrid.ColumnDefinitions = new ColumnDefinitions("*");
            MainContentGrid.RowDefinitions = new RowDefinitions("Auto,Auto");
            MainContentGrid.ColumnSpacing = 0;
            MainContentGrid.RowSpacing = 16;

            Grid.SetRow(BoardContainerBorder, 0);
            Grid.SetColumn(BoardContainerBorder, 0);
            Grid.SetRow(SidePanelBorder, 1);
            Grid.SetColumn(SidePanelBorder, 0);

            SidePanelBorder.Width = double.NaN;
            SidePanelBorder.MinWidth = 0;
            SidePanelBorder.MaxWidth = double.PositiveInfinity;
            SidePanelBorder.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            return;
        }

        MainContentGrid.ColumnDefinitions = new ColumnDefinitions("*,300");
        MainContentGrid.RowDefinitions = new RowDefinitions("*");
        MainContentGrid.ColumnSpacing = 16;
        MainContentGrid.RowSpacing = 0;

        Grid.SetRow(BoardContainerBorder, 0);
        Grid.SetColumn(BoardContainerBorder, 0);
        Grid.SetRow(SidePanelBorder, 0);
        Grid.SetColumn(SidePanelBorder, 1);

        SidePanelBorder.Width = 300;
        SidePanelBorder.MinWidth = 260;
        SidePanelBorder.MaxWidth = 340;
        SidePanelBorder.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
    }
}
