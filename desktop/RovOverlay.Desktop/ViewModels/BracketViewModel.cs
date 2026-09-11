using System.Collections.ObjectModel;
using System.Windows.Input;
using RovOverlay.Desktop.Core;
using RovOverlay.Desktop.Models;
using RovOverlay.Desktop.Services;

namespace RovOverlay.Desktop.ViewModels;

// One match on the bracket. The score boxes are editable whenever both teams are
// known, because that is how a result is recorded: type it, and it is saved.
public sealed class MatchCard : ObservableObject
{
    private readonly BracketViewModel _owner;
    private readonly Debouncer _save = new(500);
    private string _scoreA = "0";
    private string _scoreB = "0";
    private bool _isLive;

    public MatchCard(BracketViewModel owner, BracketMatch match, int number, double x, double y)
    {
        _owner = owner;
        Id = match.Id;
        Number = number;
        X = x;
        Y = y;
        OpenCommand = new AsyncRelayCommand(() => owner.PutOnAirAsync(this));
        Apply(match);
    }

    public string Id { get; }
    public int Number { get; }
    public double X { get; }
    public double Y { get; }
    public ICommand OpenCommand { get; }

    public string TeamAName { get; private set; } = "";
    public string TeamBName { get; private set; } = "";
    public string SeedA { get; private set; } = "";
    public string SeedB { get; private set; } = "";
    public bool AWon { get; private set; }
    public bool BWon { get; private set; }
    public bool ATbd { get; private set; }
    public bool BTbd { get; private set; }
    public bool Playable { get; private set; }
    public bool IsBye { get; private set; }
    public bool IsComplete { get; private set; }

    public bool IsLive { get => _isLive; set => Set(ref _isLive, value); }

    public string ScoreA
    {
        get => _scoreA;
        set
        {
            if (Set(ref _scoreA, value ?? "0")) SaveSoon();
        }
    }

    public string ScoreB
    {
        get => _scoreB;
        set
        {
            if (Set(ref _scoreB, value ?? "0")) SaveSoon();
        }
    }

    private void SaveSoon()
    {
        if (!Playable) return;
        _save.Run(() => _ = _owner.RecordResultAsync(this));
    }

    public int ScoreAValue => int.TryParse(ScoreA.Trim(), out var n) ? Math.Max(0, n) : 0;
    public int ScoreBValue => int.TryParse(ScoreB.Trim(), out var n) ? Math.Max(0, n) : 0;

    public void Apply(BracketMatch match)
    {
        var teamA = _owner.TeamOf(match.TeamAId);
        var teamB = _owner.TeamOf(match.TeamBId);

        TeamAName = teamA?.Name ?? (match.IsBye ? Loc.T("Bracket.Bye") : Loc.T("Profile.Tbd"));
        TeamBName = teamB?.Name ?? (match.IsBye ? Loc.T("Bracket.Bye") : Loc.T("Profile.Tbd"));
        SeedA = teamA is { Seed: > 0 } ? teamA.Seed.ToString() : "";
        SeedB = teamB is { Seed: > 0 } ? teamB.Seed.ToString() : "";
        ATbd = teamA is null;
        BTbd = teamB is null;
        AWon = match.WinnerId is not null && match.WinnerId == match.TeamAId;
        BWon = match.WinnerId is not null && match.WinnerId == match.TeamBId;
        Playable = !match.IsBye && match.TeamAId is not null && match.TeamBId is not null;
        IsBye = match.IsBye;
        IsComplete = match.Status == "complete";

        if (!_owner.IsEditing(this))
        {
            _scoreA = match.ScoreA.ToString();
            _scoreB = match.ScoreB.ToString();
        }

        foreach (var name in new[]
                 {
                     nameof(TeamAName), nameof(TeamBName), nameof(SeedA), nameof(SeedB), nameof(AWon), nameof(BWon),
                     nameof(ATbd), nameof(BTbd), nameof(Playable), nameof(IsBye), nameof(IsComplete),
                     nameof(ScoreA), nameof(ScoreB)
                 })
            OnPropertyChanged(name);
    }
}

// A connector drawn as an elbow: out of the feeder, across the gap, into the next
// match. A PointCollection because that is what Polyline binds to.
public sealed class BracketLine
{
    public required System.Windows.Media.PointCollection Points { get; init; }
}
public sealed record ColumnTitle(string Text, double X, double Width);

public sealed class BracketSection
{
    public required string Title { get; init; }
    public required bool ShowTitle { get; init; }
    public required double Width { get; init; }
    public required double Height { get; init; }
    public required IReadOnlyList<MatchCard> Cards { get; init; }
    public required IReadOnlyList<BracketLine> Lines { get; init; }
    public required IReadOnlyList<ColumnTitle> Columns { get; init; }
}

// The match session: draw the bracket, record results, put a match on air.
// v2's /tournament/:id/bracket, rebuilt.
public sealed class BracketViewModel : ObservableObject, IClosablePage
{
    private const double CardWidth = 210;
    private const double CardHeight = 56;
    private const double SlotHeight = 78;
    private const double ColumnGap = 58;
    private const double TopPad = 26;

    private readonly AppServices _s;
    private readonly ShellViewModel _shell;
    private readonly string _id;
    private Tournament? _t;
    private List<Team> _roster = [];
    private List<BracketMatch> _matches = [];
    private readonly Dictionary<string, MatchCard> _cards = new();
    private string? _liveMatchId;
    private bool _randomise;
    private bool _notFound;
    private string _errorText = "";

    public BracketViewModel(AppServices services, ShellViewModel shell, string tournamentId)
    {
        _s = services;
        _shell = shell;
        _id = tournamentId;

        BackCommand = new RelayCommand(shell.Back);
        DrawCommand = new AsyncRelayCommand(DrawAsync);
        ClearCommand = new AsyncRelayCommand(ClearAsync);
        OpenDraftsCommand = new RelayCommand(() => shell.Open(new DraftHistoryViewModel(services, shell, tournamentId)));

        services.DataChanged += OnDataChanged;
        Loc.Instance.Changed += OnLanguageChanged;
        _ = LoadAsync();
    }

    public ICommand BackCommand { get; }
    public ICommand DrawCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand OpenDraftsCommand { get; }

    public ObservableCollection<BracketSection> Sections { get; } = new();

    // Set by the view: true while the keyboard is in this card's score box.
    public Func<object, bool> IsEditing { get; set; } = _ => false;

    public string Title => _t?.Name ?? "";
    public bool NotFound { get => _notFound; private set => Set(ref _notFound, value); }
    public string ErrorText { get => _errorText; private set => Set(ref _errorText, value); }
    public bool IsActive => _t?.Status == "active";
    public string StatusText => _t is null ? "" : Loc.T("TStatus." + _t.Status);
    public string SeriesText => _t is null ? "" : Loc.F("Tour.BestOf", _t.BestOf);
    public string TeamsText => _t is null ? "" : Loc.F("Bracket.Teams", _t.TeamCount);

    public string? PlayedText
    {
        get
        {
            var total = _matches.Count(m => !m.IsBye);
            if (total == 0) return null;
            return Loc.F("Tour.Played", _matches.Count(m => !m.IsBye && m.Status == "complete"), total);
        }
    }

    public bool AllPlayed => _matches.Count(m => !m.IsBye) > 0 &&
                             _matches.Count(m => !m.IsBye && m.Status == "complete") == _matches.Count(m => !m.IsBye);

    public bool HasMatches => _matches.Count > 0;
    public bool IsEmpty => _matches.Count == 0;
    public string EmptyText => _t is not null && _t.TeamCount < 2 ? Loc.T("Bracket.NeedTeams") : Loc.T("Bracket.NothingDrawn");
    public string DrawLabel => Loc.T(HasMatches ? "Bracket.DrawAgain" : "Bracket.Draw");
    public string DrawTip => Loc.T(HasMatches ? "Bracket.DrawAgainTip" : "Bracket.DrawTip");
    public bool Randomise { get => _randomise; set => Set(ref _randomise, value); }

    internal Team? TeamOf(string? teamId) => teamId is null ? null : _roster.FirstOrDefault(t => t.Id == teamId);

    private static string E(string value) => Uri.EscapeDataString(value);

    private async Task LoadAsync()
    {
        try
        {
            var detail = await _s.Api.GetAsync<TournamentDetail>($"/api/tournaments/{E(_id)}");
            _t = detail.Tournament;
            _roster = detail.Teams ?? [];
        }
        catch (Exception error)
        {
            NotFound = true;
            ErrorText = error.Message;
            return;
        }

        try
        {
            _liveMatchId = (await _s.Api.GetAsync<LiveResponse>("/api/live-match")).Live.MatchId;
        }
        catch
        {
            _liveMatchId = null;
        }

        await LoadMatchesAsync();
    }

    private async Task LoadMatchesAsync()
    {
        try
        {
            Apply((await _s.Api.GetAsync<BracketMatchList>($"/api/tournaments/{E(_id)}/matches")).Matches ?? []);
        }
        catch (ApiException error) when (error.Status == 404)
        {
            NotFound = true;
            ErrorText = Loc.T("Tour.Gone");
        }
        catch (Exception error)
        {
            Toasts.Error(error.Message);
        }
    }

    private void Apply(List<BracketMatch> matches)
    {
        _matches = matches;
        Build();
        foreach (var name in new[]
                 {
                     nameof(Title), nameof(IsActive), nameof(StatusText), nameof(SeriesText), nameof(TeamsText),
                     nameof(PlayedText), nameof(AllPlayed), nameof(HasMatches), nameof(IsEmpty), nameof(EmptyText),
                     nameof(DrawLabel), nameof(DrawTip)
                 })
            OnPropertyChanged(name);
    }

    // Lays the bracket out by hand: each round is a column, and a match sits halfway
    // between the two it takes its teams from. Knockout rounds therefore fan out like
    // the paper bracket everyone reads; groups and round robin stack in plain columns.
    private void Build()
    {
        Sections.Clear();
        if (_matches.Count == 0) return;

        var elimination = _t?.Format is "single_elim" or "double_elim";
        var byBracket = _matches.GroupBy(m => m.Bracket)
            .OrderBy(g => SectionRank(g.Key))
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .ToList();

        // Match numbers run in reading order across the whole tournament, as in v2.
        var numbers = new Dictionary<string, int>();
        var counter = 1;
        foreach (var group in byBracket)
        {
            foreach (var match in group.OrderBy(m => m.Round).ThenBy(m => m.Slot)) numbers[match.Id] = counter++;
        }

        foreach (var group in byBracket)
        {
            var rounds = group.GroupBy(m => m.Round).OrderBy(g => g.Key).ToList();
            var cards = new List<MatchCard>();
            var lines = new List<BracketLine>();
            var columns = new List<ColumnTitle>();
            var positions = new Dictionary<string, double>();   // match id -> centre Y
            var totalRounds = rounds.Count;
            var firstCount = rounds.Count > 0 ? rounds[0].Count() : 0;
            var height = Math.Max(firstCount, 1) * SlotHeight + TopPad + 20;

            for (var column = 0; column < rounds.Count; column++)
            {
                var round = rounds[column].OrderBy(m => m.Slot).ToList();
                var x = column * (CardWidth + ColumnGap);
                columns.Add(new ColumnTitle(RoundTitle(rounds[column].Key, totalRounds, group.Key, elimination, column + 1), x, CardWidth));

                var previous = column > 0 ? rounds[column - 1].OrderBy(m => m.Slot).ToList() : [];
                var merges = elimination && previous.Count == round.Count * 2;

                for (var i = 0; i < round.Count; i++)
                {
                    var match = round[i];
                    double centre;
                    if (merges)
                    {
                        // Halfway between the two matches that feed it.
                        var a = positions.GetValueOrDefault(previous[i * 2].Id, TopPad + i * SlotHeight);
                        var b = positions.GetValueOrDefault(previous[i * 2 + 1].Id, a + SlotHeight);
                        centre = (a + b) / 2;
                        lines.Add(Connector(x - ColumnGap, a, x, centre));
                        lines.Add(Connector(x - ColumnGap, b, x, centre));
                    }
                    else
                    {
                        centre = TopPad + i * SlotHeight + CardHeight / 2;
                    }

                    positions[match.Id] = centre;
                    height = Math.Max(height, centre + CardHeight);

                    var card = _cards.TryGetValue(match.Id, out var existing) && existing.X == x && Math.Abs(existing.Y - (centre - CardHeight / 2)) < 0.5
                        ? existing
                        : new MatchCard(this, match, numbers[match.Id], x, centre - CardHeight / 2);
                    card.Apply(match);
                    card.IsLive = _liveMatchId == match.Id;
                    _cards[match.Id] = card;
                    cards.Add(card);
                }
            }

            Sections.Add(new BracketSection
            {
                Title = SectionTitle(group.Key),
                ShowTitle = byBracket.Count > 1 || group.Key != "main",
                Width = rounds.Count * (CardWidth + ColumnGap),
                Height = height + 16,
                Cards = cards,
                Lines = lines,
                Columns = columns
            });
        }

        // Cards for matches that no longer exist (a redraw) must not linger.
        foreach (var id in _cards.Keys.Where(id => _matches.All(m => m.Id != id)).ToList()) _cards.Remove(id);
    }

    private static BracketLine Connector(double fromX, double fromY, double toX, double toY)
    {
        var mid = (fromX + toX) / 2;
        return new BracketLine
        {
            Points = new System.Windows.Media.PointCollection
            {
                new(fromX, fromY), new(mid, fromY), new(mid, toY), new(toX, toY)
            }
        };
    }

    private static int SectionRank(string bracket) => bracket switch
    {
        "main" => 0,
        "losers" => 2,
        "grand" => 3,
        _ => 1
    };

    private static string SectionTitle(string bracket) => bracket switch
    {
        "main" => Loc.T("Bracket.Winners"),
        "losers" => Loc.T("Bracket.Losers"),
        "grand" => Loc.T("Round.Grand"),
        "playoff" => Loc.T("Bracket.Playoff"),
        _ => Loc.F("Tour.Group", bracket)
    };

    private static string RoundTitle(int round, int totalRounds, string bracket, bool elimination, int column)
    {
        if (bracket == "grand") return Loc.T(round == 1 ? "Round.Grand" : "Bracket.Reset");
        if (bracket != "main" || !elimination) return Loc.F("Round.Main", column);
        return (totalRounds - round) switch
        {
            0 => Loc.T("Bracket.Final"),
            1 => Loc.T("Bracket.Semis"),
            2 => Loc.T("Bracket.Quarters"),
            _ => Loc.F("Round.Main", round)
        };
    }

    // ---- actions ----------------------------------------------------------

    internal async Task PutOnAirAsync(MatchCard card)
    {
        var reply = await _s.Api.PostAsync<GoLiveReply>($"/api/matches/{E(card.Id)}/live", null);
        Toasts.Info(Loc.F("Bracket.OnAir", reply.Live.GameNo ?? 1));
        _liveMatchId = card.Id;
        foreach (var other in _cards.Values) other.IsLive = other.Id == card.Id;
    }

    internal async Task RecordResultAsync(MatchCard card)
    {
        try
        {
            var reply = await _s.Api.PutAsync<MatchResultReply>($"/api/matches/{E(card.Id)}/result",
                new { scoreA = card.ScoreAValue, scoreB = card.ScoreBValue });
            Apply(reply.Matches ?? _matches);
        }
        catch (Exception error)
        {
            Toasts.Error(error.Message);
            await LoadMatchesAsync();
        }
    }

    private async Task DrawAsync()
    {
        if (HasMatches)
        {
            var ok = Dialogs.Confirm(Loc.T("Bracket.DrawAgain"), [Loc.T("Bracket.DrawAgainBody")], Loc.T("Bracket.DrawAgain"), danger: true);
            if (!ok) return;
        }

        var reply = await _s.Api.PostAsync<BracketMatchList>($"/api/tournaments/{E(_id)}/matches", new { randomise = Randomise });
        Apply(reply.Matches ?? []);
        Toasts.Info(Loc.T(Randomise ? "Bracket.DrawnRandom" : "Bracket.DrawnSeed"));
    }

    private async Task ClearAsync()
    {
        if (!Dialogs.Confirm(Loc.T("Bracket.ClearTitle"), [Loc.T("Bracket.ClearBody")], Loc.T("Common.DeleteForever"), danger: true)) return;
        await _s.Api.DeleteAsync<BracketMatchList>($"/api/tournaments/{E(_id)}/matches");
        Apply([]);
        Toasts.Info(Loc.T("Bracket.Cleared"));
    }

    private void OnDataChanged(DataChange change)
    {
        var mine = change.TournamentId is null || change.TournamentId == _id;
        if (change.Topic == "live")
        {
            _ = RefreshLiveAsync();
            return;
        }
        if (!mine && change.Topic != "teams") return;
        if (change.Topic is "matches" or "roster" or "teams") _ = ReloadAsync();
    }

    private async Task RefreshLiveAsync()
    {
        try
        {
            _liveMatchId = (await _s.Api.GetAsync<LiveResponse>("/api/live-match")).Live.MatchId;
            foreach (var card in _cards.Values) card.IsLive = card.Id == _liveMatchId;
        }
        catch
        {
            // The highlight is a nicety; leave it as it was.
        }
    }

    private async Task ReloadAsync()
    {
        try
        {
            var detail = await _s.Api.GetAsync<TournamentDetail>($"/api/tournaments/{E(_id)}");
            _t = detail.Tournament;
            _roster = detail.Teams ?? [];
        }
        catch (ApiException error) when (error.Status == 404)
        {
            NotFound = true;
            ErrorText = Loc.T("Tour.Gone");
            return;
        }
        catch
        {
            return;
        }
        await LoadMatchesAsync();
    }

    private void OnLanguageChanged() => Apply(_matches);

    public void OnClosed()
    {
        _s.DataChanged -= OnDataChanged;
        Loc.Instance.Changed -= OnLanguageChanged;
    }
}
