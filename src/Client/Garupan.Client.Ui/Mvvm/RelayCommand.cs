using System;
using System.Windows.Input;
using Opus.Foundation;

namespace Garupan.Client.Ui.Mvvm;

/// <summary>
/// Carry-forward of the legacy command. Buttons / menu items bind to this — the click
/// handler invokes <see cref="Execute"/>, the visual disabled state mirrors <see cref="CanExecute"/>.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        Ensure.NotNull(execute);
        _execute = _ => execute();
        _canExecute = canExecute is null ? null : _ => canExecute();
    }

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = Ensure.NotNull(execute);
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
