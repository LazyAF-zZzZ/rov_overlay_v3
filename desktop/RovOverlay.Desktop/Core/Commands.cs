using System.Windows.Input;

namespace RovOverlay.Desktop.Core;

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _run;
    private readonly Func<object?, bool>? _canRun;

    public RelayCommand(Action run, Func<bool>? canRun = null)
        : this(_ => run(), canRun is null ? null : _ => canRun()) { }

    public RelayCommand(Action<object?> run, Func<object?, bool>? canRun = null)
    {
        _run = run;
        _canRun = canRun;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canRun?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _run(parameter);
}

// An async command that cannot be double-fired while it runs, and whose failures
// reach the operator as a toast instead of vanishing into an unobserved Task.
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _run;
    private readonly Func<object?, bool>? _canRun;
    private bool _busy;

    public AsyncRelayCommand(Func<Task> run, Func<bool>? canRun = null)
        : this(_ => run(), canRun is null ? null : _ => canRun()) { }

    public AsyncRelayCommand(Func<object?, Task> run, Func<object?, bool>? canRun = null)
    {
        _run = run;
        _canRun = canRun;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => !_busy && (_canRun?.Invoke(parameter) ?? true);

    public async void Execute(object? parameter)
    {
        if (_busy) return;
        _busy = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            await _run(parameter);
        }
        catch (Exception error)
        {
            Services.Toasts.Error(error.Message);
        }
        finally
        {
            _busy = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
