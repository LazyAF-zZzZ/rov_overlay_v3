using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Input;
using RovOverlay.Desktop.Core;
using RovOverlay.Desktop.Models;
using RovOverlay.Desktop.Services;

namespace RovOverlay.Desktop.ViewModels;

public sealed class NavItem : ObservableObject
{
    private readonly ShellViewModel _shell;
    private readonly Func<object> _create;
    private object? _page;
    private bool _isSelected;

    public NavItem(ShellViewModel shell, string key, string glyph, Func<object> create)
    {
        _shell = shell;
        _create = create;
        Key = key;
        Glyph = glyph;
        Loc.Instance.Changed += () => OnPropertyChanged(nameof(Title));
    }

    public string Key { get; }
    public string Glyph { get; }
    public string Title => Loc.T("Nav." + Key);

    // Pages are created once and kept, so switching back is instant and keeps what the
    // operator was in the middle of.
    public object Page => _page ??= _create();

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (Set(ref _isSelected, value) && value) _shell.Navigate(this);
        }
    }

    internal void Deselect()
    {
        _isSelected = false;
        OnPropertyChanged(nameof(IsSelected));
    }
}

public sealed class ShellViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly string _startPage;
    private object? _currentPage;
    private bool _isStarting;
    private string? _startupError;
    private bool _isConnected;
    private bool _hasState;
    private string _blueName = "BLUE";
    private string _redName = "RED";
    private int _blueScore;
    private int _redScore;
    private bool _bannerOnAir;
    private string _overlaySize = "1080";
    private LiveInfo? _live;
    private bool _isObsOpen;

    public ShellViewModel(AppServices services, string? startPage)
    {
        _services = services;
        _startPage = startPage ?? "Home";

        NavItems =
        [
            new NavItem(this, "Home", "\uE80F", () => new HomeViewModel(services)),
            Legacy("Control", "/control", "\uE7FC"),
            Legacy("Teams", "/teams", "\uE716"),
            Legacy("Analytics", "/analytics", "\uE9D2"),
            Legacy("Design", "/design", "\uE790"),
            Legacy("Hotkeys", "/hotkeys", "\uE765"),
            Legacy("Guide", "/guide", "\uE897")
        ];
        SettingsItem = new NavItem(this, "Settings", "\uE713", () => new SettingsViewModel(services));
        ObsSources = ObsSourceRow.Create(services);

        RetryCommand = new AsyncRelayCommand(StartAsync);
        ToggleLanguageCommand = new RelayCommand(() => Loc.Instance.Language = Loc.Instance.Language == "th" ? "en" : "th");
        ToggleObsCommand = new RelayCommand(() => IsObsOpen = !IsObsOpen);

        services.StateUpdated += ApplyState;
        services.ConnectionChanged += connected =>
        {
            IsConnected = connected;
            if (connected) _ = RefreshLiveAsync();
        };
        services.DataChanged += change =>
        {
            if (change.Topic == "live") _ = RefreshLiveAsync();
        };
        services.Backend.Exited += () => Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            IsConnected = false;
            StartupError = Loc.T("Error.Exited");
        });

        Loc.Instance.Changed += () =>
        {
            OnPropertyChanged(nameof(ConnectionText));
            OnPropertyChanged(nameof(BannerText));
            OnPropertyChanged(nameof(BannerTip));
            OnPropertyChanged(nameof(LiveMatchText));
            OnPropertyChanged(nameof(LanguageCode));
        };

        NavItem Legacy(string key, string route, string glyph) =>
            new(this, key, glyph, () => new LegacyPageViewModel(services, key, route, glyph));
    }

    public IReadOnlyList<NavItem> NavItems { get; }
    public NavItem SettingsItem { get; }
    public IReadOnlyList<ObsSourceRow> ObsSources { get; }
    public ObservableCollection<Toast> ToastItems => Toasts.Items;

    public ICommand RetryCommand { get; }
    public ICommand ToggleLanguageCommand { get; }
    public ICommand ToggleObsCommand { get; }

    public object? CurrentPage
    {
        get => _currentPage;
        private set => Set(ref _currentPage, value);
    }

    public bool IsStarting
    {
        get => _isStarting;
        private set
        {
            if (!Set(ref _isStarting, value)) return;
            OnPropertyChanged(nameof(ShowOverlay));
            OnPropertyChanged(nameof(ConnectionText));
        }
    }

    public string? StartupError
    {
        get => _startupError;
        private set
        {
            if (!Set(ref _startupError, value)) return;
            OnPropertyChanged(nameof(ShowOverlay));
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => StartupError is not null;
    public bool ShowOverlay => IsStarting || StartupError is not null;

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (Set(ref _isConnected, value)) OnPropertyChanged(nameof(ConnectionText));
        }
    }

    public string ConnectionText =>
        IsStarting ? Loc.T("Conn.Starting")
        : IsConnected ? Loc.T("Conn.Live")
        : _services.Socket is null ? Loc.T("Conn.Offline")
        : Loc.T("Conn.Connecting");

    public bool HasState { get => _hasState; private set => Set(ref _hasState, value); }
    public string BlueName { get => _blueName; private set => Set(ref _blueName, value); }
    public string RedName { get => _redName; private set => Set(ref _redName, value); }
    public int BlueScore { get => _blueScore; private set => Set(ref _blueScore, value); }
    public int RedScore { get => _redScore; private set => Set(ref _redScore, value); }

    public bool BannerOnAir
    {
        get => _bannerOnAir;
        private set
        {
            if (!Set(ref _bannerOnAir, value)) return;
            OnPropertyChanged(nameof(BannerText));
            OnPropertyChanged(nameof(BannerTip));
        }
    }

    public string BannerText => Loc.T(BannerOnAir ? "Banner.On" : "Banner.Off");
    public string BannerTip => Loc.T(BannerOnAir ? "Banner.OnTip" : "Banner.OffTip");

    public string OverlaySizeText => $"{_overlaySize}p";

    public string LiveMatchText
    {
        get
        {
            if (_live?.MatchId is null) return Loc.T("Live.Quick");
            var tournament = _live.TournamentName ?? "";
            var match = _live.MatchLabel ?? "";
            return _live.GameNo is int game
                ? Loc.F("Live.Match", tournament, match, game)
                : Loc.F("Live.MatchNoGame", tournament, match);
        }
    }

    public bool IsObsOpen { get => _isObsOpen; set => Set(ref _isObsOpen, value); }
    public string LanguageCode => Loc.Instance.Language == "th" ? "TH" : "EN";
    public string ServerUrl => _services.Backend.BaseUri.ToString().TrimEnd('/');
    public string VersionText => "v" + AppVersion.Text;

    public async Task StartAsync()
    {
        StartupError = null;
        IsStarting = true;
        try
        {
            await _services.Backend.StartAsync();
        }
        catch (Exception error)
        {
            StartupError = error.Message;
            return;
        }
        finally
        {
            IsStarting = false;
        }

        _services.Connect();
        OnPropertyChanged(nameof(ConnectionText));
        if (CurrentPage is null) NavigateTo(_startPage);
    }

    public void NavigateTo(string key)
    {
        var item = AllItems().FirstOrDefault(i => i.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) ?? NavItems[0];
        item.IsSelected = true;
    }

    internal void Navigate(NavItem target)
    {
        foreach (var item in AllItems())
        {
            if (!ReferenceEquals(item, target) && item.IsSelected) item.Deselect();
        }
        CurrentPage = target.Page;
    }

    private IEnumerable<NavItem> AllItems() => NavItems.Append(SettingsItem);

    private void ApplyState(JsonNode state)
    {
        HasState = true;
        BlueName = J.Str(state["teamBlue"]?["name"]) is { Length: > 0 } blue ? blue : "BLUE";
        RedName = J.Str(state["teamRed"]?["name"]) is { Length: > 0 } red ? red : "RED";
        BlueScore = J.Int(state["teamBlue"]?["score"]);
        RedScore = J.Int(state["teamRed"]?["score"]);
        BannerOnAir = J.Bool(state["overlayVisible"]) != false;

        var size = J.Str(state["overlaySize"]) is { Length: > 0 } s ? s : "1080";
        if (size != _overlaySize)
        {
            _overlaySize = size;
            OnPropertyChanged(nameof(OverlaySizeText));
        }
    }

    private async Task RefreshLiveAsync()
    {
        try
        {
            _live = (await _services.Api.GetAsync<LiveResponse>("/api/live-match")).Live;
            OnPropertyChanged(nameof(LiveMatchText));
        }
        catch
        {
            // The title strip keeps its last value; the connection light already says why.
        }
    }
}
