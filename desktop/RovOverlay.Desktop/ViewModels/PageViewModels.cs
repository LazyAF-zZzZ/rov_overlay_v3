using System.Windows.Input;
using RovOverlay.Desktop.Core;
using RovOverlay.Desktop.Services;

namespace RovOverlay.Desktop.ViewModels;

// A screen that has not been rebuilt in the desktop app yet. It points at the v2-style
// HTML page, which is served by the same backend and so works on the same data.
// Each one disappears as its native screen lands; see docs/PLAN.md.
public sealed class LegacyPageViewModel : ObservableObject
{
    public LegacyPageViewModel(AppServices services, string key, string route, string glyph)
    {
        Key = key;
        Glyph = glyph;
        Url = services.Url(route);
        OpenCommand = new RelayCommand(() => Browser.Open(Url));
        CopyCommand = new RelayCommand(() => Clip.Copy(Url));
        Loc.Instance.Changed += () => OnPropertyChanged(nameof(Title));
    }

    public string Key { get; }
    public string Glyph { get; }
    public string Url { get; }
    public string Title => Loc.T("Nav." + Key);
    public ICommand OpenCommand { get; }
    public ICommand CopyCommand { get; }
}

public sealed class ObsSourceRow : ObservableObject
{
    // Same list and order as v2's public/js/lib/obs-sources.js.
    private static readonly (string Name, string Path, string? Size, bool Sfx)[] Sources =
    [
        ("Overlay 1080p", "/overlay", "1920 × 1080", true),
        ("Overlay 1440p", "/overlay-1440", "2560 × 1440", true),
        ("Result", "/result", null, false),
        ("Previous picks & bans", "/overlay-prev", null, false),
        ("Standings", "/overlay-standings", null, false),
        ("Head to head", "/overlay-matchup", null, false),
        ("Team picks & bans", "/overlay-team-drafts", null, false),
        ("Team list", "/overlay-teams", null, false),
        ("Stats board", "/overlay-analytics", null, false)
    ];

    private readonly string? _size;

    private ObsSourceRow(AppServices services, string name, string path, string? size, bool sfx)
    {
        Name = name;
        _size = size;
        Url = services.Url(sfx ? path + "?sfx=1" : path);
        CopyCommand = new RelayCommand(() => Clip.Copy(Url));
        OpenCommand = new RelayCommand(() => Browser.Open(Url));
        Loc.Instance.Changed += () => OnPropertyChanged(nameof(SizeText));
    }

    public static IReadOnlyList<ObsSourceRow> Create(AppServices services) =>
        Sources.Select(s => new ObsSourceRow(services, s.Name, s.Path, s.Size, s.Sfx)).ToList();

    public string Name { get; }
    public string Url { get; }
    public string SizeText => _size ?? Loc.T("Obs.SameSize");
    public ICommand CopyCommand { get; }
    public ICommand OpenCommand { get; }
}

public sealed class SettingsViewModel : ObservableObject
{
    private readonly AppServices _services;
    private string _logText = "";

    public SettingsViewModel(AppServices services)
    {
        _services = services;
        RefreshLogCommand = new RelayCommand(RefreshLog);
        OpenDataFolderCommand = new RelayCommand(() => Browser.OpenFolder(DataDir));
        OpenMediaFolderCommand = new RelayCommand(() => Browser.OpenFolder(MediaDir));
        Loc.Instance.Changed += () =>
        {
            OnPropertyChanged(nameof(Language));
            OnPropertyChanged(nameof(ModeText));
        };
        RefreshLog();
    }

    public string Language
    {
        get => Loc.Instance.Language;
        set => Loc.Instance.Language = value;
    }

    public string DataDir => _services.Backend.Info?.DataDir ?? AppSettings.DataDir;
    public string MediaDir => _services.Backend.Info?.MediaDir ?? AppSettings.MediaDir;
    public string ServerUrl => _services.Backend.BaseUri.ToString().TrimEnd('/');
    public string ModeText => Loc.T(_services.Backend.Attached ? "Settings.ModeAttached" : "Settings.ModeOwned");
    public string NodePath => _services.Backend.NodePath ?? "—";
    public string BackendDir => _services.Backend.BackendDir ?? "—";
    public string Version => AppVersion.Text;

    public string LogText { get => _logText; private set => Set(ref _logText, value); }

    public ICommand RefreshLogCommand { get; }
    public ICommand OpenDataFolderCommand { get; }
    public ICommand OpenMediaFolderCommand { get; }

    private void RefreshLog() => LogText = string.Join(Environment.NewLine, _services.Backend.RecentLog());
}
