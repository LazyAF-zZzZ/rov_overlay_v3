using System.Collections.ObjectModel;
using System.Windows.Input;
using RovOverlay.Desktop.Core;
using RovOverlay.Desktop.Models;
using RovOverlay.Desktop.Services;

namespace RovOverlay.Desktop.ViewModels;

public sealed class HeroTile(string? hero, string? url, bool highlight)
{
    public string? Hero { get; } = hero;
    public string? Url { get; } = url;
    public bool Highlight { get; } = highlight;
    public bool IsEmpty { get; } = hero is null;
}

public sealed class DraftSideView
{
    public required string Name { get; init; }
    public required bool Won { get; init; }
    public required bool IsBlue { get; init; }
    public required IReadOnlyList<HeroTile> Picks { get; init; }
    public required IReadOnlyList<HeroTile> Bans { get; init; }
}

public sealed class DraftGameView
{
    public required string Where { get; init; }
    public required string Teams { get; init; }
    public required string GameLabel { get; init; }
    public required DraftSideView Blue { get; init; }
    public required DraftSideView Red { get; init; }
}

// Every draft recorded in one tournament: what each side picked and banned, game by
// game. v2's /tournament/:id/drafts, rebuilt.
public sealed class DraftHistoryViewModel : ObservableObject, IClosablePage
{
    private readonly AppServices _s;
    private readonly string _id;
    private List<DraftGame> _games = [];
    private string _tournamentName = "";
    private string _heroFilter = "";
    private TeamChoice? _teamFilter;
    private bool _notFound;
    private string _errorText = "";

    public DraftHistoryViewModel(AppServices services, ShellViewModel shell, string tournamentId)
    {
        _s = services;
        _id = tournamentId;
        BackCommand = new RelayCommand(shell.Back);
        ClearFiltersCommand = new RelayCommand(() =>
        {
            HeroFilter = "";
            TeamFilter = Teams.FirstOrDefault();
        });

        services.DataChanged += OnDataChanged;
        Loc.Instance.Changed += OnLanguageChanged;
        _ = LoadAsync();
    }

    public ICommand BackCommand { get; }
    public ICommand ClearFiltersCommand { get; }
    public ObservableCollection<DraftGameView> Games { get; } = new();
    public ObservableCollection<TeamChoice> Teams { get; } = new();

    public string Title => _tournamentName;
    public bool NotFound { get => _notFound; private set => Set(ref _notFound, value); }
    public string ErrorText { get => _errorText; private set => Set(ref _errorText, value); }
    public bool IsEmpty => Games.Count == 0;

    public string EmptyText => _games.Count == 0 ? Loc.T("Drafts.None") : Loc.T("Drafts.NoMatch");

    public string CountText => Games.Count != _games.Count
        ? Loc.F("Drafts.Shown", Games.Count, _games.Count)
        : _games.Count == 1 ? Loc.T("Drafts.CountOne") : Loc.F("Drafts.Count", _games.Count);

    public string HeroFilter
    {
        get => _heroFilter;
        set
        {
            if (Set(ref _heroFilter, value ?? "")) Build();
        }
    }

    public TeamChoice? TeamFilter
    {
        get => _teamFilter;
        set
        {
            if (Set(ref _teamFilter, value)) Build();
        }
    }

    private static string E(string value) => Uri.EscapeDataString(value);

    private async Task LoadAsync()
    {
        try
        {
            var reply = await _s.Api.GetAsync<DraftsReply>($"/api/tournaments/{E(_id)}/drafts");
            _games = reply.Games ?? [];
            _tournamentName = reply.Tournament.Name;
        }
        catch (Exception error)
        {
            if (_games.Count == 0)
            {
                NotFound = true;
                ErrorText = error.Message;
            }
            else
            {
                Toasts.Error(error.Message);
            }
            return;
        }

        BuildTeamFilter();
        Build();
        OnPropertyChanged(nameof(Title));
    }

    private void BuildTeamFilter()
    {
        var keep = TeamFilter?.Id ?? "";
        var seen = new Dictionary<string, string>();
        foreach (var game in _games)
        {
            foreach (var side in new[] { game.Blue, game.Red })
            {
                if (side.TeamId is { Length: > 0 } id) seen.TryAdd(id, side.Name);
            }
        }

        Teams.Clear();
        Teams.Add(new TeamChoice("", Loc.T("Drafts.AllTeams")));
        foreach (var (id, name) in seen.OrderBy(pair => pair.Value, StringComparer.CurrentCulture))
            Teams.Add(new TeamChoice(id, name));
        _teamFilter = Teams.FirstOrDefault(t => t.Id == keep) ?? Teams[0];
        OnPropertyChanged(nameof(TeamFilter));
    }

    private void Build()
    {
        var needle = HeroFilter.Trim();
        var teamId = TeamFilter?.Id ?? "";

        Games.Clear();
        foreach (var game in _games.Where(g => Matches(g, teamId, needle)))
        {
            Games.Add(new DraftGameView
            {
                Where = Rounds.Label(game.Bracket, game.Round),
                Teams = $"{Name(game.Blue)}  vs  {Name(game.Red)}",
                GameLabel = Loc.F("Drafts.GameN", game.GameNo),
                Blue = Side(game.Blue, true, needle),
                Red = Side(game.Red, false, needle)
            });
        }

        foreach (var name in new[] { nameof(CountText), nameof(IsEmpty), nameof(EmptyText) }) OnPropertyChanged(name);
    }

    private static string Name(DraftSide side) => string.IsNullOrWhiteSpace(side.Name) ? "—" : side.Name;

    private DraftSideView Side(DraftSide side, bool isBlue, string needle) => new()
    {
        Name = Name(side),
        Won = side.Won,
        IsBlue = isBlue,
        Picks = Tiles(side.Picks, ControlState.PickCount, needle),
        Bans = Tiles(side.Bans, ControlState.BanCount, needle)
    };

    private IReadOnlyList<HeroTile> Tiles(List<string?>? heroes, int count, string needle) =>
        Enumerable.Range(0, count).Select(i =>
        {
            var hero = heroes?.ElementAtOrDefault(i);
            if (string.IsNullOrEmpty(hero)) return new HeroTile(null, null, false);
            var hit = needle.Length > 0 && hero.Contains(needle, StringComparison.CurrentCultureIgnoreCase);
            return new HeroTile(hero, _s.Url($"/images/heroes/{Uri.EscapeDataString(hero)}.png"), hit);
        }).ToList();

    private static bool Matches(DraftGame game, string teamId, string needle)
    {
        if (teamId.Length > 0 && game.Blue.TeamId != teamId && game.Red.TeamId != teamId) return false;
        if (needle.Length == 0) return true;
        return new[] { game.Blue, game.Red }.Any(side =>
            (side.Picks ?? []).Concat(side.Bans ?? [])
            .Any(hero => hero is not null && hero.Contains(needle, StringComparison.CurrentCultureIgnoreCase)));
    }

    private void OnDataChanged(DataChange change)
    {
        if (change.Topic is "games" or "matches") _ = LoadAsync();
    }

    private void OnLanguageChanged()
    {
        BuildTeamFilter();
        Build();
    }

    public void OnClosed()
    {
        _s.DataChanged -= OnDataChanged;
        Loc.Instance.Changed -= OnLanguageChanged;
    }
}
