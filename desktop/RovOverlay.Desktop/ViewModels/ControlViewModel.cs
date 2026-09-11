using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using System.Windows.Input;
using RovOverlay.Desktop.Core;
using RovOverlay.Desktop.Models;
using RovOverlay.Desktop.Services;

namespace RovOverlay.Desktop.ViewModels;

// The Control Panel: the screen the operator drives the broadcast from.
//
// Everything here is a thin layer over the socket commands v2's control.js sent, so the
// overlays, the draft engine and the recorded drafts behave exactly as before. The
// server owns the state; this screen sends commands and redraws from "stateUpdate".
public sealed class ControlViewModel : ObservableObject
{
    private static readonly string[] SfxKeys = ["pick", "ban", "timer"];

    private readonly Debouncer _matchSave = new();
    private List<string> _heroes = [];
    private ControlState? _state;
    private string _tournament = "";
    private string _matchTitle = "";
    private bool _matchSaved;
    private int _lastFocusedPhase = int.MinValue;
    private string? _liveText;

    public ControlViewModel(AppServices services)
    {
        Services = services;
        Blue = new SideViewModel(this, "teamBlue");
        Red = new SideViewModel(this, "teamRed");
        foreach (var key in SfxKeys) SfxRows.Add(new SfxRow(this, key));

        StartCommand = new RelayCommand(() => Emit("draftStart"));
        PauseCommand = new RelayCommand(() => Emit("draftPause"));
        ResumeCommand = new RelayCommand(() => Emit("draftResume"));
        NextCommand = new RelayCommand(() => Emit("draftNext"));
        PrevCommand = new RelayCommand(() => Emit("draftPrev"));
        ResetDraftCommand = new RelayCommand(() => Emit("draftReset"));
        UndoCommand = new RelayCommand(() => Emit("undo"));
        SwitchTeamsCommand = new RelayCommand(() =>
        {
            Emit("switchTeams");
            Toasts.Info(Loc.T("Control.TeamsSwitched"));
        });
        ClearAllCommand = new RelayCommand(ClearAll);
        ResetMatchCommand = new AsyncRelayCommand(ResetMatchAsync);
        SetSizeCommand = new RelayCommand(p => Emit("updateOverlaySize", new { size = p as string ?? "1080" }));
        ShowBannerCommand = new RelayCommand(() => Emit("setOverlayVisible", new { visible = true }));
        HideBannerCommand = new RelayCommand(() => Emit("setOverlayVisible", new { visible = false }));
        StepRoundCommand = new RelayCommand(p => Emit("stepRound", new { delta = p is "-1" ? -1 : 1 }));
        SwapPickCommand = new RelayCommand(p =>
        {
            if (p is HeroSlot slot) (slot.Team == "teamBlue" ? Blue : Red).SwapPick(slot);
        });

        services.StateUpdated += OnState;
        services.DataChanged += OnDataChanged;
        Loc.Instance.Changed += OnLanguageChanged;

        _ = LoadAsync();
        if (services.LastState is not null) OnState(services.LastState);
    }

    public AppServices Services { get; }
    public SideViewModel Blue { get; }
    public SideViewModel Red { get; }
    public ObservableCollection<SeqBadge> Sequence { get; } = new();
    public ObservableCollection<SfxRow> SfxRows { get; } = new();
    public ObservableCollection<TeamChoice> Teams { get; } = new();
    public ObservableCollection<HintRow> Hints { get; } = new();

    public ICommand StartCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand ResumeCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand PrevCommand { get; }
    public ICommand ResetDraftCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand SwitchTeamsCommand { get; }
    public ICommand ClearAllCommand { get; }
    public ICommand ResetMatchCommand { get; }
    public ICommand SetSizeCommand { get; }
    public ICommand ShowBannerCommand { get; }
    public ICommand HideBannerCommand { get; }
    public ICommand StepRoundCommand { get; }
    public ICommand SwapPickCommand { get; }

    // What the view says is being typed in right now, so a push from the server never
    // overwrites a field under the operator's hands.
    public Func<object, bool> IsEditing { get; set; } = _ => false;

    // ---- match info -------------------------------------------------------

    public string Tournament
    {
        get => _tournament;
        set
        {
            if (!Set(ref _tournament, value ?? "")) return;
            SaveMatchInfo();
        }
    }

    public string MatchTitle
    {
        get => _matchTitle;
        set
        {
            if (!Set(ref _matchTitle, value ?? "")) return;
            SaveMatchInfo();
        }
    }

    public string MatchSaveText => Loc.T(_matchSaved ? "Control.Saved" : "Control.AutoSaves");

    private void SaveMatchInfo()
    {
        _matchSave.Run(() => Emit("updateMatchInfo", new { tournament = Tournament, title = MatchTitle }));
        _matchSaved = true;
        OnPropertyChanged(nameof(MatchSaveText));
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.4) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _matchSaved = false;
            OnPropertyChanged(nameof(MatchSaveText));
        };
        timer.Start();
    }

    // ---- draft ------------------------------------------------------------

    public string PhaseLabel => _state is null || _state.DraftIdle ? Loc.T("Control.Ready") : _state.DraftLabel.ToUpperInvariant();
    public string TimerText => _state is null || _state.DraftIdle ? "--" : _state.Timer is { Length: > 0 } t ? t : "--";
    public bool IsUrgent => Seconds(TimerText) is > 0 and <= 10;
    public string PhaseIndexText => Loc.F("Control.PhaseN", Math.Max(_state?.DraftPhaseIndex ?? -1, -1) + 1, Sequence.Count);
    public bool IsRunning => _state?.DraftRunning == true;

    public string StatusText
    {
        get
        {
            if (_state is null) return Loc.T("Control.Ready");
            var index = _state.DraftPhaseIndex;
            if (index < 0) return Loc.T("Control.Ready");
            if (index >= Sequence.Count && Sequence.Count > 0) return Loc.T("Control.Done");
            return $"{_state.DraftLabel} — {Loc.T(IsRunning ? "Control.Running" : "Control.Paused")}";
        }
    }

    public string RoundText => (_state?.Round ?? 1).ToString();
    public bool CanPrevRound => (_state?.Round ?? 1) > 1;
    public string? RoundNote => _state is { RoundsOnBoard: > 0 } s ? Loc.F("Control.RoundsOnBoard", s.RoundsOnBoard) : null;

    // ---- overlay switches -------------------------------------------------

    public bool Is1080 => (_state?.OverlaySize ?? "1080") == "1080";
    public bool Is1440 => _state?.OverlaySize == "1440";
    public bool BannerOn => _state?.OverlayVisible != false;

    // ---- live match -------------------------------------------------------

    public string? LiveText { get => _liveText; private set => Set(ref _liveText, value); }
    public bool HasLive => LiveText is not null;

    // ---- heroes -----------------------------------------------------------

    public string HeroIconUrl(string hero) => Services.Url($"/images/heroes-icons/{Uri.EscapeDataString(hero)}.png");

    // Exact name, then a name starting with what was typed, then any word of a name:
    // the same rule as v2, so muscle memory carries over.
    public string? ResolveHero(string typed)
    {
        var lower = typed.Trim().ToLowerInvariant();
        if (lower.Length == 0) return null;
        return _heroes.FirstOrDefault(h => h.ToLowerInvariant() == lower)
               ?? _heroes.FirstOrDefault(h => h.ToLowerInvariant().StartsWith(lower))
               ?? _heroes.FirstOrDefault(h => StartsWithAnyWord(h, lower));
    }

    public IEnumerable<string> MatchHeroes(string query, string exceptSlotId)
    {
        var taken = TakenHeroes(exceptSlotId);
        var available = _heroes.Where(h => !taken.Contains(h)).ToList();
        var lower = query.Trim().ToLowerInvariant();
        if (lower.Length == 0) return available;

        var prefix = available.Where(h => h.ToLowerInvariant().StartsWith(lower));
        var word = available.Where(h => !h.ToLowerInvariant().StartsWith(lower) && StartsWithAnyWord(h, lower));
        return prefix.Concat(word);
    }

    public bool IsTaken(string hero, string exceptSlotId) => TakenHeroes(exceptSlotId).Contains(hero);

    private HashSet<string> TakenHeroes(string exceptSlotId)
    {
        var taken = new HashSet<string>();
        foreach (var slot in AllSlots())
        {
            if (slot.SlotId == exceptSlotId || slot.Hero is null) continue;
            taken.Add(slot.Hero);
        }
        return taken;
    }

    private static bool StartsWithAnyWord(string hero, string lower) =>
        hero.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(word => word.StartsWith(lower));

    private IEnumerable<HeroSlot> AllSlots() =>
        Blue.Picks.Concat(Blue.Bans).Concat(Red.Picks).Concat(Red.Bans);

    public void SendHero(HeroSlot slot, string? hero) =>
        Emit(slot.IsBan ? "updateBan" : "updatePick", new { team = slot.Team, index = slot.Index, hero });

    // ---- wiring -----------------------------------------------------------

    public void Emit(string command, object? payload = null) => _ = Services.Socket?.EmitAsync(command, payload);

    private async Task LoadAsync()
    {
        try
        {
            _heroes = (await Services.Api.GetAsync<HeroesReply>("/api/heroes")).Heroes ?? [];
        }
        catch (Exception error)
        {
            Toasts.Error(error.Message);
        }

        try
        {
            var sequence = (await Services.Api.GetAsync<DraftSequenceReply>("/api/draft-sequence")).Sequence ?? [];
            Sequence.Clear();
            for (var i = 0; i < sequence.Count; i++) Sequence.Add(new SeqBadge(sequence[i].Label, i));
            OnPropertyChanged(nameof(PhaseIndexText));
        }
        catch (Exception error)
        {
            Toasts.Error(error.Message);
        }

        if (Services.LastState is null)
        {
            try { OnState(JsonNode.Parse(await Services.Api.GetTextAsync("/api/state"))!); }
            catch { /* the next stateUpdate will bring it */ }
        }

        await LoadTeamsAsync();
        await RefreshLiveAsync();
    }

    private async Task LoadTeamsAsync()
    {
        try
        {
            var teams = (await Services.Api.GetAsync<TeamList>("/api/teams")).Teams ?? [];
            Teams.Clear();
            foreach (var team in teams)
                Teams.Add(new TeamChoice(team.Id, string.IsNullOrWhiteSpace(team.Tag) ? team.Name : $"{team.Name} ({team.Tag})"));
        }
        catch
        {
            // No registry list is a missing convenience, not a broken panel.
        }
    }

    private async Task RefreshLiveAsync()
    {
        try
        {
            var live = (await Services.Api.GetAsync<LiveResponse>("/api/live-match")).Live;
            LiveText = live.MatchId is null
                ? null
                : live.GameNo is int game
                    ? Loc.F("Live.Match", live.TournamentName ?? "", live.MatchLabel ?? "", game)
                    : Loc.F("Live.MatchNoGame", live.TournamentName ?? "", live.MatchLabel ?? "");
            OnPropertyChanged(nameof(HasLive));
        }
        catch
        {
            // Leave the bar as it was.
        }
    }

    private void OnDataChanged(DataChange change)
    {
        if (change.Topic == "teams") _ = LoadTeamsAsync();
        if (change.Topic is "live" or "games" or "matches") _ = RefreshLiveAsync();
    }

    private void OnState(JsonNode node)
    {
        var state = ControlState.From(node);
        _state = state;

        if (!IsEditing(this))
        {
            if (state.Tournament != _tournament)
            {
                _tournament = state.Tournament;
                OnPropertyChanged(nameof(Tournament));
            }
            if (state.MatchTitle != _matchTitle)
            {
                _matchTitle = state.MatchTitle;
                OnPropertyChanged(nameof(MatchTitle));
            }
        }

        Blue.Apply(state.Blue, IsEditing);
        Red.Apply(state.Red, IsEditing);

        var active = state.ActiveSlots.ToHashSet();
        foreach (var slot in AllSlots()) slot.IsActive = active.Contains(slot.SlotId);

        for (var i = 0; i < Sequence.Count; i++)
        {
            Sequence[i].IsCurrent = i == state.DraftPhaseIndex;
            Sequence[i].IsDone = i < state.DraftPhaseIndex;
        }

        foreach (var row in SfxRows)
        {
            if (state.Sfx.TryGetValue(row.Key, out var level)) row.Apply(level);
        }

        BuildHints(state);

        foreach (var name in new[]
                 {
                     nameof(PhaseLabel), nameof(TimerText), nameof(IsUrgent), nameof(PhaseIndexText), nameof(IsRunning),
                     nameof(StatusText), nameof(RoundText), nameof(CanPrevRound), nameof(RoundNote),
                     nameof(Is1080), nameof(Is1440), nameof(BannerOn)
                 })
            OnPropertyChanged(name);

        // When the draft moves on, put the cursor in the slot it is waiting for.
        if (state.DraftPhaseIndex != _lastFocusedPhase)
        {
            _lastFocusedPhase = state.DraftPhaseIndex;
            FocusActiveSlot(state);
        }
    }

    private void FocusActiveSlot(ControlState state)
    {
        if (state.ActiveSlots.Count == 0) return;
        var slots = AllSlots().Where(s => state.ActiveSlots.Contains(s.SlotId)).ToList();
        if (slots.Count == 0) return;
        (slots.FirstOrDefault(s => s.Hero is null) ?? slots[0]).RequestFocus();
    }

    private void BuildHints(ControlState state)
    {
        Hints.Clear();
        Hints.Add(new HintRow("Enter", Loc.T("Control.HintConfirm")));
        Hints.Add(new HintRow("Esc", Loc.T("Control.HintEscape")));
        foreach (var (action, label) in new[]
                 {
                     ("toggleBanner", "Control.HintBanner"), ("pauseResume", "Control.HintPause"),
                     ("prevPhase", "Control.HintPrev"), ("nextPhase", "Control.HintNext"), ("undo", "Control.HintUndo")
                 })
        {
            if (state.Hotkeys.TryGetValue(action, out var binding)) Hints.Add(new HintRow(binding.Label(), Loc.T(label)));
        }
    }

    public HotkeyBinding? Binding(string action) =>
        _state?.Hotkeys.TryGetValue(action, out var binding) == true ? binding : HotkeyBinding.Defaults.GetValueOrDefault(action);

    public void ToggleBanner() => Emit("setOverlayVisible");
    public void TogglePause()
    {
        if (IsRunning) Emit("draftPause");
        else Emit("draftResume");
    }

    public void TestSound(string key, double volume)
    {
        _ = TestSoundAsync(key, volume);
    }

    private async Task TestSoundAsync(string key, double volume)
    {
        try
        {
            var sounds = await Services.Api.GetAsync<SoundsReply>("/api/sounds");
            if (sounds.Sounds is null || !sounds.Sounds.TryGetValue(key, out var found))
            {
                Toasts.Error(Loc.F("Sfx.Missing", key));
                return;
            }
            SoundTest.Play(new Uri(Services.Url(found.Url)), volume);
        }
        catch (Exception error)
        {
            Toasts.Error(error.Message);
        }
    }

    private void ClearAll()
    {
        var ok = Dialogs.Confirm(Loc.T("Control.ClearTitle"), [Loc.T("Control.ClearBody")], Loc.T("Control.Clear"), danger: true);
        if (!ok) return;
        Emit("clearAll");
        Toasts.Info(Loc.T("Control.Cleared"));
    }

    private async Task ResetMatchAsync()
    {
        var ok = Dialogs.Confirm(Loc.T("Control.ResetTitle"), [Loc.T("Control.ResetBody")], Loc.T("Control.Reset"), danger: true);
        if (!ok) return;
        await Services.Api.PostAsync<OkReply>("/api/reset-state", null);
        Toasts.Info(Loc.T("Control.ResetDone"));
    }

    private static int Seconds(string timer)
    {
        var parts = timer.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[0], out var m) && int.TryParse(parts[1], out var s)) return m * 60 + s;
        return int.TryParse(timer, out var only) ? only : 0;
    }

    private void OnLanguageChanged()
    {
        Blue.RefreshText();
        Red.RefreshText();
        foreach (var row in SfxRows) row.RefreshText();
        if (_state is not null) BuildHints(_state);
        foreach (var name in new[]
                 {
                     nameof(PhaseLabel), nameof(PhaseIndexText), nameof(StatusText), nameof(RoundNote),
                     nameof(MatchSaveText)
                 })
            OnPropertyChanged(name);
        _ = RefreshLiveAsync();
    }
}

public sealed record HeroesReply(List<string>? Heroes);
public sealed record DraftPhaseInfo(string Label, int Seconds, List<string>? Slots);
public sealed record DraftSequenceReply(List<DraftPhaseInfo>? Sequence);
public sealed record FoundSound(string File, string Url, long Bytes);
public sealed record SoundsReply(string? Dir, Dictionary<string, FoundSound>? Sounds);
