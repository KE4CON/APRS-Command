using System.Windows.Input;

namespace Aprs.Desktop.ViewModels;

public sealed class DesktopCommand : ICommand
{
    private readonly Action? execute;
    private readonly Action<object?>? executeWithParam;
    private readonly Func<bool>? canExecute;

    public DesktopCommand(Action execute, Func<bool>? canExecute = null)
    {
        this.execute = execute;
        this.canExecute = canExecute;
    }

    /// <summary>Command that receives the bound CommandParameter (e.g. a menu item's value).</summary>
    public DesktopCommand(Action<object?> execute, Func<bool>? canExecute = null)
    {
        this.executeWithParam = execute;
        this.canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return canExecute?.Invoke() ?? true;
    }

    public void Execute(object? parameter)
    {
        if (executeWithParam is not null) executeWithParam(parameter);
        else execute?.Invoke();
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
