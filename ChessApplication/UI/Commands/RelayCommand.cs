using System;
using System.Windows.Input;

namespace Chess.UI.Commands;

/// <summary>
/// RelayCommand is a concrete type within the UI/Commands module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include MainWindowViewModel (UI/ViewModels).
/// Key collaborators are Action, bool, ICommand.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MainWindowViewModel (UI/ViewModels)</para>
/// <para><b>Usage pattern:</b> UI controls trigger this command via bindings, and execution delegates run with command-state notifications for CanExecute changes.</para>
/// <para><b>Dependencies/Collaborators:</b> Action, bool, ICommand.</para>
/// <para><b>Boundary:</b> This type sits in the UI/Commands UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke() ?? true;
    }

    public void Execute(object? parameter)
    {
        _execute();
    }
}
