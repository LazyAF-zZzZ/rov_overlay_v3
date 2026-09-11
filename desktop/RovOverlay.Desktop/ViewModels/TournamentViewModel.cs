using System.Collections.ObjectModel;
using System.Windows.Input;
using RovOverlay.Desktop.Core;
using RovOverlay.Desktop.Models;
using RovOverlay.Desktop.Services;

namespace RovOverlay.Desktop.ViewModels;

public sealed record TextChoice(string Value, string Label)
{
    public override string ToString() => Label;
}

public sealed record IntChoice(int Value, string Label)
{
    public override string ToString() => Label;
}

public sealed record TeamChoice(string Id, string Label)
{
    public override string ToString() => Label;
}

public sealed class RosterRow : TeamFace
{
    private readonly TournamentViewModel _owner;
    private TeamEditor? _editor;
    private bool _isEditing;
    private int _seed;

    public RosterRow(TournamentViewModel owner, AppServices services, Team team) : base(services)
    {
        _owner = owner;
        EditCommand = new RelayCommand(() => IsEditing = !IsEditing);
        ProfileCommand = new RelayCommand(() => owner.OpenTeam(Id));
        RemoveCommand = new AsyncRelayCommand(() => owner.RemoveAsync(this));
        Apply(team);
    }

    public ICommand EditCommand { get; }
    public ICommand ProfileCommand { get; }
    public ICommand RemoveCommand { get; }

    public int Seed
    {
        get => _seed;
        private set
        {
            if (Set(ref _seed, value)) OnPropertyChanged(nameof(SeedText));
        }
    }

    public string SeedText => Loc.F("Team.Seed", Seed);

    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (!Set(ref _isEditing, value)) return;
            if (value && Team is not null) Editor.Load(Team);
            OnPropertyChanged(nameof(EditLabel));
        }
    }

    public string EditLabel => Loc.T(IsEditing ? "Common.Close" : "Common.Edit");

    public TeamEditor Editor => _editor ??= CreateEditor();

    private TeamEditor CreateEditor()
    {
        var editor = new TeamEditor(
            showSeed: true,
            save: e => _owner.SaveTeamAsync(this, e),
            uploadLogo: () => _owner.UploadLogoAsync(this),
            clearLogo: () => _owner.ClearLogoAsync(this),
            delete: () => _owner.DeleteTeamAsync(this));
        if (Team is not null) editor.Load(Team);
        return editor;
    }

    public override void Apply(Team team)
    {
        // An open editor keeps what the operator typed; everything else follows the server.
        var editorClean = _editor is null || Team is null || !_editor.Differs(Team);
        base.Apply(team);
        Seed = team.Seed;
        if (editorClean) _editor?.Load(team);
    }

    public override void RefreshText()
    {
        base.RefreshText();
        OnPropertyChanged(nameof(SeedText));
        OnPropertyChanged(nameof(EditLabel));
    }
}

public sealed class StandingLine : ObservableObject
{
    private bool _isThrough;

    public StandingLine(StandingRow row)
    {
        RankText = row.Tied ? $"{row.Rank}=" : row.Rank.ToString();
        Name = string.IsNullOrWhiteSpace(row.Name) ? "—" : row.Name;
        Played = row.Played;
        Won = row.Won;
        Lost = row.Lost;
        DiffText = row.GameDiff > 0 ? $"+{row.GameDiff}" : row.GameDiff.ToString();
        Points = row.Points;
        Tied = row.Tied;
    }

    public string RankText { get; }
    public string Name { get; }
    public int Played { get; }
    public int Won { get; }
    public int Lost { get; }
    public string DiffText { get; }
    public int Points { get; }
    public bool Tied { get; }
    public bool IsThrough { get => _isThrough; set => Set(ref _isThrough, value); }
}

public sealed record StandingsGroupView(string Title, string? RemainingText, IReadOnlyList<StandingLine> Rows);

// One tournament: its details, its teams, where its matches are, its standings.
// v2's /tournament/:id, rebuilt.
public sealed class TournamentViewModel : ObservableObject, IClosablePage
{
    private const string DetailsFold = "tournament.details";
    private const string TeamsFold = "tournament.teams";
    private static readonly string[] StandingsFormats = ["round_robin", "group_stage"];

    private readonly AppServices _s;
    private readonly ShellViewModel _shell;
    private readonly string _id;
    private TournamentOptions? _options;
    private Tournament? _t;
    private List<Team> _registry = new();
    private bool _notFound;
    private string _errorText = "";

    private string _fName = "";
    private string _fNote = "";
    private string _fStatus = "active";
    private FormatChoice? _fFormat;
    private int _fBestOf = 3;
    private bool _detailsOpen;
    private bool _teamsOpen;
    private bool _addOpen;
    private TeamChoice? _pick;

    private string? _matchBadge;
    private bool _matchesComplete;
    private string _matchNote = "";
    private bool _showStandings;
    private int _perGroup = 2;
    private string _perGroupText = "2";
    private bool _anyTied;

    public TournamentViewModel(AppServices services, ShellViewModel shell, string id)
    {
        _s = services;
        _shell = shell;
        _id = id;
        _detailsOpen = services.Settings.IsOpen(DetailsFold, false);
        _teamsOpen = services.Settings.IsOpen(TeamsFold, true);

        BackCommand = new RelayCommand(shell.Back);
        SaveDetailsCommand = new AsyncRelayCommand(SaveDetailsAsync, () => _t is not null && FName.Trim().Length > 0);
        RevertCommand = new RelayCommand(() =>
        {
            if (_t is null) return;
            LoadForm(_t);
            Toasts.Info(Loc.T("Common.Reverted"));
        });
        DeleteCommand = new AsyncRelayCommand(DeleteAsync);
        ToggleDetailsCommand = new RelayCommand(() => DetailsOpen = !DetailsOpen);
        ToggleTeamsCommand = new RelayCommand(() => TeamsOpen = !TeamsOpen);
        ToggleAddCommand = new RelayCommand(() =>
        {
            AddOpen = !AddOpen;
            if (AddOpen) TeamsOpen = true;
        });
        CloseAddCommand = new RelayCommand(() => AddOpen = false);
        AddExistingCommand = new AsyncRelayCommand(AddExistingAsync, () => Pick is not null && !IsFull);
        CreateAndAddCommand = new AsyncRelayCommand(CreateAndAddAsync, () => !IsFull);
        ClearNewTeamCommand = new RelayCommand(() =>
        {
            NewTeam.Clear();
            Toasts.Info(Loc.T("Team.FormCleared"));
        });
        OpenBracketCommand = new RelayCommand(() => shell.Open(new BracketViewModel(services, shell, id)));
        OpenDraftsCommand = new RelayCommand(() => shell.Open(new DraftHistoryViewModel(services, shell, id)));
        DrawPlayoffCommand = new AsyncRelayCommand(DrawPlayoffAsync);
        ObsSources = ObsSourceRow.CreateForTournament(services, id);

        services.DataChanged += OnDataChanged;
        Loc.Instance.Changed += OnLanguageChanged;
        _ = LoadAsync();
    }

    public ICommand BackCommand { get; }
    public ICommand SaveDetailsCommand { get; }
    public ICommand RevertCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ToggleDetailsCommand { get; }
    public ICommand ToggleTeamsCommand { get; }
    public ICommand ToggleAddCommand { get; }
    public ICommand CloseAddCommand { get; }
    public ICommand AddExistingCommand { get; }
    public ICommand CreateAndAddCommand { get; }
    public ICommand ClearNewTeamCommand { get; }
    public ICommand OpenBracketCommand { get; }
    public ICommand OpenDraftsCommand { get; }
    public ICommand DrawPlayoffCommand { get; }

    public NewTeamForm NewTeam { get; } = new();
    public IReadOnlyList<ObsSourceRow> ObsSources { get; }

    // ---- header -----------------------------------------------------------

    public bool NotFound { get => _notFound; private set => Set(ref _notFound, value); }
    public string ErrorText { get => _errorText; private set => Set(ref _errorText, value); }
    public string Title => _t?.Name ?? "";
    public bool IsActive => _t?.Status == "active";
    public string StatusText => _t is null ? "" : Loc.T("TStatus." + _t.Status);
    public string FormatText => _t is null ? "" : Loc.T("Format." + _t.Format);
    public string SeriesText => _t is null ? "" : Loc.F("Tour.BestOf", _t.BestOf);

    // ---- details form -----------------------------------------------------

    public ObservableCollection<FormatChoice> Formats { get; } = new();
    public ObservableCollection<IntChoice> BestOfChoices { get; } = new();
    public ObservableCollection<TextChoice> StatusChoices { get; } = new();

    public bool DetailsOpen
    {
        get => _detailsOpen;
        set
        {
            if (Set(ref _detailsOpen, value)) _s.Settings.SetOpen(DetailsFold, value);
        }
    }

    public string FName { get => _fName; set => Set(ref _fName, value ?? ""); }
    public string FNote { get => _fNote; set => Set(ref _fNote, value ?? ""); }
    public string FStatus { get => _fStatus; set => Set(ref _fStatus, value ?? "active"); }
    public int FBestOf { get => _fBestOf; set => Set(ref _fBestOf, value); }

    public FormatChoice? FFormat
    {
        get => _fFormat;
        set
        {
            if (!Set(ref _fFormat, value)) return;
            OnPropertyChanged(nameof(FormatHint));
            OnPropertyChanged(nameof(FormatHintIsWarning));
        }
    }

    public bool FormatHintIsWarning => FFormat is not null && _t is not null && _t.TeamCount > FFormat.MaxTeams;

    public string FormatHint
    {
        get
        {
            if (FFormat is null) return "";
            return FormatHintIsWarning
                ? Loc.F("Tour.FormatTooSmall", FFormat.Label, FFormat.MaxTeams, _t!.TeamCount)
                : Loc.F("Home.FormatHint", FFormat.MinTeams, FFormat.MaxTeams);
        }
    }

    private bool IsDetailsDirty =>
        _t is not null && (FName != _t.Name || FNote != (_t.Note ?? "") || FStatus != _t.Status
                           || FFormat?.Id != _t.Format || FBestOf != _t.BestOf);

    // ---- teams ------------------------------------------------------------

    public ObservableCollection<RosterRow> Roster { get; } = new();
    public ObservableCollection<TeamChoice> Available { get; } = new();

    public bool TeamsOpen
    {
        get => _teamsOpen;
        set
        {
            if (Set(ref _teamsOpen, value)) _s.Settings.SetOpen(TeamsFold, value);
        }
    }

    public bool AddOpen { get => _addOpen; set => Set(ref _addOpen, value); }
    public TeamChoice? Pick { get => _pick; set => Set(ref _pick, value); }
    public bool HasAvailable => Available.Count > 0;
    public string PickerEmptyText => Loc.T(_registry.Count == 0 ? "Tour.RegistryEmpty" : "Tour.AllIn");
    public bool IsFull => _t is not null && _t.TeamCount >= _t.MaxTeams;
    public string TeamCountText => _t is null ? "" : Loc.F("Tour.TeamCount", _t.TeamCount, _t.MaxTeams);
    public bool RosterEmpty => _t is not null && Roster.Count == 0;
    public string RosterEmptyText => _t is null ? "" : Loc.F("Tour.NoTeams", _t.MaxTeams);

    // ---- match session and standings ---------------------------------------

    public string? MatchBadge { get => _matchBadge; private set => Set(ref _matchBadge, value); }
    public bool MatchesComplete { get => _matchesComplete; private set => Set(ref _matchesComplete, value); }
    public string MatchNote { get => _matchNote; private set => Set(ref _matchNote, value); }

    public ObservableCollection<StandingsGroupView> StandingGroups { get; } = new();
    public bool ShowStandings { get => _showStandings; private set => Set(ref _showStandings, value); }
    public bool NoStandings => ShowStandings && StandingGroups.Count == 0;
    public bool AnyTied { get => _anyTied; private set => Set(ref _anyTied, value); }

    public string PerGroupText
    {
        get => _perGroupText;
        set
        {
            if (!Set(ref _perGroupText, value ?? "")) return;
            if (int.TryParse(_perGroupText.Trim(), out var n) && n is >= 1 and <= 8)
            {
                _perGroup = n;
                ApplyThrough();
            }
        }
    }

    // ---- loading ----------------------------------------------------------

    private static string E(string value) => Uri.EscapeDataString(value);

    private async Task LoadAsync()
    {
        try
        {
            _options = await _s.Api.GetAsync<TournamentOptions>("/api/tournament-options");
        }
        catch
        {
            // The form falls back to what the tournament itself says.
        }

        try
        {
            var detail = await _s.Api.GetAsync<TournamentDetail>($"/api/tournaments/{E(_id)}");
            ApplyTournament(detail.Tournament, loadForm: true);
            ApplyRoster(detail.Teams);
        }
        catch (Exception error)
        {
            NotFound = true;
            ErrorText = error.Message;
            return;
        }

        await RefreshRegistryAsync();
        await LoadMatchesAsync();
        await LoadStandingsAsync();
    }

    private async Task ReloadAsync(bool registryToo)
    {
        try
        {
            var detail = await _s.Api.GetAsync<TournamentDetail>($"/api/tournaments/{E(_id)}");
            ApplyTournament(detail.Tournament, loadForm: false);
            ApplyRoster(detail.Teams);
        }
        catch (ApiException error) when (error.Status == 404)
        {
            NotFound = true;
            ErrorText = Loc.T("Tour.Gone");
            return;
        }
        catch
        {
            return; // the next change notice will bring it back
        }
        if (registryToo) await RefreshRegistryAsync();
    }

    private void ApplyTournament(Tournament t, bool loadForm)
    {
        var wasDirty = IsDetailsDirty;
        _t = t;
        if (loadForm || !wasDirty) LoadForm(t);
        foreach (var name in new[]
                 {
                     nameof(Title), nameof(IsActive), nameof(StatusText), nameof(FormatText), nameof(SeriesText),
                     nameof(IsFull), nameof(TeamCountText), nameof(RosterEmptyText), nameof(FormatHint),
                     nameof(FormatHintIsWarning)
                 })
            OnPropertyChanged(name);
    }

    private void LoadForm(Tournament t)
    {
        RebuildChoices(t);
        FName = t.Name;
        FNote = t.Note ?? "";
        FStatus = t.Status;
        FFormat = Formats.FirstOrDefault(f => f.Id == t.Format);
        FBestOf = t.BestOf;
    }

    private void RebuildChoices(Tournament t)
    {
        Formats.Clear();
        var formats = _options?.Formats ?? [new FormatOption(t.Format, t.Format, 2, t.MaxTeams)];
        foreach (var f in formats) Formats.Add(new FormatChoice(f.Id, Loc.T("Format." + f.Id), f.MinTeams, f.MaxTeams));

        BestOfChoices.Clear();
        foreach (var n in _options?.BestOf ?? [t.BestOf]) BestOfChoices.Add(new IntChoice(n, Loc.F("Tour.BestOf", n)));

        StatusChoices.Clear();
        foreach (var s in _options?.Statuses ?? ["active", "finished"]) StatusChoices.Add(new TextChoice(s, Loc.T("TStatus." + s)));
    }

    // Rows are matched by team id and updated in place, so an open editor survives a
    // change pushed from another screen.
    private void ApplyRoster(List<Team>? teams)
    {
        var list = teams ?? [];
        var existing = Roster.ToDictionary(r => r.Id);
        var next = list.Select(team =>
        {
            if (existing.TryGetValue(team.Id, out var row))
            {
                row.Apply(team);
                return row;
            }
            return new RosterRow(this, _s, team);
        }).ToList();

        if (!next.Select(r => r.Id).SequenceEqual(Roster.Select(r => r.Id)))
        {
            Roster.Clear();
            foreach (var row in next) Roster.Add(row);
        }

        RebuildAvailable();
        OnPropertyChanged(nameof(RosterEmpty));
    }

    private async Task RefreshRegistryAsync()
    {
        try
        {
            _registry = (await _s.Api.GetAsync<TeamList>("/api/teams")).Teams ?? [];
        }
        catch (Exception error)
        {
            Toasts.Error(error.Message);
            _registry = [];
        }
        RebuildAvailable();
    }

    private void RebuildAvailable()
    {
        var inRoster = Roster.Select(r => r.Id).ToHashSet();
        var keep = Pick?.Id;
        Available.Clear();
        foreach (var team in _registry.Where(t => !inRoster.Contains(t.Id)))
            Available.Add(new TeamChoice(team.Id, string.IsNullOrWhiteSpace(team.Tag) ? team.Name : $"{team.Name} ({team.Tag})"));
        Pick = Available.FirstOrDefault(c => c.Id == keep) ?? Available.FirstOrDefault();
        OnPropertyChanged(nameof(HasAvailable));
        OnPropertyChanged(nameof(PickerEmptyText));
    }

    private async Task LoadMatchesAsync()
    {
        List<MatchLite> matches;
        try
        {
            matches = (await _s.Api.GetAsync<MatchList>($"/api/tournaments/{E(_id)}/matches")).Matches ?? [];
        }
        catch (Exception error)
        {
            Toasts.Error(error.Message);
            return;
        }

        var total = matches.Count(m => !m.IsBye);
        var played = matches.Count(m => !m.IsBye && m.Status == "complete");
        MatchBadge = total > 0 ? Loc.F("Tour.Played", played, total) : null;
        MatchesComplete = total > 0 && played == total;
        MatchNote = total > 0
            ? Loc.F("Tour.MatchesDrawn", total)
            : Loc.T(_t is not null && _t.TeamCount < 2 ? "Tour.NeedTwoTeams" : "Tour.NoBracket");
    }

    private async Task LoadStandingsAsync()
    {
        if (_t is null || !StandingsFormats.Contains(_t.Format))
        {
            ShowStandings = false;
            OnPropertyChanged(nameof(NoStandings));
            return;
        }

        List<StandingsGroup> groups;
        try
        {
            groups = (await _s.Api.GetAsync<StandingsReply>($"/api/tournaments/{E(_id)}/standings")).Groups ?? [];
        }
        catch
        {
            ShowStandings = false;
            OnPropertyChanged(nameof(NoStandings));
            return;
        }

        StandingGroups.Clear();
        foreach (var group in groups)
        {
            var title = group.Bracket == "main" ? Loc.T("Tour.Table") : Loc.F("Tour.Group", group.Bracket);
            var remaining = group.Remaining > 0 ? Loc.F("Tour.StillToPlay", group.Remaining) : null;
            StandingGroups.Add(new StandingsGroupView(title, remaining, (group.Rows ?? []).Select(r => new StandingLine(r)).ToList()));
        }
        AnyTied = StandingGroups.Any(g => g.Rows.Any(r => r.Tied));
        ApplyThrough();
        ShowStandings = true;
        OnPropertyChanged(nameof(NoStandings));
    }

    private void ApplyThrough()
    {
        foreach (var group in StandingGroups)
        {
            for (var i = 0; i < group.Rows.Count; i++) group.Rows[i].IsThrough = i < _perGroup;
        }
    }

    // ---- actions ----------------------------------------------------------

    private async Task SaveDetailsAsync()
    {
        var reply = await _s.Api.PutAsync<TournamentUpdate>($"/api/tournaments/{E(_id)}", new
        {
            name = FName.Trim(),
            format = FFormat?.Id ?? _t?.Format,
            bestOf = FBestOf,
            status = FStatus,
            note = FNote
        });
        ApplyTournament(reply.Tournament, loadForm: true);
        Toasts.Info(Loc.T("Common.Saved"));
        await LoadStandingsAsync();
    }

    private async Task DeleteAsync()
    {
        if (_t is null) return;

        var onAir = false;
        try
        {
            onAir = (await _s.Api.GetAsync<LiveResponse>("/api/live-match")).Live.TournamentId == _id;
        }
        catch
        {
            // Not knowing is not a reason to stop; the server clears the live pointer anyway.
        }

        var body = new List<string> { Loc.F("Tour.DeleteQ", _t.Name) };
        if (onAir) body.Add(Loc.T("Tour.DeleteOnAir"));
        body.Add(Loc.T("Tour.DeleteErases"));
        body.Add(Loc.T("Tour.DeleteTeamsStay"));
        if (!Dialogs.Confirm(Loc.T("Tour.DeleteTitle"), body, Loc.T("Common.DeleteForever"), danger: true)) return;

        var reply = await _s.Api.DeleteAsync<DeleteTournamentReply>($"/api/tournaments/{E(_id)}");
        var gone = reply.Removed is { } r && (r.Matches > 0 || r.Games > 0) ? Loc.F("Tour.DeletedCounts", r.Matches, r.Games) : "";
        Toasts.Info(Loc.F("Tour.Deleted", _t.Name) + gone);
        _shell.Back();
    }

    private async Task AddTeamAsync(string teamId)
    {
        var reply = await _s.Api.PostAsync<RosterReply>($"/api/tournaments/{E(_id)}/teams", new { teamId });
        ApplyTournament(reply.Tournament, loadForm: false);
        ApplyRoster(reply.Teams);
    }

    private async Task AddExistingAsync()
    {
        if (Pick is null) return;
        await AddTeamAsync(Pick.Id);
        Toasts.Info(Loc.T("Tour.TeamAdded"));
    }

    private async Task CreateAndAddAsync()
    {
        var team = await NewTeam.CreateAsync(_s);
        if (team is null) return;

        try
        {
            await RefreshRegistryAsync();
            await AddTeamAsync(team.Id);
            NewTeam.Clear();
            Toasts.Info(Loc.F("Tour.TeamAddedNamed", team.Name));
        }
        catch (Exception error)
        {
            await RefreshRegistryAsync();
            Toasts.Error(Loc.F("Tour.InRegistryBut", team.Name, error.Message));
        }
    }

    internal async Task RemoveAsync(RosterRow row)
    {
        if (!Dialogs.Confirm(Loc.T("Tour.RemoveTitle"), [Loc.F("Tour.RemoveQ", row.Name), Loc.T("Tour.RemoveStays")], Loc.T("Tour.Remove")))
            return;
        var reply = await _s.Api.DeleteAsync<RosterReply>($"/api/tournaments/{E(_id)}/teams/{E(row.Id)}");
        ApplyTournament(reply.Tournament, loadForm: false);
        ApplyRoster(reply.Teams);
        Toasts.Info(Loc.T("Tour.Removed"));
    }

    internal async Task SaveTeamAsync(RosterRow row, TeamEditor editor)
    {
        await _s.Api.PutAsync<TeamReply>($"/api/teams/{E(row.Id)}", editor.Body());
        if (editor.SeedValue != row.Seed)
        {
            await _s.Api.PutAsync<OkReply>($"/api/tournaments/{E(_id)}/teams/{E(row.Id)}/seed", new { seed = editor.SeedValue });
        }
        row.IsEditing = false;
        await ReloadAsync(registryToo: true);
        Toasts.Info(Loc.T("Team.Saved"));
    }

    internal async Task UploadLogoAsync(RosterRow row)
    {
        await TeamActions.UploadLogoAsync(_s, row.Id);
        await ReloadAsync(registryToo: false);
    }

    internal async Task ClearLogoAsync(RosterRow row)
    {
        await TeamActions.ClearLogoAsync(_s, row.Id);
        await ReloadAsync(registryToo: false);
    }

    internal async Task DeleteTeamAsync(RosterRow row)
    {
        if (await TeamActions.DeleteAsync(_s, row.Id, row.Name, enteredTournaments: true))
            await ReloadAsync(registryToo: true);
    }

    internal void OpenTeam(string teamId) => _shell.Open(new TeamProfileViewModel(_s, _shell, teamId));

    private async Task DrawPlayoffAsync()
    {
        var ok = Dialogs.Confirm(Loc.T("Tour.PlayoffTitle"),
            [Loc.F("Tour.PlayoffTop", _perGroup), Loc.T("Tour.PlayoffSafe")], Loc.T("Tour.DrawPlayoff"));
        if (!ok) return;

        var reply = await _s.Api.PostAsync<PlayoffReply>($"/api/tournaments/{E(_id)}/playoffs", new { perGroup = _perGroup });
        Toasts.Info(Loc.F("Tour.Through", reply.Promoted));
        await LoadMatchesAsync();
        await LoadStandingsAsync();
    }

    // ---- live updates -----------------------------------------------------

    private void OnDataChanged(DataChange change)
    {
        var mine = change.TournamentId is null || change.TournamentId == _id;
        if (change.Topic == "matches" && mine)
        {
            _ = LoadMatchesAsync();
            _ = LoadStandingsAsync();
        }
        if ((change.Topic == "roster" && mine) || change.Topic == "teams") _ = ReloadAsync(registryToo: true);
        if (change.Topic == "tournaments" && mine) _ = ReloadAsync(registryToo: false);
    }

    private void OnLanguageChanged()
    {
        if (_t is not null)
        {
            var keep = (FName, FNote, FStatus, Format: FFormat?.Id, FBestOf);
            RebuildChoices(_t);
            FFormat = Formats.FirstOrDefault(f => f.Id == keep.Format);
            (FName, FNote, FStatus, FBestOf) = (keep.FName, keep.FNote, keep.FStatus, keep.FBestOf);
            ApplyTournament(_t, loadForm: false);
        }
        foreach (var row in Roster) row.RefreshText();
        OnPropertyChanged(nameof(PickerEmptyText));
        _ = LoadMatchesAsync();
        _ = LoadStandingsAsync();
    }

    public void OnClosed()
    {
        _s.DataChanged -= OnDataChanged;
        Loc.Instance.Changed -= OnLanguageChanged;
    }
}
