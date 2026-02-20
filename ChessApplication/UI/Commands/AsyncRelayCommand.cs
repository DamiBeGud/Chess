using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Chess.UI.Commands;

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
