using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using System.Windows.Input;
using RovOverlay.Desktop.Core;
using RovOverlay.Desktop.Models;
using RovOverlay.Desktop.Services;

namespace RovOverlay.Desktop.ViewModels;

public sealed class HeroStatRow
{
    private const double MeterWidth = 120;

    // The meter is full at 40% presence, as in v2: almost nothing reaches higher, and a
    // full-scale bar would leave every real hero as a stub.
    public HeroStatRow(HeroStat stat, string imageUrl)
    {
        Hero = stat.Hero;
        ImageUrl = imageUrl;
        PresenceText = Percent(stat.Presence);
        PickText = Percent(stat.PickRate);
        BanText = Percent(stat.BanRate);
        Meter = Math.Min(1, stat.Presence / 0.4) * MeterWidth;

        WinText = stat.WinRate is { } rate ? Percent(rate) : "—";
        HasWinRate = stat.WinRate is not null;
        WinIsGood = stat.WinRate >= 0.5;
        WinTip = stat.WinRate is not null
            ? Loc.F("Analytics.WinTip", stat.Wins, stat.Decided)
            : stat.Picked > 0 ? Loc.T("Analytics.NoWinner") : null;

        PriorityText = stat.BanPriority is { } priority ? $"#{priority}" : "—";
        PriorityTip = stat.EarlyBans > 0 ? Loc.F("Analytics.PrioTip", stat.EarlyBans, Percent(stat.EarlyBanRate)) : null;
    }

    public string Hero { get; }
    public string ImageUrl { get; }
    public string PresenceText { get; }
    public string PickText { get; }
    public string BanText { get; }
    public double Meter { get; }
    public string WinText { get; }
    public bool HasWinRate { get; }
    public bool WinIsGood { get; }
    public string? WinTip { get; }
    public string PriorityText { get; }
    public string? PriorityTip { get; }

    private static string Percent(double value) => $"{value * 100:0.0}%";
}

public sealed record LiveHeroChip(string Hero, string ImageUrl, bool IsBlue, bool IsBan);

// Pick and ban statistics across games with a completed draft, filtered by tournament
// and team. v2's /analytics, rebuilt.
public sealed class AnalyticsViewModel : ObservableObject, IClosablePage
{
    private readonly AppServices _s;
    private List<HeroStat> _heroes = [];
    private AnalyticsSummary? _summary;
    private string _search = "";
    private TeamChoice? _tournament;
    private TeamChoice? _team;
    private bool _loaded;
    private bool _hasLive;
    private string _liveTitle = "";
    private string _liveNote = "";

    public AnalyticsViewModel(AppServices services)
    {
        _s = services;
        ResetScopeCommand = new RelayCommand(() =>
        {
            _tournament = Tournaments.FirstOrDefault();
            _team = Teams.FirstOrDefault();
            OnPropertyChanged(nameof(Tournament));
            OnPropertyChanged(nameof(Team));
            _ = LoadAsync();
        });

        services.StateUpdated += OnState;
        services.DataChanged += OnDataChanged;
        Loc.Instance.Changed += OnLanguageChanged;

        _ = StartAsync();
        if (services.LastState is not null) OnState(services.LastState);
    }

    public ICommand ResetScopeCommand { get; }
    public ObservableCollection<HeroStatRow> Rows { get; } = new();
    public ObservableCollection<TeamChoice> Tournaments { get; } = new();
    public ObservableCollection<TeamChoice> Teams { get; } = new();
    public ObservableCollection<LiveHeroChip> LiveHeroes { get; } = new();

    public TeamChoice? Tournament
    {
        get => _tournament;
        set
        {
            if (Set(ref _tournament, value)) _ = LoadAsync();
        }
    }

    public TeamChoice? Team
    {
        get => _team;
        set
        {
            if (Set(ref _team, value)) _ = LoadAsync();
        }
    }

    public string Search
    {
        get => _search;
        set
        {
            if (Set(ref _search, value ?? "")) Build();
        }
    }

    public string GamesText => (_summary?.Games ?? 0) == 1
        ? Loc.T("Analytics.GamesOne")
        : Loc.F("Analytics.Games", _summary?.Games ?? 0);
    public string HeroesSeenText => Loc.F("Analytics.HeroesSeen", _summary?.HeroesSeen ?? 0);
    public string DecidedText => Loc.F("Analytics.Decided", _summary?.DecidedGames ?? 0);
    public bool HasGames => _summary is { Games: > 0 };
    public bool AllDecided => _summary is not null && _summary.Games == _summary.DecidedGames;
    public bool IsEmpty => _loaded && Rows.Count == 0;

    public string EmptyText
    {
        get
        {
            if (!HasGames)
            {
                var scoped = (Tournament?.Id.Length ?? 0) > 0 || (Team?.Id.Length ?? 0) > 0;
                return Loc.T(scoped ? "Analytics.EmptyScope" : "Analytics.Empty");
            }
            return Loc.F("Analytics.NoHero", Search.Trim());
        }
    }

    public bool HasLive { get => _hasLive; private set => Set(ref _hasLive, value); }
    public string LiveTitle { get => _liveTitle; private set => Set(ref _liveTitle, value); }
    public string LiveNote { get => _liveNote; private set => Set(ref _liveNote, value); }

    private async Task StartAsync()
    {
        await LoadScopeAsync();
        await LoadAsync();
    }

    private async Task LoadScopeAsync()
    {
        var keepTournament = Tournament?.Id ?? "";
        var keepTeam = Team?.Id ?? "";

        Tournaments.Clear();
        Tournaments.Add(new TeamChoice("", Loc.T("Analytics.AllTournaments")));
        try
        {
            foreach (var t in (await _s.Api.GetAsync<TournamentList>("/api/tournaments")).Tournaments ?? [])
                Tournaments.Add(new TeamChoice(t.Id, t.Name));
        }
        catch
        {
            // The filter is a convenience; the numbers still load.
        }

        Teams.Clear();
        Teams.Add(new TeamChoice("", Loc.T("Analytics.AllTeams")));
        try
        {
            foreach (var team in (await _s.Api.GetAsync<TeamList>("/api/teams")).Teams ?? [])
                Teams.Add(new TeamChoice(team.Id, team.Name));
        }
        catch
        {
            // Same.
        }

        _tournament = Tournaments.FirstOrDefault(c => c.Id == keepTournament) ?? Tournaments[0];
        _team = Teams.FirstOrDefault(c => c.Id == keepTeam) ?? Teams[0];
        OnPropertyChanged(nameof(Tournament));
        OnPropertyChanged(nameof(Team));
    }

    private async Task LoadAsync()
    {
        var query = new List<string>();
        if (Tournament is { Id.Length: > 0 } tournament) query.Add($"tournamentId={Uri.EscapeDataString(tournament.Id)}");
        if (Team is { Id.Length: > 0 } team) query.Add($"teamId={Uri.EscapeDataString(team.Id)}");
        var url = "/api/analytics" + (query.Count > 0 ? "?" + string.Join("&", query) : "");

        try
        {
            var reply = await _s.Api.GetAsync<AnalyticsReply>(url);
            _heroes = reply.Heroes ?? [];
            _summary = reply.Summary;
        }
        catch (Exception error)
        {
            Toasts.Error(error.Message);
            return;
        }

        _loaded = true;
        Build();
    }

    private void Build()
    {
        var needle = Search.Trim();
        Rows.Clear();
        foreach (var stat in _heroes.Where(h => h.Present > 0)
                     .Where(h => needle.Length == 0 || h.Hero.Contains(needle, StringComparison.CurrentCultureIgnoreCase)))
        {
            Rows.Add(new HeroStatRow(stat, _s.Url($"/images/heroes-icons/{Uri.EscapeDataString(stat.Hero)}.png")));
        }

        foreach (var name in new[]
                 {
                     nameof(GamesText), nameof(HeroesSeenText), nameof(DecidedText), nameof(HasGames),
                     nameof(AllDecided), nameof(IsEmpty), nameof(EmptyText)
                 })
            OnPropertyChanged(name);
    }

    // The draft in progress is shown on its own, never counted: while it is half done
    // the denominator would grow before the numerator and every rate would sag.
    private void OnState(JsonNode node)
    {
        var state = ControlState.From(node);
        var chips = new List<LiveHeroChip>();
        foreach (var (side, isBlue) in new[] { (state.Blue, true), (state.Red, false) })
        {
            foreach (var hero in side.Bans.Where(h => h is not null))
                chips.Add(new LiveHeroChip(hero!, _s.Url($"/images/heroes-icons/{Uri.EscapeDataString(hero!)}.png"), isBlue, true));
            foreach (var hero in side.Picks.Where(h => h is not null))
                chips.Add(new LiveHeroChip(hero!, _s.Url($"/images/heroes-icons/{Uri.EscapeDataString(hero!)}.png"), isBlue, false));
        }

        var complete = state.Blue.Picks.All(h => h is not null) && state.Blue.Bans.All(h => h is not null)
                       && state.Red.Picks.All(h => h is not null) && state.Red.Bans.All(h => h is not null);

        if (chips.Count == 0 || complete)
        {
            HasLive = false;
            LiveHeroes.Clear();
            return;
        }

        LiveHeroes.Clear();
        foreach (var chip in chips) LiveHeroes.Add(chip);
        LiveTitle = $"{state.Blue.Name} vs {state.Red.Name}";
        LiveNote = Loc.F("Analytics.LiveNote", chips.Count(c => !c.IsBan), chips.Count(c => c.IsBan));
        HasLive = true;
    }

    private void OnDataChanged(DataChange change)
    {
        if (change.Topic is "games" or "matches") _ = LoadAsync();
        if (change.Topic is "teams" or "tournaments") _ = LoadScopeAsync();
    }

    private void OnLanguageChanged()
    {
        Build();
        OnPropertyChanged(nameof(LiveNote));
    }

    public void OnClosed()
    {
        _s.StateUpdated -= OnState;
        _s.DataChanged -= OnDataChanged;
        Loc.Instance.Changed -= OnLanguageChanged;
    }
}
