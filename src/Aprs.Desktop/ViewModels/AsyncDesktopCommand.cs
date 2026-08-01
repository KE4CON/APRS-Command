using System.Windows.Input;

namespace Aprs.Desktop.ViewModels;

/// <summary>
/// An <see cref="ICommand"/> for asynchronous handlers. Runs the supplied task without blocking the
/// UI thread, and ignores re-entrant invocations while a previous run is still in flight (so a
/// double-click cannot start the work twice).
/// </summary>
public sealed class AsyncDesktopCommand : ICommand
{
    private readonly Func<Task> executeAsync;
    private readonly Func<bool>? canExecute;
    private bool isRunning;

    public AsyncDesktopCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
    {
        this.executeAsync = executeAsync;
        this.canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !isRunning && (canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (isRunning) return;
        isRunning = true;
        RaiseCanExecuteChanged();
        try
        {
            await executeAsync().ConfigureAwait(true);
        }
        finally
        {
            isRunning = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
