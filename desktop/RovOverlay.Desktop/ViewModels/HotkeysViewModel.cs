using System.Text.Json.Nodes;
using System.Windows.Input;
using RovOverlay.Desktop.Core;
using RovOverlay.Desktop.Models;
using RovOverlay.Desktop.Services;

namespace RovOverlay.Desktop.ViewModels;

public sealed class HotkeyRow : ObservableObject
{
    private readonly HotkeysViewModel _owner;
    private HotkeyBinding? _binding;
    private string? _accelerator;
    private bool _held = true;
    private bool _recording;

    public HotkeyRow(HotkeysViewModel owner, string action, string labelKey, string? noteKey, bool isGlobal)
    {
        _owner = owner;
        Action = action;
        LabelKey = labelKey;
        NoteKey = noteKey;
        IsGlobal = isGlobal;
        RecordCommand = new RelayCommand(() => owner.ToggleRecording(this));
        DefaultCommand = new RelayCommand(() => owner.ResetOne(this));
    }

    public string Action { get; }
    public string LabelKey { get; }
    public string? NoteKey { get; }
    public bool IsGlobal { get; }
    public ICommand RecordCommand { get; }
    public ICommand DefaultCommand { get; }

    public string Label => Loc.T(LabelKey);
    public string? Note => NoteKey is null ? null : Loc.T(NoteKey);

    public HotkeyBinding? Binding
    {
        get => _binding;
        set
        {
            _binding = value;
            OnPropertyChanged(nameof(KeyText));
            OnPropertyChanged(nameof(IsChanged));
        }
    }

    public string KeyText => Recording ? Loc.T("Hotkeys.PressAKey") : Binding?.Label() ?? Loc.T("Hotkeys.NotSet");

    public bool IsChanged
    {
        get
        {
            var fallback = IsGlobal ? _owner.GlobalDefault(Action) : HotkeyBinding.Defaults.GetValueOrDefault(Action);
            return Binding is not null && fallback is not null && Binding != fallback;
        }
    }

    public bool Recording
    {
        get => _recording;
        set
        {
            if (!Set(ref _recording, value)) return;
            OnPropertyChanged(nameof(KeyText));
            OnPropertyChanged(nameof(RecordLabel));
        }
    }

    public string RecordLabel => Loc.T(Recording ? "Common.Cancel" : "Hotkeys.Change");

    // System-wide only: what Windows actually gave us.
    public string? Accelerator
    {
        get => _accelerator;
        set
        {
            _accelerator = value;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(IsBlocked));
        }
    }

    public bool Held
    {
        get => _held;
        set
        {
            _held = value;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(IsBlocked));
        }
    }

    public bool IsBlocked => IsGlobal && (Accelerator is null || !Held);

    public string? StatusText
    {
        get
        {
            if (!IsGlobal) return null;
            if (Accelerator is null) return Loc.T("Hotkeys.CannotRegister");
            return Held ? Loc.F("Hotkeys.RegisteredAs", Accelerator) : Loc.F("Hotkeys.TakenBy", Accelerator);
        }
    }

    public void RefreshText()
    {
        foreach (var name in new[] { nameof(Label), nameof(Note), nameof(KeyText), nameof(RecordLabel), nameof(StatusText) })
            OnPropertyChanged(name);
    }
}

// Which key does what: on the Control Panel, and system-wide while another program has
// focus. v2's /hotkeys, rebuilt, with the system-wide half backed by Win32 rather than
// Electron.
public sealed class HotkeysViewModel : ObservableObject, IClosablePage
{
    private static readonly (string Action, string Label, string? Note)[] LocalActions =
    [
        ("toggleBanner", "Hotkeys.ToggleBanner", "Hotkeys.ToggleBannerNote"),
        ("pauseResume", "Hotkeys.PauseResume", null),
        ("prevPhase", "Hotkeys.PrevPhase", null),
        ("nextPhase", "Hotkeys.NextPhase", null),
        ("undo", "Hotkeys.Undo", null)
    ];

    // System-wide adds the score and the rounds: the end of a game is exactly when the
    // operator is in OBS rather than here. The Control Panel's own keys stay without them,
    // because a bare key there would hand out a point on a stray keystroke.
    private static readonly (string Action, string Label, string? Note)[] GlobalActions =
    [
        ("toggleBanner", "Hotkeys.ToggleBanner", null),
        ("pauseResume", "Hotkeys.PauseResume", null),
        ("prevPhase", "Hotkeys.PrevPhase", null),
        ("nextPhase", "Hotkeys.NextPhase", null),
        ("undo", "Hotkeys.Undo", null),
        ("bluePlus", "Hotkeys.BluePlus", "Hotkeys.BluePlusNote"),
        ("redPlus", "Hotkeys.RedPlus", "Hotkeys.RedPlusNote"),
        ("blueMinus", "Hotkeys.BlueMinus", "Hotkeys.BlueMinusNote"),
        ("redMinus", "Hotkeys.RedMinus", "Hotkeys.RedMinusNote"),
        ("prevRound", "Hotkeys.PrevRound", "Hotkeys.RoundNote"),
        ("nextRound", "Hotkeys.NextRound", "Hotkeys.RoundNote")
    ];

    // Same defaults as the server's GLOBAL_HOTKEY_DEFAULTS, so "Default" here and a
    // reset there agree about what original means.
    private static readonly Dictionary<string, HotkeyBinding> GlobalDefaults = new()
    {
        ["toggleBanner"] = new("KeyF", true, false, true, false),
        ["pauseResume"] = new("KeyD", true, false, true, false),
        ["prevPhase"] = new("KeyE", true, false, true, false),
        ["nextPhase"] = new("KeyR", true, false, true, false),
        ["undo"] = new("KeyZ", true, false, true, false),
        ["bluePlus"] = new("Digit1", true, false, true, false),
        ["redPlus"] = new("Digit2", true, false, true, false),
        ["blueMinus"] = new("KeyQ", true, false, true, false),
        ["redMinus"] = new("KeyW", true, false, true, false),
        ["prevRound"] = new("KeyA", true, false, true, false),
        ["nextRound"] = new("KeyS", true, false, true, false)
    };

    private readonly AppServices _s;
    private bool _globalEnabled;
    private bool _applying;
    private HotkeyRow? _recording;

    public HotkeysViewModel(AppServices services)
    {
        _s = services;
        Local = LocalActions.Select(a => new HotkeyRow(this, a.Action, a.Label, a.Note, false)).ToList();
        Global = GlobalActions.Select(a => new HotkeyRow(this, a.Action, a.Label, a.Note, true)).ToList();

        ResetLocalCommand = new RelayCommand(() =>
        {
            StopRecording();
            services.Socket?.EmitAsync("resetHotkeys");
            Toasts.Info(Loc.T("Hotkeys.AllReset"));
        });
        ResetGlobalCommand = new RelayCommand(() =>
        {
            StopRecording();
            services.Socket?.EmitAsync("resetGlobalHotkeys");
            Toasts.Info(Loc.T("Hotkeys.GlobalReset"));
        });

        services.StateUpdated += OnState;
        Loc.Instance.Changed += OnLanguageChanged;
        if (services.LastState is not null) OnState(services.LastState);
        _ = RefreshAcceleratorsAsync();
    }

    public IReadOnlyList<HotkeyRow> Local { get; }
    public IReadOnlyList<HotkeyRow> Global { get; }
    public ICommand ResetLocalCommand { get; }
    public ICommand ResetGlobalCommand { get; }

    public bool GlobalEnabled
    {
        get => _globalEnabled;
        set
        {
            if (!Set(ref _globalEnabled, value) || _applying) return;
            _s.Socket?.EmitAsync("updateGlobalHotkeys", new { enabled = value });
            Toasts.Info(Loc.T(value ? "Hotkeys.GlobalOn" : "Hotkeys.GlobalOff"));
            _ = RefreshAcceleratorsAsync();
        }
    }

    public bool IsRecording => _recording is not null;

    internal HotkeyBinding? GlobalDefault(string action) => GlobalDefaults.GetValueOrDefault(action);

    internal void ToggleRecording(HotkeyRow row)
    {
        if (ReferenceEquals(_recording, row))
        {
            StopRecording();
            return;
        }
        StopRecording();
        _recording = row;
        row.Recording = true;
        OnPropertyChanged(nameof(IsRecording));
    }

    private void StopRecording()
    {
        if (_recording is null) return;
        _recording.Recording = false;
        _recording = null;
        OnPropertyChanged(nameof(IsRecording));
    }

    // Called by the view while a row is recording. Returns true when the key was taken.
    public bool Record(HotkeyBinding binding)
    {
        if (_recording is not { } row) return false;

        if (row.IsGlobal && !binding.Ctrl && !binding.Alt && !binding.Shift && !binding.Meta)
        {
            Toasts.Error(Loc.T("Hotkeys.NeedsModifier"));
            return true;
        }

        Save(row, binding);
        return true;
    }

    public void CancelRecording()
    {
        if (_recording is null) return;
        StopRecording();
        Toasts.Info(Loc.T("Hotkeys.Cancelled"));
    }

    private void Save(HotkeyRow row, HotkeyBinding binding)
    {
        StopRecording();
        row.Binding = binding;

        if (row.IsGlobal)
        {
            _s.Socket?.EmitAsync("updateGlobalHotkeys", new
            {
                bindings = new Dictionary<string, object>
                {
                    [row.Action] = new { code = binding.Code, ctrl = binding.Ctrl, shift = binding.Shift, alt = binding.Alt, meta = binding.Meta }
                }
            });
            _ = RefreshAcceleratorsAsync();
        }
        else
        {
            _s.Socket?.EmitAsync("updateHotkeys", new Dictionary<string, object>
            {
                [row.Action] = new { code = binding.Code, ctrl = binding.Ctrl, shift = binding.Shift, alt = binding.Alt, meta = binding.Meta }
            });
        }

        Toasts.Info($"{row.Label}: {binding.Label()}");
    }

    internal void ResetOne(HotkeyRow row)
    {
        var fallback = row.IsGlobal ? GlobalDefault(row.Action) : HotkeyBinding.Defaults.GetValueOrDefault(row.Action);
        if (fallback is not null) Save(row, fallback);
    }

    private void OnState(JsonNode node)
    {
        _applying = true;
        try
        {
            foreach (var row in Local)
            {
                if (HotkeyBinding.From(node["hotkeys"]?[row.Action]) is { } binding) row.Binding = binding;
            }

            var global = node["globalHotkeys"];
            GlobalEnabled = J.Bool(global?["enabled"]) == true;
            foreach (var row in Global)
            {
                if (HotkeyBinding.From(global?["bindings"]?[row.Action]) is { } binding) row.Binding = binding;
            }
        }
        finally
        {
            _applying = false;
        }
    }

    // What the app managed to register with Windows, so a key another program holds is
    // shown as such instead of silently doing nothing.
    private async Task RefreshAcceleratorsAsync()
    {
        GlobalHotkeysReply reply;
        try
        {
            reply = await _s.Api.GetAsync<GlobalHotkeysReply>("/api/global-hotkeys");
        }
        catch
        {
            return;
        }

        foreach (var row in Global)
        {
            var accelerator = reply.Accelerators?.GetValueOrDefault(row.Action);
            row.Accelerator = accelerator;
            row.Held = accelerator is null || reply.Held is null || reply.Held.Contains(accelerator);
        }
    }

    private void OnLanguageChanged()
    {
        foreach (var row in Local.Concat(Global)) row.RefreshText();
    }

    public void OnClosed()
    {
        _s.StateUpdated -= OnState;
        Loc.Instance.Changed -= OnLanguageChanged;
    }
}
