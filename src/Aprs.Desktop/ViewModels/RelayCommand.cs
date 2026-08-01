using System.Windows.Input;

namespace Aprs.Desktop.ViewModels;

/// <summary>
/// A parameterized <see cref="ICommand"/> whose handler receives the command parameter — the shared
/// implementation for commands that act on a bound item (for example a selected list row). The
/// parameterless <see cref="DesktopCommand"/> covers the common case; use this when the parameter is
/// needed. Consolidates what used to be duplicated file-scoped command types across viewmodels.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> execute;
    private readonly Func<object?, bool>? canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        this.execute = execute;
        this.canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => execute(parameter);

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
