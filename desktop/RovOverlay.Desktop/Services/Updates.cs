using System.Windows;
using RovOverlay.Desktop.Core;
using Velopack;
using Velopack.Sources;

namespace RovOverlay.Desktop.Services;

// Auto-update, sitting in front of Velopack.
//
// One rule shapes this whole file: the operator may be live on air. So the app never
// restarts itself, never steals focus and never opens a window of its own. A new
// version is found and downloaded quietly, the operator is told it is ready, and it is
// applied when they close the app anyway, on their own terms.
//
// Only an installed copy can update itself. A portable copy, or a developer running
// from the repo, has no install for Velopack to replace; that is not an error, and the
// Settings screen says so plainly instead of pretending to check.
public sealed class UpdateService : ObservableObject, IDisposable
{
    public const string RepoUrl = "https://github.com/LazyAF-zZzZ/rov_overlay_v3";

    // A broadcast day is long. Six hours is often enough to catch a patch, rare enough
    // to be invisible.
    private static readonly TimeSpan Every = TimeSpan.FromHours(6);

    private readonly AppSettings _settings;
    private readonly CancellationTokenSource _stop = new();

    private UpdateManager? _applying;
    private UpdateInfo? _ready;
    private string _status = "";
    private bool _isBusy;
    private int _percent;

    public UpdateService(AppSettings settings)
    {
        _settings = settings;
        IsSupported = Manager() is not null;
        Status = Loc.T(IsSupported ? "Update.Idle" : "Update.NotInstalled");
        Loc.Instance.Changed += () =>
        {
            // The status line is whole sentences, so it has to be rebuilt, not re-looked-up.
            if (!IsBusy && _ready is null) Status = Loc.T(IsSupported ? "Update.Idle" : "Update.NotInstalled");
        };
    }

    // False for a portable copy or a build from the repo: there is nothing to replace.
    public bool IsSupported { get; }

    public bool IsReady => _ready is not null;
    public string? ReadyVersion => _ready?.TargetFullRelease.Version.ToString();

    // The release notes ride inside the downloaded package, so what the operator reads is
    // exactly what was written for that version - offline, and with nothing fetched.
    public string? ReleaseNotes => _ready?.TargetFullRelease.NotesMarkdown;

    // Raised once, when a version has finished downloading. The shell decides when to put
    // it in front of anyone: never in the middle of a broadcast.
    public event Action? Ready;
    public string Status { get => _status; private set => Set(ref _status, value); }
    public int Percent { get => _percent; private set => Set(ref _percent, value); }

    public bool IsBusy
    {
        get => _isBusy;
        private set { if (Set(ref _isBusy, value)) OnPropertyChanged(nameof(ShowProgress)); }
    }

    public bool ShowProgress => IsBusy && Percent > 0;

    // The channel is read fresh on every check, so switching it in Settings takes effect
    // at the next check instead of at the next restart.
    private UpdateManager? Manager()
    {
        try
        {
            var beta = _settings.UpdateChannel == "beta";
            var source = new GithubSource(RepoUrl, null, beta);
            var manager = new UpdateManager(source, new UpdateOptions { ExplicitChannel = beta ? "beta" : null });
            return manager.IsInstalled ? manager : null;
        }
        catch
        {
            // No install, no feed, no network stack: all of them mean "cannot update",
            // which is not worth interrupting a broadcast for.
            return null;
        }
    }

    public void Start()
    {
        if (IsSupported) _ = LoopAsync();
    }

    private async Task LoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            await CheckAsync(manual: false);
            try
            {
                await Task.Delay(Every, _stop.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    // manual: the operator pressed the button in Settings, so silence would look broken.
    // An automatic check says nothing unless it actually found something.
    public async Task CheckAsync(bool manual)
    {
        if (!IsSupported)
        {
            if (manual) Toast(Loc.T("Update.NotInstalled"), false);
            return;
        }

        if (_ready is not null)
        {
            if (manual) Toast(Loc.F("Update.ReadyToast", ReadyVersion), false);
            return;
        }

        if (IsBusy) return;

        var manager = Manager();
        if (manager is null) return;

        try
        {
            IsBusy = true;
            Percent = 0;
            Status = Loc.T("Update.Checking");

            var found = await manager.CheckForUpdatesAsync();
            if (found is null)
            {
                Status = Loc.F("Update.UpToDate", AppVersion.Text);
                if (manual) Toast(Status, false);
                return;
            }

            var version = found.TargetFullRelease.Version.ToString();
            Status = Loc.F("Update.Downloading", version);

            // Deltas mean this is usually a small download, but it still happens in the
            // background: nothing about the operator's screen changes while it runs.
            await manager.DownloadUpdatesAsync(found, p => Percent = p, _stop.Token);

            _applying = manager;
            _ready = found;
            OnPropertyChanged(nameof(IsReady));
            OnPropertyChanged(nameof(ReadyVersion));
            Status = Loc.F("Update.Ready", version);
            Toast(Loc.F("Update.ReadyToast", version), false);
            Application.Current?.Dispatcher.InvokeAsync(() => Ready?.Invoke());
        }
        catch (OperationCanceledException)
        {
            // The app is closing.
        }
        catch (Exception e)
        {
            Status = Loc.T("Update.Failed");
            if (manual) Toast(e.Message, true);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(ShowProgress));
        }
    }

    // The operator asked for it now rather than at the next close.
    //
    // Velopack is told to wait for this process to end and then start the new one, and the
    // app is closed the ordinary way: OnExit still runs, so the backend is asked to stop
    // and gets to flush the last state change instead of being killed with the job object.
    public void ApplyNow()
    {
        if (_applying is null || _ready is null) return;
        try
        {
            _applying.WaitExitThenApplyUpdates(_ready.TargetFullRelease, silent: true, restart: true);
            Application.Current?.Shutdown();
        }
        catch (Exception e)
        {
            Toast(e.Message, true);
        }
    }

    // Called once, as the app is closing. Velopack waits for this process to go, swaps
    // the install folder and stops. restart: false on purpose: a window reappearing
    // after someone deliberately closed the app is alarming, and they may have closed it
    // to shut the machine down.
    public void ApplyOnExit()
    {
        if (_applying is null || _ready is null) return;
        try
        {
            _applying.WaitExitThenApplyUpdates(_ready.TargetFullRelease, silent: true, restart: false);
        }
        catch
        {
            // The update stays downloaded and applies on a later close.
        }
    }

    // Toasts are an ObservableCollection the window binds to, and these calls arrive on
    // whatever thread the download finished on.
    private static void Toast(string message, bool error) =>
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            if (error) Toasts.Error(message);
            else Toasts.Info(message);
        });

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }
}
