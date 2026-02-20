using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Chess.Online;
using Chess.UI.ViewModels;

namespace Chess;

/// <summary>
/// MainWindow is a concrete type within the AppShell module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include App (AppShell).
/// Key collaborators are MainWindowViewModel, IOnlineMatchSessionService, Window.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> App (AppShell)</para>
/// <para><b>Usage pattern:</b> Startup code constructs and wires this type during application initialization and desktop-lifetime setup.</para>
/// <para><b>Dependencies/Collaborators:</b> MainWindowViewModel, IOnlineMatchSessionService, Window.</para>
/// <para><b>Boundary:</b> This type sits in the application shell boundary and participates in startup or desktop lifetime wiring.</para>
/// </remarks>
public partial class MainWindow : Window
{
    private const double NarrowLayoutBreakpointWidth = 980d;
    private MainWindowViewModel? _ownedViewModel;
    private IOnlineMatchSessionService? _ownedOnlineMatchSessionService;
    private int _ownedViewModelDisposed;
    private int _onlineShutdownState;
    private bool _isNarrowLayout;

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(InputElement.KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel, true);
        SizeChanged += OnWindowSizeChanged;
        LayoutUpdated += OnWindowLayoutUpdated;
        Closing += OnWindowClosing;
        UpdateResponsiveLayout(GetResponsiveWidth(Bounds.Width));
    }

    public MainWindow(MainWindowViewModel viewModel)
        : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _ownedViewModel = viewModel;
        DataContext = viewModel;
    }

    public MainWindow(
        MainWindowViewModel viewModel,
        IOnlineMatchSessionService onlineMatchSessionService)
        : this(viewModel)
    {
        ArgumentNullException.ThrowIfNull(onlineMatchSessionService);
        _ownedOnlineMatchSessionService = onlineMatchSessionService;
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

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        var onlineService = _ownedOnlineMatchSessionService;
        if (onlineService is null)
        {
            DisposeOwnedViewModel();
            return;
        }

        var shutdownState = Volatile.Read(ref _onlineShutdownState);
        if (shutdownState == 2)
        {
            return;
        }

        e.Cancel = true;
        if (shutdownState == 1)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _onlineShutdownState, 1, 0) != 0)
        {
            return;
        }

        DisposeOwnedViewModel();
        _ = DisposeOwnedOnlineServiceAndCloseAsync(onlineService);
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

    private void DisposeOwnedViewModel()
    {
        if (_ownedViewModel is null)
        {
            return;
        }

        if (Interlocked.Exchange(ref _ownedViewModelDisposed, 1) != 0)
        {
            return;
        }

        _ownedViewModel.Dispose();
    }

    private async Task DisposeOwnedOnlineServiceAndCloseAsync(IOnlineMatchSessionService onlineService)
    {
        try
        {
            await onlineService.DisposeAsync();
        }
        catch (Exception)
        {
            // Best effort during shutdown: continue closing even if disposal fails.
        }
        finally
        {
            Interlocked.Exchange(ref _onlineShutdownState, 2);

            try
            {
                Dispatcher.UIThread.Post(
                    static state => ((MainWindow)state!).Close(),
                    this,
                    DispatcherPriority.Background);
            }
            catch (ObjectDisposedException)
            {
                // Window/dispatcher already torn down.
            }
            catch (InvalidOperationException)
            {
                // Dispatcher is no longer accepting work.
            }
        }
    }
}
