using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace RovOverlay.Desktop.Services;

public sealed record Toast(string Message, bool IsError);

// Short messages in the corner of the window. Safe to call from any thread.
public static class Toasts
{
    private const int MaxVisible = 4;

    public static ObservableCollection<Toast> Items { get; } = new();

    public static void Info(string message) => Show(message, false);
    public static void Error(string message) => Show(message, true);

    private static void Show(string message, bool isError)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) return;
        if (!dispatcher.CheckAccess())
        {
            dispatcher.InvokeAsync(() => Show(message, isError));
            return;
        }

        var toast = new Toast(message, isError);
        Items.Add(toast);
        while (Items.Count > MaxVisible) Items.RemoveAt(0);

        // Errors stay longer: they usually need reading, not just noticing.
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(isError ? 6 : 3) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Items.Remove(toast);
        };
        timer.Start();
    }
}
