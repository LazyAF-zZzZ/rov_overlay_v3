using System.Collections.ObjectModel;
using System.Windows.Input;
using RovOverlay.Desktop.Core;
using RovOverlay.Desktop.Models;
using RovOverlay.Desktop.Services;

namespace RovOverlay.Desktop.ViewModels;

public sealed record HeroSuggestion(string Name, string IconUrl);

// One hero box: a pick or a ban. Typing filters the hero list, Enter or a click
// commits, and the box refuses a hero already taken elsewhere on the board.
public sealed class HeroSlot : ObservableObject
{
    private readonly ControlViewModel _owner;
    private string _text = "";
    private string? _hero;
    private bool _isActive;
    private bool _isOpen;
    private int _highlight = -1;

    public HeroSlot(ControlViewModel owner, string team, string kind, int index, string label)
    {
        _owner = owner;
        Team = team;
        Kind = kind;                       // "Pick" or "Ban"
        Index = index;
        Label = label;
        SlotId = $"{(team == "teamBlue" ? "blue" : "red")}{kind}{index}";
        ClearCommand = new RelayCommand(() =>
        {
            Text = "";
            Commit(preferMatch: true);
        });
        ChooseCommand = new RelayCommand(p =>
        {
            if (p is HeroSuggestion suggestion) Choose(suggestion);
        });
    }

    public string Team { get; }
    public string Kind { get; }
    public int Index { get; }
    public string Label { get; }
    public string SlotId { get; }
    public bool IsBan => Kind == "Ban";
    public ICommand ClearCommand { get; }
    public ICommand ChooseCommand { get; }

    public ObservableCollection<HeroSuggestion> Suggestions { get; } = new();

    // Raised when the draft moves to this slot, so the view can put the cursor here.
    public event Action? FocusRequested;

    public string Text
    {
        get => _text;
        set => Set(ref _text, value ?? "");
    }

    public string? Hero
    {
        get => _hero;
        private set
        {
            if (!Set(ref _hero, value)) return;
            OnPropertyChanged(nameof(IconUrl));
            OnPropertyChanged(nameof(HasHero));
        }
    }

    public string? IconUrl => Hero is null ? null : _owner.HeroIconUrl(Hero);
    public bool HasHero => Hero is not null;

    // เลือกไว้แล้วแต่ยังไม่ยืนยัน
    //
    // ผู้ใช้ขอมา: ระหว่างที่ทีมสลับตัวกันไปมา อยากให้ภาพขึ้นจอตามได้เลย
    // แต่เสียงกับอนิเมชันของการพิคต้องรอจนกดยืนยัน เพราะนั่นคือจังหวะที่ล็อกจริง
    private bool _isPending;
    private ICommand? _confirm;

    public bool IsPending { get => _isPending; private set => Set(ref _isPending, value); }

    public ICommand ConfirmCommand => _confirm ??= new RelayCommand(() => _owner.ConfirmPick(this));

    // The slot the draft is waiting on right now.
    public bool IsActive { get => _isActive; set => Set(ref _isActive, value); }

    public bool IsOpen { get => _isOpen; set => Set(ref _isOpen, value); }

    public int Highlight
    {
        get => _highlight;
        set => Set(ref _highlight, value);
    }

    // Called when the server's state arrives: never fights what is being typed.
    // pending มีความหมายเฉพาะกับพิค แบนไม่มีขั้นยืนยัน จึงปล่อยค่าเริ่มต้นไว้
    public void Apply(string? hero, bool editing, bool pending = false)
    {
        Hero = hero;
        IsPending = pending;
        if (!editing) Text = hero ?? "";
    }

    public void OpenSuggestions()
    {
        var matches = _owner.MatchHeroes(Text, SlotId);
        Suggestions.Clear();
        foreach (var name in matches.Take(200)) Suggestions.Add(new HeroSuggestion(name, _owner.HeroIconUrl(name)));
        Highlight = Suggestions.Count > 0 ? 0 : -1;
        IsOpen = Suggestions.Count > 0;
    }

    public void Close()
    {
        IsOpen = false;
        Highlight = -1;
    }

    public void Move(int step)
    {
        if (!IsOpen || Suggestions.Count == 0) return;
        Highlight = (Highlight + step + Suggestions.Count) % Suggestions.Count;
    }

    public void ChooseHighlighted()
    {
        if (Highlight >= 0 && Highlight < Suggestions.Count) Choose(Suggestions[Highlight]);
        else Commit(preferMatch: true);
    }

    public void Choose(HeroSuggestion suggestion)
    {
        Text = suggestion.Name;
        Close();
        Commit(preferMatch: true);
    }

    public void Cancel()
    {
        Text = Hero ?? "";
        Close();
    }

    // preferMatch: Enter or a click on a suggestion, which re-sends even when the value
    // did not change, so a hero the server rejected can be sent again.
    public void Commit(bool preferMatch = false)
    {
        var typed = Text.Trim();
        if (typed.Length == 0)
        {
            if (Hero is null && !preferMatch) return;
            Hero = null;
            Text = "";
            _owner.SendHero(this, null);
            return;
        }

        var hero = _owner.ResolveHero(typed);
        if (hero is null)
        {
            Text = Hero ?? "";
            Toasts.Error(Loc.T("Control.HeroNotFound"));
            return;
        }
        if (_owner.IsTaken(hero, SlotId))
        {
            Text = Hero ?? "";
            Toasts.Error(Loc.F("Control.HeroTaken", hero));
            return;
        }

        var changed = hero != Hero;
        Hero = hero;
        Text = hero;
        if (changed || preferMatch) _owner.SendHero(this, hero);
    }

    public void RequestFocus() => FocusRequested?.Invoke();
}

// One player row: nickname, lane, and the two-step swap button.
public sealed class PlayerSlot : ObservableObject
{
    private readonly SideViewModel _side;
    private readonly Debouncer _save = new();
    private string _name = "";
    private string _position = "";
    private bool _isSwapping;

    public PlayerSlot(SideViewModel side, int index)
    {
        _side = side;
        Index = index;
        SwapCommand = new RelayCommand(() => side.SwapPlayer(this));
    }

    public int Index { get; }
    public int Number => Index + 1;
    public string Placeholder => Loc.F("Team.PlayerN", Number);
    public ICommand SwapCommand { get; }
    public static IReadOnlyList<PositionChoice> Positions => PlayerEditor.Positions;

    public string Name
    {
        get => _name;
        set
        {
            if (!Set(ref _name, value ?? "")) return;
            _save.Run(() => _side.Owner.Emit("updatePlayerName", new { team = _side.Key, index = Index, name = Name }));
            _side.FlashSaved();
        }
    }

    public string Position
    {
        get => _position;
        set
        {
            if (!Set(ref _position, value ?? "")) return;
            _side.Owner.Emit("updatePlayerPosition", new { team = _side.Key, index = Index, position = Position });
        }
    }

    public bool IsSwapping
    {
        get => _isSwapping;
        set
        {
            if (Set(ref _isSwapping, value)) OnPropertyChanged(nameof(SwapLabel));
        }
    }

    public string SwapLabel => Loc.T(IsSwapping ? "Control.Cancel" : "Control.Swap");

    public void Apply(string name, string position, bool editingName, bool editingPosition)
    {
        if (!editingName && name != _name)
        {
            _name = name;
            OnPropertyChanged(nameof(Name));
        }
        if (!editingPosition && position != _position)
        {
            _position = position;
            OnPropertyChanged(nameof(Position));
        }
    }

    public void SetNameLocally(string name)
    {
        _name = name;
        OnPropertyChanged(nameof(Name));
    }

    public void RefreshText()
    {
        OnPropertyChanged(nameof(Placeholder));
        OnPropertyChanged(nameof(SwapLabel));
    }
}

// One side of the board: blue or red.
public sealed class SideViewModel : ObservableObject
{
    private readonly Debouncer _nameSave = new();
    private readonly Debouncer _scoreSave = new();
    private string _name = "";
    private string _scoreText = "0";
    private string? _logoUrl;
    private bool _saved;
    private TeamChoice? _loadTeam;

    public SideViewModel(ControlViewModel owner, string key)
    {
        Owner = owner;
        Key = key;
        IsBlue = key == "teamBlue";
        Players = Enumerable.Range(0, ControlState.PickCount).Select(i => new PlayerSlot(this, i)).ToList();
        Picks = Enumerable.Range(0, ControlState.PickCount)
            .Select(i => new HeroSlot(owner, key, "Pick", i, Loc.F("Control.PickN", i + 1))).ToList();
        Bans = Enumerable.Range(0, ControlState.BanCount)
            .Select(i => new HeroSlot(owner, key, "Ban", i, Loc.F("Control.BanN", i + 1))).ToList();
        // One row per player: nickname, lane and that player's pick, so the board reads
        // across the way the operator thinks about it.
        Rows = Players.Select((player, i) => new BoardRow(player, Picks[i])).ToList();

        UploadLogoCommand = new AsyncRelayCommand(UploadLogoAsync);
        ClearLogoCommand = new AsyncRelayCommand(ClearLogoAsync);
        // Busy while the server works, so a quick double press cannot count two games.
        AddPointCommand = new AsyncRelayCommand(() => owner.FinishGameAsync(IsBlue ? "blue" : "red"));
    }

    public ICommand AddPointCommand { get; }

    // What screen readers and UI automation call the +1 button: "+1 PSG Esports".
    public string AddPointName => Loc.F("Flow.AddPointName", ControlViewModel.SideName(this));

    public ControlViewModel Owner { get; }
    public string Key { get; }
    public bool IsBlue { get; }
    public string Title => Loc.T(IsBlue ? "Control.BlueTeam" : "Control.RedTeam");
    public IReadOnlyList<PlayerSlot> Players { get; }
    public IReadOnlyList<HeroSlot> Picks { get; }
    public IReadOnlyList<HeroSlot> Bans { get; }
    public IReadOnlyList<BoardRow> Rows { get; }
    public ICommand UploadLogoCommand { get; }
    public ICommand ClearLogoCommand { get; }

    public string Name
    {
        get => _name;
        set
        {
            if (!Set(ref _name, value ?? "")) return;
            OnPropertyChanged(nameof(AddPointName));
            _nameSave.Run(() => Owner.Emit("updateTeamName", new { team = Key, name = Name }));
            FlashSaved();
        }
    }

    public string ScoreText
    {
        get => _scoreText;
        set
        {
            if (!Set(ref _scoreText, value ?? "")) return;
            var score = int.TryParse(_scoreText.Trim(), out var n) ? Math.Clamp(n, 0, 99) : 0;
            _scoreSave.Run(() => Owner.Emit("updateScore", new { team = Key, score }));
            FlashSaved();
        }
    }

    public string? LogoUrl { get => _logoUrl; private set => Set(ref _logoUrl, value); }

    // The registry picker: choosing a team fills this side with its name, logo and roster.
    public TeamChoice? LoadTeam
    {
        get => _loadTeam;
        set
        {
            if (!Set(ref _loadTeam, value) || value is null) return;
            _ = LoadFromRegistryAsync(value);
        }
    }

    public bool Saved
    {
        get => _saved;
        private set
        {
            if (Set(ref _saved, value)) OnPropertyChanged(nameof(SaveText));
        }
    }

    public string SaveText => Loc.T(Saved ? "Control.Saved" : "Control.AutoSaves");

    public void FlashSaved()
    {
        Saved = true;
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.4) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Saved = false;
        };
        timer.Start();
    }

    private async Task LoadFromRegistryAsync(TeamChoice choice)
    {
        try
        {
            await Owner.Services.Api.PostAsync<OkReply>($"/api/teams/{Uri.EscapeDataString(choice.Id)}/live", new { team = Key });
            Toasts.Info(Loc.T("Control.TeamLoaded"));
        }
        catch (Exception error)
        {
            Toasts.Error(error.Message);
        }
        finally
        {
            _loadTeam = null;
            OnPropertyChanged(nameof(LoadTeam));
        }
    }

    private async Task UploadLogoAsync()
    {
        var path = Dialogs.PickImage();
        if (path is null) return;
        if (!TeamMediaSide.TryType(path, out var type))
        {
            Toasts.Error(Loc.T("Team.LogoType"));
            return;
        }
        if (new System.IO.FileInfo(path).Length > TeamMedia.MaxBytes)
        {
            Toasts.Error(Loc.T("Team.LogoTooBig"));
            return;
        }
        var bytes = await System.IO.File.ReadAllBytesAsync(path);
        await Owner.Services.Api.PostBytesAsync<OkReply>($"/api/team-logo/{Key}", bytes, type);
        Toasts.Info(Loc.T("Team.LogoUploaded"));
    }

    private async Task ClearLogoAsync()
    {
        await Owner.Services.Api.DeleteAsync<OkReply>($"/api/team-logo/{Key}");
        Toasts.Info(Loc.T("Team.LogoCleared"));
    }

    public void Apply(SideState side, Func<object, bool> isEditing)
    {
        if (!isEditing(this) && side.Name != _name)
        {
            _name = side.Name;
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(AddPointName));
        }
        var score = side.Score.ToString();
        if (!isEditing(this) && score != _scoreText)
        {
            _scoreText = score;
            OnPropertyChanged(nameof(ScoreText));
        }

        // The side's logo is its own file (blue-team / red-team), or the registry team's
        // file when this side was filled from the registry.
        LogoUrl = side.LogoVersion > 0 && side.LogoExt.Length > 0
            ? Owner.Services.Url($"/images/team-logos/{side.LogoSource ?? (IsBlue ? "blue-team" : "red-team")}.{side.LogoExt}?v={side.LogoVersion}")
            : null;

        for (var i = 0; i < Players.Count; i++)
            Players[i].Apply(side.Players.ElementAtOrDefault(i) ?? "", side.Positions.ElementAtOrDefault(i) ?? "",
                isEditing(Players[i]), isEditing(Players[i]));

        for (var i = 0; i < Picks.Count; i++)
            Picks[i].Apply(side.Picks.ElementAtOrDefault(i), isEditing(Picks[i]), side.PicksPending.ElementAtOrDefault(i));
        for (var i = 0; i < Bans.Count; i++) Bans[i].Apply(side.Bans.ElementAtOrDefault(i), isEditing(Bans[i]));
    }

    private PlayerSlot? _swapFrom;

    // Two taps: the first arms a row, the second swaps the two nicknames.
    public void SwapPlayer(PlayerSlot slot)
    {
        if (_swapFrom is null)
        {
            _swapFrom = slot;
            slot.IsSwapping = true;
            return;
        }
        var from = _swapFrom;
        _swapFrom = null;
        from.IsSwapping = false;
        if (ReferenceEquals(from, slot)) return;

        (from.Name, slot.Name) = (slot.Name, from.Name);
        Toasts.Info(Loc.F("Control.PlayerSwapped", from.Number, slot.Number));
    }

    private HeroSlot? _pickSwapFrom;

    public void SwapPick(HeroSlot slot)
    {
        if (_pickSwapFrom is null)
        {
            _pickSwapFrom = slot;
            slot.IsActive = slot.IsActive; // no visual change; the button carries the state
            OnPropertyChanged(nameof(PickSwapFromIndex));
            return;
        }
        var from = _pickSwapFrom;
        _pickSwapFrom = null;
        OnPropertyChanged(nameof(PickSwapFromIndex));
        if (ReferenceEquals(from, slot)) return;

        Owner.Emit("swapPicks", new { team = Key, index1 = from.Index, index2 = slot.Index });
        Toasts.Info(Loc.F("Control.PickSwapped", from.Index + 1, slot.Index + 1));
    }

    public int PickSwapFromIndex => _pickSwapFrom?.Index ?? -1;

    public void RefreshText()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(SaveText));
        foreach (var player in Players) player.RefreshText();
    }
}

internal static class TeamMediaSide
{
    public static bool TryType(string path, out string type)
    {
        type = System.IO.Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => ""
        };
        return type.Length > 0;
    }
}

public sealed record BoardRow(PlayerSlot Player, HeroSlot Pick);

public sealed class SeqBadge(string label, int index) : ObservableObject
{
    private bool _isCurrent;
    private bool _isDone;

    public string Text { get; } = $"{index + 1}. {label}";
    public bool IsCurrent { get => _isCurrent; set => Set(ref _isCurrent, value); }
    public bool IsDone { get => _isDone; set => Set(ref _isDone, value); }
}

public sealed class SfxRow : ObservableObject
{
    private readonly ControlViewModel _owner;
    private readonly Debouncer _send = new(150);
    private double _value = 100;

    public SfxRow(ControlViewModel owner, string key)
    {
        _owner = owner;
        Key = key;
        TestCommand = new RelayCommand(() => owner.TestSound(Key, Value / 100));
    }

    public string Key { get; }
    public string Label => Loc.T("Sfx." + Key);
    public string Note => Loc.T("Sfx." + Key + ".note");
    public ICommand TestCommand { get; }

    public double Value
    {
        get => _value;
        set
        {
            if (!Set(ref _value, value)) return;
            OnPropertyChanged(nameof(ValueText));
            _send.Run(() => _owner.Emit("updateSfx", new Dictionary<string, double> { [Key] = Math.Round(Value) / 100 }));
        }
    }

    public string ValueText => $"{Math.Round(Value)}%";

    public void Apply(double level)
    {
        var percent = Math.Round(level * 100);
        if (Math.Abs(percent - _value) < 0.5) return;
        _value = percent;
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(ValueText));
    }

    public void RefreshText()
    {
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(Note));
    }
}

public sealed record HintRow(string Keys, string Label);
