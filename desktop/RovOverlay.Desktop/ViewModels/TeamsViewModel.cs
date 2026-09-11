using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using RovOverlay.Desktop.Core;
using RovOverlay.Desktop.Models;
using RovOverlay.Desktop.Services;

namespace RovOverlay.Desktop.ViewModels;

public sealed class TeamListRow : TeamFace
{
    private readonly TeamsViewModel _owner;
    private bool _isSelected;
    private int _tournaments;
    private int _won;
    private int _lost;

    public TeamListRow(TeamsViewModel owner, AppServices services, Team team, TeamSummary? summary) : base(services)
    {
        _owner = owner;
        ProfileCommand = new RelayCommand(() => owner.OpenProfile(Id));
        DeleteCommand = new AsyncRelayCommand(() => owner.DeleteAsync(this));
        Apply(team, summary);
    }

    public ICommand ProfileCommand { get; }
    public ICommand DeleteCommand { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (Set(ref _isSelected, value)) _owner.SelectionChanged();
        }
    }

    public int Tournaments => _tournaments;
    public string TournamentsText => _tournaments == 1 ? Loc.T("Teams.InOneTournament") : Loc.F("Teams.InTournaments", _tournaments);
    public bool HasTournaments => _tournaments > 0;
    public string RecordText => Loc.F("Teams.Record", _won, _lost);
    public bool HasRecord => _won + _lost > 0;
    public bool IsWinning => _won >= _lost;

    public void Apply(Team team, TeamSummary? summary)
    {
        _tournaments = summary?.Tournaments ?? 0;
        _won = summary?.Won ?? 0;
        _lost = summary?.Lost ?? 0;
        Apply(team);
    }

    public override void RefreshText()
    {
        base.RefreshText();
        foreach (var name in new[] { nameof(Tournaments), nameof(TournamentsText), nameof(HasTournaments), nameof(RecordText), nameof(HasRecord), nameof(IsWinning) })
            OnPropertyChanged(name);
    }
}

// The team registry: every team, shared by every tournament. v2's /teams, rebuilt.
public sealed class TeamsViewModel : ObservableObject
{
    private readonly AppServices _s;
    private readonly ShellViewModel _shell;
    private int _total;
    private bool _loaded;
    private bool _createOpen;
    private string _searchText = "";

    public TeamsViewModel(AppServices services, ShellViewModel shell)
    {
        _s = services;
        _shell = shell;
        View = CollectionViewSource.GetDefaultView(Rows);
        View.Filter = item => item is TeamListRow row && Matches(row);

        ToggleCreateCommand = new RelayCommand(() => CreateOpen = !CreateOpen);
        CloseCreateCommand = new RelayCommand(() => CreateOpen = false);
        ClearFormCommand = new RelayCommand(() =>
        {
            NewTeam.Clear();
            Toasts.Info(Loc.T("Team.FormCleared"));
        });
        CreateCommand = new AsyncRelayCommand(CreateAsync);
        ToggleAllCommand = new RelayCommand(ToggleAll);
        ClearSelectionCommand = new RelayCommand(() =>
        {
            foreach (var row in Rows) row.IsSelected = false;
        });
        DeleteSelectedCommand = new AsyncRelayCommand(DeleteSelectedAsync, () => SelectedCount > 0);
        RefreshCommand = new AsyncRelayCommand(LoadAsync);

        // A roster or a result elsewhere changes the tournament and W-L counts shown here.
        services.DataChanged += change =>
        {
            if (change.Topic is "teams" or "roster" or "matches") _ = LoadAsync();
        };
        Loc.Instance.Changed += () =>
        {
            foreach (var row in Rows) row.RefreshText();
            NotifyCounts();
        };

        _ = LoadAsync();
    }

    public ObservableCollection<TeamListRow> Rows { get; } = new();
    public ICollectionView View { get; }
    public NewTeamForm NewTeam { get; } = new();

    public ICommand ToggleCreateCommand { get; }
    public ICommand CloseCreateCommand { get; }
    public ICommand ClearFormCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand ToggleAllCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public ICommand DeleteSelectedCommand { get; }
    public ICommand RefreshCommand { get; }

    public bool CreateOpen { get => _createOpen; set => Set(ref _createOpen, value); }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!Set(ref _searchText, value ?? "")) return;
            View.Refresh();
            NotifyCounts();
            SelectionChanged();
        }
    }

    public string CountText => Loc.F("Teams.Count", _total);
    public string? ShownText
    {
        get
        {
            var shown = Shown().Count();
            return SearchText.Trim().Length > 0 && shown != _total ? Loc.F("Teams.Shown", shown) : null;
        }
    }

    public bool IsEmpty => _loaded && _total == 0;
    public bool NoMatches => _total > 0 && View.IsEmpty;
    public string NoMatchText => Loc.F("Teams.NoMatch", SearchText.Trim());

    public int SelectedCount => Rows.Count(r => r.IsSelected);
    public bool HasSelection => SelectedCount > 0;
    public string SelectedText => Loc.F("Teams.Selected", SelectedCount);
    public string DeleteSelectedText => Loc.F("Teams.DeleteSelected", SelectedCount);

    // Checked when every team shown is selected, unchecked when none is, blank between.
    public bool? SelectAllState
    {
        get
        {
            var shown = Shown().ToList();
            var chosen = shown.Count(r => r.IsSelected);
            if (shown.Count == 0 || chosen == 0) return false;
            return chosen == shown.Count ? true : null;
        }
    }

    internal void SelectionChanged()
    {
        foreach (var name in new[] { nameof(SelectedCount), nameof(HasSelection), nameof(SelectedText), nameof(DeleteSelectedText), nameof(SelectAllState) })
            OnPropertyChanged(name);
    }

    private void NotifyCounts()
    {
        foreach (var name in new[] { nameof(CountText), nameof(ShownText), nameof(IsEmpty), nameof(NoMatches), nameof(NoMatchText) })
            OnPropertyChanged(name);
    }

    private IEnumerable<TeamListRow> Shown() => Rows.Where(Matches);

    private bool Matches(TeamListRow row)
    {
        var needle = SearchText.Trim();
        return needle.Length == 0
               || row.Name.Contains(needle, StringComparison.CurrentCultureIgnoreCase)
               || row.Tag.Contains(needle, StringComparison.CurrentCultureIgnoreCase);
    }

    private async Task LoadAsync()
    {
        List<Team> teams;
        try
        {
            teams = (await _s.Api.GetAsync<TeamList>("/api/teams")).Teams ?? [];
        }
        catch (Exception error)
        {
            Toasts.Error(error.Message);
            return;
        }

        Dictionary<string, TeamSummary> summaries;
        try
        {
            summaries = ((await _s.Api.GetAsync<TeamSummaries>("/api/team-summaries")).Summaries ?? [])
                .ToDictionary(s => s.TeamId);
        }
        catch
        {
            summaries = new(); // the counts are a nicety; the registry itself still loaded
        }

        // Keep row objects (and their tick boxes) for teams that are still there.
        var existing = Rows.ToDictionary(r => r.Id);
        var next = teams.Select(team =>
        {
            summaries.TryGetValue(team.Id, out var summary);
            if (existing.TryGetValue(team.Id, out var row))
            {
                row.Apply(team, summary);
                return row;
            }
            return new TeamListRow(this, _s, team, summary);
        }).ToList();

        if (!next.Select(r => r.Id).SequenceEqual(Rows.Select(r => r.Id)))
        {
            Rows.Clear();
            foreach (var row in next) Rows.Add(row);
        }

        _total = teams.Count;
        _loaded = true;
        View.Refresh();
        NotifyCounts();
        SelectionChanged();
    }

    private void ToggleAll()
    {
        var shown = Shown().ToList();
        var selectAll = !shown.All(r => r.IsSelected);
        foreach (var row in shown) row.IsSelected = selectAll;
        SelectionChanged();
    }

    private async Task CreateAsync()
    {
        var team = await NewTeam.CreateAsync(_s);
        if (team is null) return;
        NewTeam.Clear();
        Toasts.Info(Loc.F("Teams.Created", team.Name));
        await LoadAsync();
    }

    internal async Task DeleteAsync(TeamListRow row)
    {
        if (await TeamActions.DeleteAsync(_s, row.Id, row.Name, row.Tournaments > 0)) await LoadAsync();
    }

    private async Task DeleteSelectedAsync()
    {
        var chosen = Rows.Where(r => r.IsSelected).ToList();
        if (chosen.Count == 0) return;

        var names = string.Join(", ", chosen.Take(5).Select(r => r.Name));
        if (chosen.Count > 5) names += Loc.F("Teams.AndMore", chosen.Count - 5);
        var body = new List<string> { Loc.F("Teams.DeleteSelectedQ", chosen.Count), names };
        if (chosen.Any(r => r.Tournaments > 0)) body.Add(Loc.T("Teams.DeleteSelectedDropped"));

        if (!Dialogs.Confirm(Loc.T("Teams.DeleteSelectedTitle"), body, Loc.T("Common.DeleteForever"), danger: true)) return;

        var reply = await _s.Api.PostAsync<BulkDeleteReply>("/api/teams/bulk-delete", new { ids = chosen.Select(r => r.Id).ToArray() });
        Toasts.Info(Loc.F("Teams.DeletedN", reply.Removed));
        await LoadAsync();
    }

    internal void OpenProfile(string teamId) => _shell.Open(new TeamProfileViewModel(_s, _shell, teamId));
}
