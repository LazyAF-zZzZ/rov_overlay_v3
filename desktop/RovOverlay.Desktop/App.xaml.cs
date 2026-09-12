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
    // Raise this when the licence text changes, so it is shown and agreed to again
    // instead of being assumed from an old agreement.
    private const int LicenceVersion = 1;

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

        // The licence, once, before anything of the app opens. A snapshot run is a
        // development aid with nobody at the keyboard, so it is never asked there.
        if (args.SnapshotPath is null && settings.AgreedLicence < LicenceVersion)
        {
            string[] keys = ["Licence.Free", "Licence.May", "Licence.MayNot", "Licence.Keep", "Licence.Assets"];
            if (!Dialogs.Agree(Loc.T("Licence.Title"), keys.Select(Loc.T), Loc.T("Licence.Agree"), Loc.T("Licence.Exit")))
            {
                Shutdown();
                return;
            }
            settings.AgreedLicence = LicenceVersion;
            settings.Save();
        }

        _services = new AppServices(settings);
        var shell = new ShellViewModel(_services, args.Page) { OpenOnStart = args.Open, ThenOnStart = args.Then };
        var window = new MainWindow { DataContext = shell };
        if (args.Size is { } size)
        {
            window.Width = size.Width;
            window.Height = size.Height;
        }
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
            // If a new version was downloaded, this is the moment it goes in: the
            // operator has closed the app themselves, so nothing is interrupted.
            _services.Updates.ApplyOnExit();
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
//   --open <tournament:ID|team:ID>             then open that tournament or team on top
//   --then <bracket|drafts>                    and open that page of the tournament above it
//   --lang <th|en>                             language for this run only (not saved)
//   --snapshot <file.png>                      render the window to a PNG and exit
//   --snapshot-delay <ms>                      wait before the snapshot (default 2500)
//   --size <width>x<height>                    open at this size, to fit a whole screen in one snapshot
// The snapshot switches exist so a screen can be checked without a person at the
// keyboard; the operator never needs them.
internal sealed record StartupArgs(string? Page, string? Open, string? Then, string? Language, string? SnapshotPath, int SnapshotDelayMs, System.Windows.Size? Size)
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
        System.Windows.Size? size = null;
        if (Value("--size")?.Split('x') is [var w, var h]
            && double.TryParse(w, out var width) && double.TryParse(h, out var height))
        {
            size = new System.Windows.Size(Math.Clamp(width, 800, 4000), Math.Clamp(height, 600, 4000));
        }

        return new StartupArgs(Value("--page"), Value("--open"), Value("--then"), language, Value("--snapshot"), delay, size);
    }
}
