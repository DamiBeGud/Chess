using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Chess.UI.Commands;

/// <summary>
/// AsyncRelayCommand is a concrete type within the UI/Commands module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include MainWindowViewModel (UI/ViewModels).
/// Key collaborators are Task, Exception, bool, ICommand.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MainWindowViewModel (UI/ViewModels)</para>
/// <para><b>Usage pattern:</b> UI controls trigger this command via bindings, and execution delegates run with command-state notifications for CanExecute changes.</para>
/// <para><b>Dependencies/Collaborators:</b> Task, Exception, bool, ICommand.</para>
/// <para><b>Boundary:</b> This type sits in the UI/Commands UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _executeAsync;
    private readonly Func<bool>? _canExecute;
    private readonly Action<Exception>? _onException;
    private bool _isExecuting;

    public AsyncRelayCommand(
        Func<Task> executeAsync,
        Action<Exception>? onException = null,
        Func<bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        _executeAsync = executeAsync;
        _onException = onException;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return !_isExecuting && (_canExecute?.Invoke() ?? true);
    }

    public void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _ = ExecuteCoreAsync();
    }

    public void NotifyCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task ExecuteCoreAsync()
    {
        _isExecuting = true;
        NotifyCanExecuteChanged();

        try
        {
            await _executeAsync();
        }
        catch (Exception exception)
        {
            _onException?.Invoke(exception);
        }
        finally
        {
            _isExecuting = false;
            NotifyCanExecuteChanged();
        }
    }
}
