using System.Collections.ObjectModel;
using System.Windows.Input;
using RovOverlay.Desktop.Core;
using RovOverlay.Desktop.Models;
using RovOverlay.Desktop.Services;

namespace RovOverlay.Desktop.ViewModels;

// Choice records override ToString() because a closed ComboBox shows the selected
// item's ToString(), not its DisplayMemberPath (docs/PLAN.md §9).
public sealed record StatsScopeChoice(string? Id, string Label)
{
    public override string ToString() => Label;
}

public sealed record StatsFormChip(string Text, bool IsWin);

public sealed record StatsHeroRow(
    string Hero, string ImageUrl, string CountText, string RateText, double Meter,
    string WinText, string? WinTip, bool HasWin, bool WinIsGood);

public sealed record StatsOpponentRow(
    string Name, bool IsLink, ICommand? OpenCommand, string SeriesText, string GamesText, string LastText,
    string LastTournament, string LastScoreText, bool Ahead, bool Behind);

public sealed record StatsPlayerHero(string Hero, string ImageUrl, string Text, string? Tip);
public sealed record StatsPlayerRow(string Name, string GamesText, IReadOnlyList<StatsPlayerHero> Heroes);

// The Statistics tab of a team's page: every number the server keeps about one team.
//
// It loads the first time the tab is opened, not with the page: most visits to a team
// page are to edit the roster, and a big team's stats are the heaviest read the page has.
// After that it follows results and drafts as they change, like every other screen.
public sealed class TeamStatsPanel : ObservableObject
{
    // The rate bar is drawn inside a 58px column; the most-used hero fills it.
    private const double MeterWidth = 58;
    private const int PlayerHeroes = 6;

    private readonly AppServices _services;
    private readonly ShellViewModel _shell;
    private readonly string _teamId;
    private TeamStatsData? _data;
    private List<TeamTournament> _tournaments = [];
    private StatsScopeChoice? _scope;
    private bool _loaded;
    private bool _rebuildingScopes;
    private int _request;

    public TeamStatsPanel(AppServices services, ShellViewModel shell, string teamId)
    {
        _services = services;
        _shell = shell;
        _teamId = teamId;
        RebuildScopes();
        Loc.Instance.Changed += OnLanguageChanged;
    }

    public ObservableCollection<StatsScopeChoice> Scopes { get; } = new();
    public ObservableCollection<StatsFormChip> Form { get; } = new();
    public ObservableCollection<StatsHeroRow> Picks { get; } = new();
    public ObservableCollection<StatsHeroRow> Bans { get; } = new();
    public ObservableCollection<StatsHeroRow> BansAgainst { get; } = new();
    public ObservableCollection<StatsOpponentRow> Opponents { get; } = new();
    public ObservableCollection<StatsPlayerRow> Players { get; } = new();

    public bool IsLoaded => _loaded;

    public StatsScopeChoice? Scope
    {
        get => _scope;
        set
        {
            // Rebuilding the list clears the ComboBox's selection for a moment; that is not
            // the operator choosing "nothing", so it must not fire a load.
            if (!Set(ref _scope, value) || _rebuildingScopes || value is null) return;
            _ = LoadAsync();
        }
    }

    public string SeriesValue { get; private set; } = "0 – 0";
    public string SeriesDetail { get; private set; } = "";
    public string GamesValue { get; private set; } = "0 – 0";
    public string GamesDetail { get; private set; } = "";
    public string TournamentsText { get; private set; } = "";
    public string BlueValue { get; private set; } = "0 – 0";
    public string BlueDetail { get; private set; } = "";
    public string RedValue { get; private set; } = "0 – 0";
    public string RedDetail { get; private set; } = "";
    public string? SidesUnknownText { get; private set; }
    public string BasedText { get; private set; } = "";

    public bool HasForm => Form.Count > 0;
    public bool HasPicks => Picks.Count > 0;
    public bool HasBans => Bans.Count > 0;
    public bool HasBansAgainst => BansAgainst.Count > 0;
    public bool HasOpponents => Opponents.Count > 0;
    public bool HasPlayers => Players.Count > 0;

    // Called by the page whenever its tournament list is (re)loaded.
    public void SetTournaments(IEnumerable<TeamTournament> tournaments)
    {
        _tournaments = tournaments.ToList();
        RebuildScopes();
    }

    public Task EnsureLoadedAsync() => _loaded ? Task.CompletedTask : LoadAsync();

    public async Task LoadAsync()
    {
        var ticket = ++_request;
        var path = $"/api/teams/{Uri.EscapeDataString(_teamId)}/stats";
        if (_scope?.Id is { } tournamentId) path += $"?tournamentId={Uri.EscapeDataString(tournamentId)}";

        TeamStatsData data;
        try
        {
            data = (await _services.Api.GetAsync<TeamStatsReply>(path)).Stats;
        }
        catch (Exception error)
        {
            if (ticket == _request) Toasts.Error(error.Message);
            return;
        }

        // A slower reply for a filter the operator has already changed must not win.
        if (ticket != _request) return;
        _data = data;
        _loaded = true;
        Build();
    }

    private void RebuildScopes()
    {
        var selected = _scope?.Id;
        _rebuildingScopes = true;
        Scopes.Clear();
        Scopes.Add(new StatsScopeChoice(null, Loc.T("TStats.AllTournaments")));
        foreach (var t in _tournaments) Scopes.Add(new StatsScopeChoice(t.Id, t.Name));
        _scope = Scopes.FirstOrDefault(s => s.Id == selected) ?? Scopes[0];
        _rebuildingScopes = false;
        OnPropertyChanged(nameof(Scope));
    }

    private void Build()
    {
        if (_data is not { } d) return;

        var r = d.Record;
        SeriesValue = $"{r.SeriesWon} – {r.SeriesLost}";
        SeriesDetail = Detail(r.SeriesWon, r.SeriesWon + r.SeriesLost);
        GamesValue = $"{r.GamesWon} – {r.GamesLost}";
        GamesDetail = Detail(r.GamesWon, r.GamesWon + r.GamesLost);
        TournamentsText = r.Tournaments == 1 ? Loc.T("TStats.TournamentsOne") : Loc.F("TStats.TournamentsCount", r.Tournaments);

        BlueValue = $"{d.Sides.Blue.Won} – {d.Sides.Blue.Played - d.Sides.Blue.Won}";
        BlueDetail = Detail(d.Sides.Blue.Won, d.Sides.Blue.Played);
        RedValue = $"{d.Sides.Red.Won} – {d.Sides.Red.Played - d.Sides.Red.Won}";
        RedDetail = Detail(d.Sides.Red.Won, d.Sides.Red.Played);
        SidesUnknownText = d.Sides.Unknown > 0 ? Loc.F("TStats.SidesUnknown", d.Sides.Unknown) : null;
        BasedText = Loc.F("TStats.Based", d.DraftedGames, d.DecidedGames);

        Form.Clear();
        foreach (var outcome in r.Form ?? [])
            Form.Add(new StatsFormChip(Loc.T(outcome == "win" ? "TStats.W" : "TStats.L"), outcome == "win"));

        Fill(Picks, (d.Picks ?? []).Select(p => (p.Hero, p.Picked, (int?)p.Wins, p.Decided)), d.DraftedGames);
        Fill(Bans, (d.Bans ?? []).Select(b => (b.Hero, b.Banned, (int?)null, 0)), d.DraftedGames);
        Fill(BansAgainst, (d.BansAgainst ?? []).Select(b => (b.Hero, b.Banned, (int?)null, 0)), d.DraftedGames);

        Opponents.Clear();
        foreach (var o in d.Opponents ?? [])
        {
            var id = o.OpponentId;
            Opponents.Add(new StatsOpponentRow(
                o.Name,
                id is not null,
                id is null ? null : new RelayCommand(() => _shell.Open(new TeamProfileViewModel(_services, _shell, id))),
                $"{o.SeriesWon} – {o.SeriesLost}",
                $"{o.GamesWon} – {o.GamesLost}",
                Loc.F("TStats.LastText", o.LastTournament, o.LastScore, o.LastOpponentScore),
                o.LastTournament,
                $"{o.LastScore} – {o.LastOpponentScore}",
                o.SeriesWon > o.SeriesLost,
                o.SeriesLost > o.SeriesWon));
        }

        Players.Clear();
        foreach (var p in d.Players ?? [])
        {
            var heroes = (p.Heroes ?? []).Take(PlayerHeroes).Select(h => new StatsPlayerHero(
                h.Hero,
                Icon(h.Hero),
                Loc.F("TStats.PlayerHero", h.Picked, h.Decided > 0 ? Percent((double)h.Wins / h.Decided) : "—"),
                h.Decided > 0 ? Loc.F("TStats.WinTip", h.Wins, h.Decided) : Loc.T("TStats.NoWinner"))).ToList();
            Players.Add(new StatsPlayerRow(p.Name, Loc.F("TStats.PlayerGames", p.Games), heroes));
        }

        // Every text and flag above changed at once: one notification refreshes them all.
        OnPropertyChanged(string.Empty);
    }

    // Rate = games with the hero / drafted games, the same rule as the Analytics screen.
    private void Fill(ObservableCollection<StatsHeroRow> rows, IEnumerable<(string Hero, int Count, int? Wins, int Decided)> source, int drafted)
    {
        rows.Clear();
        var list = source.ToList();
        var max = list.Count == 0 ? 1 : Math.Max(1, list.Max(x => x.Count));
        foreach (var (hero, count, wins, decided) in list)
        {
            var hasWin = wins is not null && decided > 0;
            var winRate = hasWin ? (double)wins!.Value / decided : 0;
            rows.Add(new StatsHeroRow(
                hero,
                Icon(hero),
                count.ToString(),
                Percent(drafted > 0 ? (double)count / drafted : 0),
                MeterWidth * count / max,
                wins is null ? "" : hasWin ? Percent(winRate) : "—",
                wins is null ? null : hasWin ? Loc.F("TStats.WinTip", wins.Value, decided) : Loc.T("TStats.NoWinner"),
                hasWin,
                winRate >= 0.5));
        }
    }

    private static string Detail(int won, int played) =>
        played == 0 ? Loc.T("TStats.NoResults") : Loc.F("TStats.WinRate", Percent((double)won / played));

    private static string Percent(double value) => $"{value * 100:0.0}%";

    private string Icon(string hero) => _services.Url($"/images/heroes-icons/{Uri.EscapeDataString(hero)}.png");

    private void OnLanguageChanged()
    {
        RebuildScopes();
        Build();
    }

    public void Detach() => Loc.Instance.Changed -= OnLanguageChanged;
}
