using System.Windows.Threading;

namespace RovOverlay.Desktop.Core;

// Waits for typing to stop before sending. The Control Panel's text fields save
// themselves; sending on every keystroke would put a half-typed team name on air.
public sealed class Debouncer(int milliseconds = 450)
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(milliseconds) };
    private Action? _pending;

    public void Run(Action action)
    {
        _pending = action;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer.Tick += OnTick;
        _timer.Start();
    }

    // Send now rather than waiting out the delay: used when a field loses focus.
    public void Flush()
    {
        if (_pending is null) return;
        _timer.Stop();
        var action = _pending;
        _pending = null;
        action();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        var action = _pending;
        _pending = null;
        action?.Invoke();
    }
}
