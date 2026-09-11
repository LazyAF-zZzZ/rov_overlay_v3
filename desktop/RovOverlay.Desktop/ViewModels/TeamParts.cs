using System.IO;
using System.Windows.Input;
using RovOverlay.Desktop.Core;
using RovOverlay.Desktop.Models;
using RovOverlay.Desktop.Services;

namespace RovOverlay.Desktop.ViewModels;

// Pieces shared by every screen that shows or edits teams: the tournament's roster, the
// registry, and the team profile. Same split as v2's public/js/lib/team-ui.js.

public sealed class PositionChoice : ObservableObject
{
    public PositionChoice(string value, string labelKey)
    {
        Value = value;
        LabelKey = labelKey;
        Loc.Instance.Changed += () => OnPropertyChanged(nameof(Label));
    }

    public string Value { get; }
    public string LabelKey { get; }
    public string Label => Loc.T(LabelKey);

    // The closed ComboBox shows the selected item's ToString(): DisplayMemberPath only
    // reaches the drop-down list through our own template (docs/PLAN.md §9).
    public override string ToString() => Label;
}

public sealed class PlayerRow : ObservableObject
{
    private readonly PlayerEditor _owner;
    private string _name = "";
    private string _position = "";
    private bool _isCaptain;

    public PlayerRow(PlayerEditor owner, int slot)
    {
        _owner = owner;
        Slot = slot;
    }

    public int Slot { get; }
    public string Placeholder => Loc.F("Team.PlayerN", Slot);
    public string Name { get => _name; set => Set(ref _name, value ?? ""); }
    public string Position { get => _position; set => Set(ref _position, value ?? ""); }

    // One captain per team: choosing one clears the others.
    public bool IsCaptain
    {
        get => _isCaptain;
        set
        {
            if (Set(ref _isCaptain, value) && value) _owner.CaptainChosen(this);
        }
    }

    internal void ClearCaptain()
    {
        _isCaptain = false;
        OnPropertyChanged(nameof(IsCaptain));
    }
}

// The five player slots: name, position, captain. The server sanitises what it gets,
// so this only has to keep the form honest.
public sealed class PlayerEditor
{
    public const int RosterSize = 5;

    public static IReadOnlyList<PositionChoice> Positions { get; } =
    [
        new("", "Pos.none"),
        new("jungle", "Pos.jungle"),
        new("carry", "Pos.carry"),
        new("midlane", "Pos.midlane"),
        new("offlane", "Pos.offlane"),
        new("support", "Pos.support")
    ];

    public PlayerEditor(IEnumerable<TeamPlayer>? players = null)
    {
        Rows = Enumerable.Range(1, RosterSize).Select(slot => new PlayerRow(this, slot)).ToList();
        Load(players);
    }

    public IReadOnlyList<PlayerRow> Rows { get; }

    public void Load(IEnumerable<TeamPlayer>? players)
    {
        var list = players?.ToList() ?? [];
        for (var i = 0; i < Rows.Count; i++)
        {
            var player = i < list.Count ? list[i] : null;
            Rows[i].Name = player?.Name ?? "";
            Rows[i].Position = Positions.Any(p => p.Value == player?.Position) ? player!.Position! : "";
            Rows[i].IsCaptain = player?.IsCaptain == true;
        }
    }

    public void Clear() => Load(null);

    public object[] Read() =>
        Rows.Select(r => (object)new { name = r.Name.Trim(), position = r.Position, isCaptain = r.IsCaptain }).ToArray();

    public bool Matches(IEnumerable<TeamPlayer>? players)
    {
        var list = players?.ToList() ?? [];
        for (var i = 0; i < Rows.Count; i++)
        {
            var player = i < list.Count ? list[i] : null;
            if (Rows[i].Name.Trim() != (player?.Name ?? "")) return false;
            if (Rows[i].Position != (player?.Position ?? "")) return false;
            if (Rows[i].IsCaptain != (player?.IsCaptain == true)) return false;
        }
        return true;
    }

    internal void CaptainChosen(PlayerRow chosen)
    {
        foreach (var row in Rows)
        {
            if (!ReferenceEquals(row, chosen) && row.IsCaptain) row.ClearCaptain();
        }
    }
}

// "Create a team" with its players and logo in one go, as on v2's registry and
// tournament pages.
public sealed class NewTeamForm : ObservableObject
{
    private string _name = "";
    private string _tag = "";
    private string? _logoPath;

    public NewTeamForm()
    {
        ChooseLogoCommand = new RelayCommand(() =>
        {
            var path = Dialogs.PickImage();
            if (path is not null) LogoPath = path;
        });
        ClearCommand = new RelayCommand(Clear);
        Loc.Instance.Changed += () => OnPropertyChanged(nameof(LogoText));
    }

    public PlayerEditor Players { get; } = new();
    public string Name { get => _name; set => Set(ref _name, value ?? ""); }
    public string Tag { get => _tag; set => Set(ref _tag, value ?? ""); }

    public string? LogoPath
    {
        get => _logoPath;
        private set
        {
            if (!Set(ref _logoPath, value)) return;
            OnPropertyChanged(nameof(LogoText));
            OnPropertyChanged(nameof(HasLogo));
        }
    }

    public bool HasLogo => LogoPath is not null;

    public string LogoText
    {
        get
        {
            if (LogoPath is null) return Loc.T("Team.ChooseLogo");
            var file = Path.GetFileName(LogoPath);
            return Loc.F("Team.LogoChosen", file.Length > 18 ? file[..18] + "…" : file);
        }
    }

    public ICommand ChooseLogoCommand { get; }
    public ICommand ClearCommand { get; }

    public void Clear()
    {
        Name = "";
        Tag = "";
        LogoPath = null;
        Players.Clear();
    }

    // Returns null when nothing was created. A failed logo upload still returns the
    // team: it exists, and the operator is told the logo needs another try.
    public async Task<Team?> CreateAsync(AppServices services)
    {
        var name = Name.Trim();
        if (name.Length == 0)
        {
            Toasts.Error(Loc.T("Team.NameRequired"));
            return null;
        }

        var reply = await services.Api.PostAsync<TeamReply>("/api/teams",
            new { name, tag = Tag.Trim(), players = Players.Read() });

        if (LogoPath is not null)
        {
            try
            {
                await TeamMedia.UploadLogoAsync(services.Api, reply.Team.Id, LogoPath);
            }
            catch (Exception error)
            {
                Toasts.Error(Loc.F("Team.LogoFailedAfterCreate", reply.Team.Name, error.Message));
            }
        }
        return reply.Team;
    }
}

// Editing an existing team: name, tag, players, logo, and the seed when it is edited
// from inside a tournament. The owning screen supplies what each button does.
public sealed class TeamEditor : ObservableObject
{
    private string _name = "";
    private string _tag = "";
    private string _seed = "0";

    public TeamEditor(bool showSeed, Func<TeamEditor, Task> save, Func<Task> uploadLogo, Func<Task> clearLogo, Func<Task> delete)
    {
        ShowSeed = showSeed;
        SaveCommand = new AsyncRelayCommand(() => save(this), () => Name.Trim().Length > 0);
        UploadLogoCommand = new AsyncRelayCommand(uploadLogo);
        ClearLogoCommand = new AsyncRelayCommand(clearLogo);
        DeleteCommand = new AsyncRelayCommand(delete);
    }

    public PlayerEditor Players { get; } = new();
    public bool ShowSeed { get; }
    public string Name { get => _name; set => Set(ref _name, value ?? ""); }
    public string Tag { get => _tag; set => Set(ref _tag, value ?? ""); }
    public string Seed { get => _seed; set => Set(ref _seed, value ?? ""); }
    public int SeedValue => int.TryParse(Seed.Trim(), out var n) ? Math.Clamp(n, 0, 9999) : 0;

    public ICommand SaveCommand { get; }
    public ICommand UploadLogoCommand { get; }
    public ICommand ClearLogoCommand { get; }
    public ICommand DeleteCommand { get; }

    public void Load(Team team)
    {
        Name = team.Name;
        Tag = team.Tag ?? "";
        Seed = team.Seed.ToString();
        Players.Load(team.Players);
    }

    // True when the operator has typed something not yet saved. Used so a change
    // pushed from elsewhere never overwrites a form someone is filling in.
    public bool Differs(Team team) =>
        Name != team.Name || Tag != (team.Tag ?? "") || (ShowSeed && SeedValue != team.Seed) || !Players.Matches(team.Players);

    public object Body() => new { name = Name, tag = Tag, players = Players.Read() };
}

// Name, tag and logo as every team row shows them.
public abstract class TeamFace : ObservableObject
{
    private string _id = "";
    private string _name = "";
    private string _tag = "";
    private string? _logoUrl;
    private string _initials = "";
    private string _playersText = "";

    protected TeamFace(AppServices services) => Services = services;

    protected AppServices Services { get; }
    public Team? Team { get; private set; }
    public string Id { get => _id; private set => Set(ref _id, value); }
    public string Name { get => _name; private set => Set(ref _name, value); }

    public string Tag
    {
        get => _tag;
        private set
        {
            if (Set(ref _tag, value)) OnPropertyChanged(nameof(HasTag));
        }
    }

    public bool HasTag => Tag.Length > 0;
    public string? LogoUrl { get => _logoUrl; private set => Set(ref _logoUrl, value); }
    public string Initials { get => _initials; private set => Set(ref _initials, value); }
    public string PlayersText { get => _playersText; private set => Set(ref _playersText, value); }

    public virtual void Apply(Team team)
    {
        Team = team;
        Id = team.Id;
        Name = team.Name;
        Tag = team.Tag ?? "";
        LogoUrl = TeamMedia.LogoUrl(Services, team);
        Initials = TeamMedia.Initials(team);
        RefreshText();
    }

    public virtual void RefreshText()
    {
        if (Team is null) return;
        var players = Team.Players ?? [];
        var named = players.Count(p => !string.IsNullOrWhiteSpace(p.Name));
        PlayersText = Loc.F("Team.Players", named, Math.Max(players.Count, PlayerEditor.RosterSize));
    }
}

// Team actions that behave the same wherever they are pressed.
public static class TeamActions
{
    private static string E(string value) => Uri.EscapeDataString(value);

    public static async Task SaveAsync(AppServices services, string teamId, TeamEditor editor)
    {
        await services.Api.PutAsync<TeamReply>($"/api/teams/{E(teamId)}", editor.Body());
        Toasts.Info(Loc.T("Team.Saved"));
    }

    public static async Task UploadLogoAsync(AppServices services, string teamId)
    {
        var path = Dialogs.PickImage();
        if (path is null) return;
        await TeamMedia.UploadLogoAsync(services.Api, teamId, path);
        Toasts.Info(Loc.T("Team.LogoUploaded"));
    }

    public static async Task ClearLogoAsync(AppServices services, string teamId)
    {
        await services.Api.DeleteAsync<TeamReply>($"/api/teams/{E(teamId)}/logo");
        Toasts.Info(Loc.T("Team.LogoCleared"));
    }

    // Returns true when the team was deleted.
    public static async Task<bool> DeleteAsync(AppServices services, string teamId, string name, bool enteredTournaments)
    {
        var body = new List<string> { Loc.F("Team.DeleteQ", name) };
        if (enteredTournaments) body.Add(Loc.T("Team.DeleteDropped"));
        if (!Dialogs.Confirm(Loc.T("Team.DeleteTitle"), body, Loc.T("Common.DeleteForever"), danger: true)) return false;

        await services.Api.DeleteAsync<OkReply>($"/api/teams/{E(teamId)}");
        Toasts.Info(Loc.T("Team.Deleted"));
        return true;
    }
}

// Round names as the bracket and the team history print them.
public static class Rounds
{
    public static string Label(string bracket, int round) => bracket switch
    {
        "grand" => Loc.T(round == 2 ? "Round.GrandReset" : "Round.Grand"),
        "losers" => Loc.F("Round.Losers", round),
        "main" => Loc.F("Round.Main", round),
        "playoff" => Loc.F("Round.Playoff", round),
        _ => Loc.F("Round.Group", bracket, round)
    };
}
