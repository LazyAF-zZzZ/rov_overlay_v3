using System.Collections.ObjectModel;
using System.Windows.Input;
using RovOverlay.Desktop.Core;
using RovOverlay.Desktop.Models;
using RovOverlay.Desktop.Services;

namespace RovOverlay.Desktop.ViewModels;

public sealed record HistoryHeader(string Title, ICommand OpenCommand);

public sealed record HistoryLine(
    string RoundText,
    string OpponentText,
    bool OpponentIsLink,
    bool OpponentMuted,
    ICommand? OpenOpponentCommand,
    string ScoreText,
    string BoText,
    string OutcomeText,
    string OutcomeKind);

public sealed record ProfileTournament(string Name, string StatusText, bool IsActive, string FormatText, string SeedText, ICommand OpenCommand);

// One team: its roster, the tournaments it entered, every match it played.
// v2's /teams/:id, rebuilt.
public sealed class TeamProfileViewModel : TeamFace, IClosablePage
{
    private readonly ShellViewModel _shell;
    private readonly string _id;
    private TeamHistory? _data;
    private bool _notFound;
    private string _errorText = "";
    private string _tab = "profile";

    public TeamProfileViewModel(AppServices services, ShellViewModel shell, string id) : base(services)
    {
        _shell = shell;
        _id = id;
        Editor = new TeamEditor(
            showSeed: false,
            save: SaveAsync,
            uploadLogo: async () =>
            {
                await TeamActions.UploadLogoAsync(Services, _id);
                await LoadAsync(forceEditor: false);
            },
            clearLogo: async () =>
            {
                await TeamActions.ClearLogoAsync(Services, _id);
                await LoadAsync(forceEditor: false);
            },
            delete: DeleteAsync);
        BackCommand = new RelayCommand(shell.Back);
        Stats = new TeamStatsPanel(services, shell, id);
        RevertCommand = new RelayCommand(() =>
        {
            if (_data is null) return;
            Editor.Load(_data.Team);
            Toasts.Info(Loc.T("Common.Reverted"));
        });

        services.DataChanged += OnDataChanged;
        Loc.Instance.Changed += OnLanguageChanged;
        _ = LoadAsync(forceEditor: true);
    }

    public TeamEditor Editor { get; }

    // The Statistics tab, and which tab is showing ("profile" or "stats").
    public TeamStatsPanel Stats { get; }

    public string Tab
    {
        get => _tab;
        set
        {
            if (!Set(ref _tab, value ?? "profile")) return;
            OnPropertyChanged(nameof(IsProfileTab));
            OnPropertyChanged(nameof(IsStatsTab));
            if (IsStatsTab) _ = Stats.EnsureLoadedAsync();
        }
    }

    public bool IsProfileTab => Tab != "stats";
    public bool IsStatsTab => Tab == "stats";
    public ICommand BackCommand { get; }
    public ICommand RevertCommand { get; }
    public ObservableCollection<ProfileTournament> Tournaments { get; } = new();
    public ObservableCollection<object> History { get; } = new();

    public bool NotFound { get => _notFound; private set => Set(ref _notFound, value); }
    public string ErrorText { get => _errorText; private set => Set(ref _errorText, value); }

    public string? CaptainText
    {
        get
        {
            var captain = _data?.Team.Players?.FirstOrDefault(p => p.IsCaptain && !string.IsNullOrWhiteSpace(p.Name));
            return captain is null ? null : Loc.F("Profile.Captain", captain.Name);
        }
    }

    public bool HasRecord => _data?.Record.Played > 0;
    public string RecordText => _data is null ? "" : Loc.F("Teams.Record", _data.Record.Won, _data.Record.Lost);
    public bool IsWinning => _data is not null && _data.Record.Won >= _data.Record.Lost;
    public string GamesText => _data is null ? "" : Loc.F("Profile.Games", _data.Record.GamesWon, _data.Record.GamesLost);
    public string TournamentsCountText => _data is null ? ""
        : _data.Record.Tournaments == 1 ? Loc.T("Teams.InOneTournament")
        : Loc.F("Teams.InTournaments", _data.Record.Tournaments);
    public bool HasTournaments => Tournaments.Count > 0;
    public string? HistoryHint { get; private set; }
    public bool HistoryEmpty => _data is not null && History.Count == 0;

    private static string E(string value) => Uri.EscapeDataString(value);

    private async Task LoadAsync(bool forceEditor)
    {
        TeamHistory data;
        try
        {
            data = await Services.Api.GetAsync<TeamHistory>($"/api/teams/{E(_id)}/history");
        }
        catch (ApiException error) when (error.Status == 404)
        {
            NotFound = true;
            ErrorText = error.Message;
            return;
        }
        catch (Exception error)
        {
            if (_data is null)
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

        // A change pushed from elsewhere must not wipe what the operator is typing.
        var editorClean = _data is null || !Editor.Differs(_data.Team);
        _data = data;
        Apply(data.Team);
        if (forceEditor || editorClean) Editor.Load(data.Team);
        Build();
    }

    private void Build()
    {
        if (_data is null) return;

        Tournaments.Clear();
        foreach (var t in _data.Tournaments ?? [])
        {
            var id = t.Id;
            Tournaments.Add(new ProfileTournament(
                t.Name,
                Loc.T("TStatus." + t.Status),
                t.Status == "active",
                Loc.T("Format." + t.Format),
                Loc.F("Team.Seed", t.Seed),
                new RelayCommand(() => _shell.Open(new TournamentViewModel(Services, _shell, id)))));
        }

        Stats.SetTournaments(_data.Tournaments ?? []);

        History.Clear();
        var heading = "";
        foreach (var match in _data.Matches ?? [])
        {
            if (match.TournamentName != heading)
            {
                heading = match.TournamentName;
                var tournamentId = match.TournamentId;
                History.Add(new HistoryHeader(heading,
                    new RelayCommand(() => _shell.Open(new TournamentViewModel(Services, _shell, tournamentId)))));
            }
            History.Add(Line(match));
        }

        var played = (_data.Matches ?? []).Count(m => m.Outcome is "win" or "loss");
        HistoryHint = played > 0 ? Loc.F("Profile.HistoryHint", played) : null;

        foreach (var name in new[]
                 {
                     nameof(CaptainText), nameof(HasRecord), nameof(RecordText), nameof(IsWinning), nameof(GamesText),
                     nameof(TournamentsCountText), nameof(HasTournaments), nameof(HistoryHint), nameof(HistoryEmpty)
                 })
            OnPropertyChanged(name);
    }

    private HistoryLine Line(HistoryMatch m)
    {
        string opponent;
        var isLink = false;
        var muted = false;
        ICommand? open = null;

        if (m.IsBye)
        {
            opponent = Loc.T("Profile.NoOpponent");
            muted = true;
        }
        else if (m.OpponentId is { } opponentId)
        {
            opponent = m.OpponentName ?? Loc.T("Profile.UnknownTeam");
            isLink = true;
            open = new RelayCommand(() => _shell.Open(new TeamProfileViewModel(Services, _shell, opponentId)));
        }
        else if (m.OpponentGone)
        {
            opponent = m.OpponentName ?? Loc.T("Profile.DeletedTeam");
            muted = true;
        }
        else
        {
            opponent = Loc.T("Profile.Tbd");
            muted = true;
        }

        var (outcome, kind) = m switch
        {
            { IsBye: true } => (Loc.T("Profile.Bye"), "none"),
            { Outcome: "win" } => (Loc.T("Profile.Win"), "win"),
            { Outcome: "loss" } => (Loc.T("Profile.Loss"), "loss"),
            { Status: "live" } => (Loc.T("Profile.InProgress"), "live"),
            _ => (Loc.T("Profile.NotPlayed"), "none")
        };

        return new HistoryLine(
            Rounds.Label(m.Bracket, m.Round),
            opponent,
            isLink,
            muted,
            open,
            m.IsBye ? "–" : $"{m.Score} – {m.OpponentScore}",
            $"Bo{m.BestOf}",
            outcome,
            kind);
    }

    private async Task SaveAsync(TeamEditor editor)
    {
        await TeamActions.SaveAsync(Services, _id, editor);
        await LoadAsync(forceEditor: true);
    }

    private async Task DeleteAsync()
    {
        var entered = _data?.Record.Tournaments > 0 || Tournaments.Count > 0;
        if (await TeamActions.DeleteAsync(Services, _id, Name, entered)) _shell.Back();
    }

    private void OnDataChanged(DataChange change)
    {
        var mine = change.TeamId is null || change.TeamId == _id;
        if ((change.Topic == "teams" && mine) || change.Topic is "matches" or "roster")
            _ = LoadAsync(forceEditor: false);

        // Statistics follow results and drafts too, once the tab has been opened.
        if (Stats.IsLoaded && change.Topic is "matches" or "games" or "roster" or "teams")
            _ = Stats.LoadAsync();
    }

    private void OnLanguageChanged() => Build();

    public void OnClosed()
    {
        Services.DataChanged -= OnDataChanged;
        Loc.Instance.Changed -= OnLanguageChanged;
        Stats.Detach();
    }
}
