using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using RovOverlay.Desktop.Core;
using RovOverlay.Desktop.Models;
using RovOverlay.Desktop.Services;

namespace RovOverlay.Desktop.ViewModels;

// Choice records override ToString() because a closed ComboBox shows the selected
// item's ToString(), not its DisplayMemberPath (docs/PLAN.md §9).
public sealed record FormatChoice(string Id, string Label, int MinTeams, int MaxTeams)
{
    public override string ToString() => Label;
}

public sealed class TournamentRow
{
    public TournamentRow(Tournament t)
    {
        Id = t.Id;
        Name = t.Name;
        Note = string.IsNullOrWhiteSpace(t.Note) ? null : t.Note.Trim();
        Status = t.Status;
        IsActive = t.Status == "active";
        FormatLabel = Loc.T("Format." + t.Format);
        SeriesText = $"Bo{t.BestOf}";
        TeamsText = $"{t.TeamCount} / {t.MaxTeams}";
        StatusText = Loc.T("TStatus." + t.Status);
        UpdatedText = FormatTime(t.UpdatedAt);
    }

    public string Id { get; }
    public string Name { get; }
    public string? Note { get; }
    public string Status { get; }
    public bool IsActive { get; }
    public string FormatLabel { get; }
    public string SeriesText { get; }
    public string TeamsText { get; }
    public string StatusText { get; }
    public string UpdatedText { get; }

    private static string FormatTime(long stamp)
    {
        if (stamp <= 0) return "";
        // The server stores Date.now() milliseconds; accept seconds too rather than show 1970.
        var time = stamp < 100_000_000_000
            ? DateTimeOffset.FromUnixTimeSeconds(stamp)
            : DateTimeOffset.FromUnixTimeMilliseconds(stamp);
        return time.ToLocalTime().ToString("d MMM yyyy  HH:mm", Loc.Instance.Culture);
    }
}

public sealed class HomeViewModel : ObservableObject
{
    private readonly AppServices _services;
    private List<Tournament> _tournaments = new();
    private TournamentOptions? _options;
    private bool _loaded;
    private bool _isCreating;
    private string _searchText = "";
    private string _statusFilter = "all";
    private string _newName = "";
    private string _newNote = "";
    private FormatChoice? _newFormat;
    private int _newBestOf = 3;

    public HomeViewModel(AppServices services, ShellViewModel shell)
    {
        _services = services;
        View = CollectionViewSource.GetDefaultView(Rows);
        View.Filter = Matches;

        ShowCreateCommand = new RelayCommand(() => IsCreating = true);
        CancelCreateCommand = new RelayCommand(() =>
        {
            IsCreating = false;
            ResetForm();
        });
        CreateCommand = new AsyncRelayCommand(CreateAsync, () => NewName.Trim().Length > 0 && NewFormat is not null);
        OpenCommand = new RelayCommand(p =>
        {
            if (p is TournamentRow row) shell.Open(new TournamentViewModel(services, shell, row.Id));
        });
        RefreshCommand = new AsyncRelayCommand(LoadAsync);

        // Tournaments change from other places too: another window, the web pages,
        // a restore. The team count on each row moves with the roster.
        services.DataChanged += change =>
        {
            if (change.Topic is "tournaments" or "roster") _ = LoadListAsync();
        };
        Loc.Instance.Changed += () =>
        {
            RebuildFormats();
            RebuildRows();
        };

        _ = LoadAsync();
    }

    public ObservableCollection<TournamentRow> Rows { get; } = new();
    public ICollectionView View { get; }
    public ObservableCollection<FormatChoice> Formats { get; } = new();
    public ObservableCollection<int> BestOfOptions { get; } = new();

    public ICommand ShowCreateCommand { get; }
    public ICommand CancelCreateCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand RefreshCommand { get; }

    public bool IsCreating { get => _isCreating; set => Set(ref _isCreating, value); }
    public string NewName { get => _newName; set => Set(ref _newName, value); }
    public string NewNote { get => _newNote; set => Set(ref _newNote, value); }
    public int NewBestOf { get => _newBestOf; set => Set(ref _newBestOf, value); }

    public FormatChoice? NewFormat
    {
        get => _newFormat;
        set
        {
            if (Set(ref _newFormat, value)) OnPropertyChanged(nameof(FormatHint));
        }
    }

    public string FormatHint => NewFormat is null ? "" : Loc.F("Home.FormatHint", NewFormat.MinTeams, NewFormat.MaxTeams);

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (Set(ref _searchText, value)) RefreshView();
        }
    }

    public string StatusFilter
    {
        get => _statusFilter;
        set
        {
            if (Set(ref _statusFilter, value)) RefreshView();
        }
    }

    public string CountText => Rows.Count.ToString();
    public bool IsEmpty => _loaded && Rows.Count == 0;
    public bool NoMatches => Rows.Count > 0 && View.IsEmpty;

    private async Task LoadAsync()
    {
        await LoadOptionsAsync();
        await LoadListAsync();
    }

    private async Task LoadOptionsAsync()
    {
        try
        {
            _options = await _services.Api.GetAsync<TournamentOptions>("/api/tournament-options");
        }
        catch (Exception error)
        {
            Toasts.Error(error.Message);
            return;
        }

        RebuildFormats();
        BestOfOptions.Clear();
        foreach (var n in _options.BestOf) BestOfOptions.Add(n);
        NewBestOf = DefaultBestOf();
    }

    private async Task LoadListAsync()
    {
        try
        {
            _tournaments = (await _services.Api.GetAsync<TournamentList>("/api/tournaments")).Tournaments ?? new();
        }
        catch (Exception error)
        {
            Toasts.Error(error.Message);
        }
        _loaded = true;
        RebuildRows();
    }

    private void RebuildFormats()
    {
        if (_options is null) return;
        var selected = NewFormat?.Id;
        Formats.Clear();
        foreach (var f in _options.Formats)
            Formats.Add(new FormatChoice(f.Id, Loc.T("Format." + f.Id), f.MinTeams, f.MaxTeams));
        NewFormat = Formats.FirstOrDefault(f => f.Id == selected) ?? Formats.FirstOrDefault();
        OnPropertyChanged(nameof(FormatHint));
    }

    private void RebuildRows()
    {
        Rows.Clear();
        foreach (var t in _tournaments) Rows.Add(new TournamentRow(t));
        RefreshView();
    }

    private void RefreshView()
    {
        View.Refresh();
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(NoMatches));
    }

    private bool Matches(object item)
    {
        if (item is not TournamentRow row) return false;
        if (StatusFilter != "all" && row.Status != StatusFilter) return false;
        var search = SearchText.Trim();
        return search.Length == 0 || row.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase);
    }

    private async Task CreateAsync()
    {
        var reply = await _services.Api.PostAsync<CreatedTournament>("/api/tournaments", new
        {
            name = NewName.Trim(),
            format = NewFormat!.Id,
            bestOf = NewBestOf,
            note = NewNote.Trim()
        });
        Toasts.Info(Loc.F("Home.Created", reply.Tournament.Name));
        IsCreating = false;
        ResetForm();
        await LoadListAsync();
    }

    private void ResetForm()
    {
        NewName = "";
        NewNote = "";
        NewFormat = Formats.FirstOrDefault();
        NewBestOf = DefaultBestOf();
    }

    private int DefaultBestOf() => BestOfOptions.Contains(3) ? 3 : BestOfOptions.FirstOrDefault(1);
}
