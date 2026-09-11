using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Windows.Input;
using System.Windows.Media;
using RovOverlay.Desktop.Core;
using RovOverlay.Desktop.Models;
using RovOverlay.Desktop.Services;

namespace RovOverlay.Desktop.ViewModels;

// One background image the overlay can wear. The file name is fixed by the server
// (backend/server/domain/media.ts), so a slot is a slot, never a user-typed path.
public sealed class SkinSlotRow : ObservableObject
{
    private static readonly HttpClient Probe = new() { Timeout = TimeSpan.FromSeconds(3) };
    private readonly DesignViewModel _owner;
    private string? _previewUrl;
    private long _version;

    public SkinSlotRow(DesignViewModel owner, string key, string file, string group, string part, int width, int height, string noteKey)
    {
        _owner = owner;
        Key = key;
        File = file;
        Group = group;
        Part = part;
        Width = width;
        Height = height;
        NoteKey = noteKey;
        UploadCommand = new AsyncRelayCommand(() => owner.UploadAsync(this));
        ClearCommand = new AsyncRelayCommand(() => owner.ClearAsync(this));
    }

    public string Key { get; }
    public string File { get; }
    public string Group { get; }
    public string Part { get; }
    public int Width { get; }
    public int Height { get; }
    public string NoteKey { get; }
    public string Note => Loc.T(NoteKey);
    public string SizeText => $"W {Width} × H {Height} px";
    public string RatioText => $"{(double)Width / Height:0.00} : 1";
    public string FileText => $"{File}.png";
    public ICommand UploadCommand { get; }
    public ICommand ClearCommand { get; }

    public string? PreviewUrl { get => _previewUrl; private set => Set(ref _previewUrl, value); }
    public bool HasImage => PreviewUrl is not null;

    // The state carries a version stamp, not the file type, so the three allowed types
    // are probed in turn. Cheap: it is a request to a server on this machine.
    public async Task ApplyAsync(long version, AppServices services)
    {
        if (version == _version) return;
        _version = version;

        if (version == 0)
        {
            PreviewUrl = null;
            OnPropertyChanged(nameof(HasImage));
            return;
        }

        foreach (var ext in new[] { "png", "jpg", "webp" })
        {
            var url = services.Url($"/images/skins/{File}.{ext}?v={version}");
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                using var reply = await Probe.SendAsync(request);
                if (!reply.IsSuccessStatusCode) continue;
                PreviewUrl = url;
                OnPropertyChanged(nameof(HasImage));
                return;
            }
            catch
            {
                // Try the next extension.
            }
        }

        PreviewUrl = null;
        OnPropertyChanged(nameof(HasImage));
    }

    public void RefreshText()
    {
        OnPropertyChanged(nameof(Note));
    }
}

public sealed class ThemeColorRow : ObservableObject
{
    private readonly DesignViewModel _owner;
    private string _hex = "#000000";

    public ThemeColorRow(DesignViewModel owner, string key, string labelKey, string fallback)
    {
        _owner = owner;
        Key = key;
        LabelKey = labelKey;
        Default = fallback;
        _hex = fallback;
    }

    public string Key { get; }
    public string LabelKey { get; }
    public string Default { get; }
    public string Label => Loc.T(LabelKey);
    public bool IsChanged => !string.Equals(_hex, Default, StringComparison.OrdinalIgnoreCase);

    public string Hex
    {
        get => _hex;
        set
        {
            var text = (value ?? "").Trim().ToLowerInvariant();
            if (!Set(ref _hex, text)) return;
            OnPropertyChanged(nameof(Swatch));
            OnPropertyChanged(nameof(IsValid));
            OnPropertyChanged(nameof(IsChanged));
            if (IsValid) _owner.PushTheme(Key, text);
        }
    }

    public bool IsValid => System.Text.RegularExpressions.Regex.IsMatch(_hex, "^#[0-9a-f]{6}$");

    public Brush Swatch => IsValid
        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(_hex))
        : Brushes.Transparent;

    public void Apply(string hex)
    {
        if (string.Equals(hex, _hex, StringComparison.OrdinalIgnoreCase)) return;
        _hex = hex.ToLowerInvariant();
        foreach (var name in new[] { nameof(Hex), nameof(Swatch), nameof(IsValid), nameof(IsChanged) }) OnPropertyChanged(name);
    }

    public void RefreshText() => OnPropertyChanged(nameof(Label));
}

public sealed class ThemeNumberRow : ObservableObject
{
    private readonly DesignViewModel _owner;
    private double _value;

    public ThemeNumberRow(DesignViewModel owner, string key, string labelKey, double min, double max, double fallback)
    {
        _owner = owner;
        Key = key;
        LabelKey = labelKey;
        Min = min;
        Max = max;
        Default = fallback;
        _value = fallback;
    }

    public string Key { get; }
    public string LabelKey { get; }
    public double Min { get; }
    public double Max { get; }
    public double Default { get; }
    public string Label => Loc.T(LabelKey);
    public string ValueText => $"{Math.Round(_value)}px";
    public bool IsChanged => Math.Abs(_value - Default) > 0.5;

    public double Value
    {
        get => _value;
        set
        {
            if (!Set(ref _value, value)) return;
            OnPropertyChanged(nameof(ValueText));
            OnPropertyChanged(nameof(IsChanged));
            _owner.PushTheme(Key, (int)Math.Round(value));
        }
    }

    public void Apply(double value)
    {
        if (Math.Abs(value - _value) < 0.5) return;
        _value = value;
        foreach (var name in new[] { nameof(Value), nameof(ValueText), nameof(IsChanged) }) OnPropertyChanged(name);
    }

    public void RefreshText() => OnPropertyChanged(nameof(Label));
}

// How the overlay looks: the background images it wears, and the colours and sizes of
// everything drawn on top. v2's /design, rebuilt.
public sealed class DesignViewModel : ObservableObject, IClosablePage
{
    private readonly AppServices _s;
    private readonly Debouncer _themeSend = new(120);
    private readonly Dictionary<string, object> _pendingTheme = new();
    private bool _skinEnabled;
    private bool _showPanels = true;
    private bool _applying;

    public DesignViewModel(AppServices services)
    {
        _s = services;

        // Same six slots the Design page offered in v2, in the same order.
        Slots =
        [
            new(this, "overlayBottom1080", "overlay-bottom-1080", "Overlay", "Banner", 1920, 430, "Design.NoteBanner"),
            new(this, "resultTop1080", "result-top-1080", "Result", "Top", 1920, 540, "Design.NoteTop"),
            new(this, "resultBottom1080", "result-bottom-1080", "Result", "Bottom", 1920, 540, "Design.NoteBottom"),
            new(this, "overlayBottom1440", "overlay-bottom-1440", "Overlay", "Banner", 2560, 573, "Design.NoteBanner"),
            new(this, "resultTop1440", "result-top-1440", "Result", "Top", 2560, 720, "Design.NoteTop"),
            new(this, "resultBottom1440", "result-bottom-1440", "Result", "Bottom", 2560, 720, "Design.NoteBottom")
        ];

        Colors =
        [
            new(this, "blue", "Design.Blue", "#38bdf8"),
            new(this, "red", "Design.Red", "#f87171"),
            new(this, "text", "Design.Text", "#ffffff"),
            new(this, "accent", "Design.Accent", "#f59e0b"),
            new(this, "label", "Design.Label", "#c0c0c0")
        ];

        Numbers =
        [
            new(this, "typeTournament", "Design.TypeTournament", 10, 48, 18),
            new(this, "typeTitle", "Design.TypeTitle", 10, 60, 24),
            new(this, "typeScore", "Design.TypeScore", 12, 96, 42),
            new(this, "typeTimer", "Design.TypeTimer", 12, 96, 40),
            new(this, "typePlayer", "Design.TypePlayer", 10, 48, 22),
            new(this, "typeCaption", "Design.TypeCaption", 8, 40, 14),
            new(this, "logoSize", "Design.LogoSize", 40, 260, 138),
            new(this, "logoInset", "Design.LogoInset", -40, 200, 10)
        ];

        ResetThemeCommand = new RelayCommand(() =>
        {
            _s.Socket?.EmitAsync("resetTheme");
            Toasts.Info(Loc.T("Design.ThemeReset"));
        });
        OpenPreviewCommand = new RelayCommand(() => Browser.Open(_s.Url(Is1440 ? "/overlay-1440" : "/overlay")));

        services.StateUpdated += OnState;
        Loc.Instance.Changed += OnLanguageChanged;
        if (services.LastState is not null) OnState(services.LastState);
    }

    public IReadOnlyList<SkinSlotRow> Slots { get; }
    public IReadOnlyList<ThemeColorRow> Colors { get; }
    public IReadOnlyList<ThemeNumberRow> Numbers { get; }
    public ICommand ResetThemeCommand { get; }
    public ICommand OpenPreviewCommand { get; }

    public bool Is1440 { get; private set; }
    public string PreviewSizeText => Is1440 ? "2560 × 1440" : "1920 × 1080";

    public bool SkinEnabled
    {
        get => _skinEnabled;
        set
        {
            if (!Set(ref _skinEnabled, value) || _applying) return;
            _s.Socket?.EmitAsync("updateSkinOptions", new { enabled = value });
        }
    }

    public bool ShowPanels
    {
        get => _showPanels;
        set
        {
            if (!Set(ref _showPanels, value) || _applying) return;
            _s.Socket?.EmitAsync("updateSkinOptions", new { showPanels = value });
        }
    }

    // Dragging a slider fires constantly; the overlay only needs the value it settles on,
    // plus enough updates on the way to feel live.
    internal void PushTheme(string key, object value)
    {
        if (_applying) return;
        _pendingTheme[key] = value;
        _themeSend.Run(() =>
        {
            var payload = new Dictionary<string, object>(_pendingTheme);
            _pendingTheme.Clear();
            _s.Socket?.EmitAsync("updateTheme", payload);
        });
    }

    internal async Task UploadAsync(SkinSlotRow slot)
    {
        var path = Dialogs.PickImage();
        if (path is null) return;

        var type = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => null
        };
        if (type is null)
        {
            Toasts.Error(Loc.T("Team.LogoType"));
            return;
        }
        if (new FileInfo(path).Length > 8 * 1024 * 1024)
        {
            Toasts.Error(Loc.T("Design.TooBig"));
            return;
        }

        var bytes = await File.ReadAllBytesAsync(path);
        await _s.Api.PostBytesAsync<OkReply>($"/api/skin/{slot.Key}", bytes, type);
        Toasts.Info(Loc.T("Design.Uploaded"));
    }

    internal async Task ClearAsync(SkinSlotRow slot)
    {
        await _s.Api.DeleteAsync<OkReply>($"/api/skin/{slot.Key}");
        Toasts.Info(Loc.T("Design.Cleared"));
    }

    private void OnState(JsonNode node)
    {
        _applying = true;
        try
        {
            var skin = node["skin"];
            SkinEnabled = J.Bool(skin?["enabled"]) == true;
            ShowPanels = J.Bool(skin?["showPanels"]) != false;

            foreach (var slot in Slots) _ = slot.ApplyAsync(J.Int(skin?["slots"]?[slot.Key]), _s);

            var theme = node["theme"];
            foreach (var row in Colors)
            {
                if (J.Str(theme?[row.Key]) is { Length: > 0 } hex) row.Apply(hex);
            }
            foreach (var row in Numbers) row.Apply(J.Int(theme?[row.Key], (int)row.Default));

            var size = J.Str(node["overlaySize"]) == "1440";
            if (size != Is1440)
            {
                Is1440 = size;
                OnPropertyChanged(nameof(Is1440));
                OnPropertyChanged(nameof(PreviewSizeText));
            }
        }
        finally
        {
            _applying = false;
        }
    }

    private void OnLanguageChanged()
    {
        foreach (var slot in Slots) slot.RefreshText();
        foreach (var row in Colors) row.RefreshText();
        foreach (var row in Numbers) row.RefreshText();
    }

    public void OnClosed()
    {
        _s.StateUpdated -= OnState;
        Loc.Instance.Changed -= OnLanguageChanged;
    }
}
