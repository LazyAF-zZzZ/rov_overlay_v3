using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RovOverlay.Desktop.Services;
using RovOverlay.Desktop.ViewModels;
using RovOverlay.Desktop.Views;

namespace RovOverlay.Desktop;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private AppServices? _services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var args = StartupArgs.Parse(e.Args);

        // One operator app per machine: two would fight over the same backend and port.
        // Snapshot runs (a development aid, see StartupArgs) use their own name so they
        // can run beside an open app.
        _singleInstance = new Mutex(true, args.SnapshotPath is null ? "RovOverlayTool3.Desktop" : "RovOverlayTool3.Snapshot", out var first);
        if (!first)
        {
            MessageBox.Show(Loc.T("App.AlreadyOpen"), "ROV Overlay Tool", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // A bug in one screen must not close the app in the middle of a broadcast.
        DispatcherUnhandledException += (_, ev) =>
        {
            Toasts.Error(ev.Exception.Message);
            ev.Handled = true;
        };

        var settings = AppSettings.Load();
        Loc.Instance.Language = args.Language ?? settings.Language;
        Loc.Instance.Changed += () =>
        {
            settings.Language = Loc.Instance.Language;
            settings.Save();
        };

        _services = new AppServices(settings);
        var shell = new ShellViewModel(_services, args.Page);
        var window = new MainWindow { DataContext = shell };
        MainWindow = window;
        window.Show();

        await shell.StartAsync();

        if (args.SnapshotPath is not null)
        {
            await Task.Delay(args.SnapshotDelayMs);
            SaveSnapshot(window, args.SnapshotPath);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Stop the backend we started. The socket loop is cancelled without waiting on it:
        // the process is ending, and waiting on the UI thread for work that posts back to
        // the UI thread is how shutdowns hang.
        if (_services is not null)
        {
            _ = _services.Socket?.DisposeAsync().AsTask();
            _services.Backend.Dispose();
        }
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private static void SaveSnapshot(Window window, string path)
    {
        window.UpdateLayout();
        var content = (FrameworkElement)window.Content;
        var dpi = VisualTreeHelper.GetDpi(window);
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(content.ActualWidth * dpi.DpiScaleX),
            (int)Math.Ceiling(content.ActualHeight * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(content);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var file = File.Create(path);
        encoder.Save(file);
    }
}

// Command-line switches, all optional:
//   --page <Home|Control|Teams|...|Settings>   open on that screen
//   --lang <th|en>                             language for this run only (not saved)
//   --snapshot <file.png>                      render the window to a PNG and exit
//   --snapshot-delay <ms>                      wait before the snapshot (default 2500)
// The snapshot switches exist so a screen can be checked without a person at the
// keyboard; the operator never needs them.
internal sealed record StartupArgs(string? Page, string? Language, string? SnapshotPath, int SnapshotDelayMs)
{
    public static StartupArgs Parse(string[] args)
    {
        string? Value(string name)
        {
            var i = Array.FindIndex(args, a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        var delay = int.TryParse(Value("--snapshot-delay"), out var ms) ? Math.Clamp(ms, 0, 60_000) : 2500;
        var language = Value("--lang") is "th" or "en" ? Value("--lang") : null;
        return new StartupArgs(Value("--page"), language, Value("--snapshot"), delay);
    }
}
