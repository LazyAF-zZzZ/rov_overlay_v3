using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using RovOverlay.Desktop.Core;

namespace RovOverlay.Desktop.Services;

// Messages from the person who makes the app: "3.0.2 fixes the timer", "the new hero
// is in", "do not update mid-tournament". They arrive as one public file in the
// repository, read over HTTPS.
//
// There is no server of ours in this and nothing is sent about the operator, not even
// which version they run: the filtering by version happens here, on their machine.
// Offline, nothing is shown and nothing complains.
public sealed class NoticeService : ObservableObject, IDisposable
{
    public const string FeedUrl =
        "https://raw.githubusercontent.com/LazyAF-zZzZ/rov_overlay_v3/main/notices.json";

    private static readonly TimeSpan Every = TimeSpan.FromHours(6);
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly AppSettings _settings;
    private readonly CancellationTokenSource _stop = new();
    private bool _isOpen;

    public NoticeService(AppSettings settings)
    {
        _settings = settings;
        Items.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasNotices));
            OnPropertyChanged(nameof(Count));
        };
        Loc.Instance.Changed += () =>
        {
            foreach (var item in Items) item.Refresh();
        };
    }

    public ObservableCollection<NoticeItem> Items { get; } = new();

    public bool HasNotices => Items.Count > 0;
    public int Count => Items.Count;

    public bool IsOpen { get => _isOpen; set => Set(ref _isOpen, value); }

    public void Start() => _ = LoopAsync();

    private async Task LoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            await FetchAsync();
            try
            {
                await Task.Delay(Every, _stop.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task FetchAsync()
    {
        List<NoticeDto>? feed;
        try
        {
            feed = await Http.GetFromJsonAsync<List<NoticeDto>>(FeedUrl, Json, _stop.Token);
        }
        catch
        {
            // No network, no file yet, or a half-written one. Silence is the right answer:
            // notices are extra, never something the app needs to work.
            return;
        }

        if (feed is null) return;
        Application.Current?.Dispatcher.InvokeAsync(() => Apply(feed));
    }

    private void Apply(List<NoticeDto> feed)
    {
        var now = DateTimeOffset.UtcNow;
        var showing = new List<NoticeDto>();
        foreach (var dto in feed)
        {
            if (string.IsNullOrWhiteSpace(dto.Id)) continue;
            if (_settings.DismissedNotices.Contains(dto.Id)) continue;
            if (dto.Expires is { } expires && expires < now) continue;
            if (!Versions.InRange(AppVersion.Text, dto.MinVersion, dto.MaxVersion)) continue;
            showing.Add(dto);
        }

        var known = Items.Select(i => i.Id).ToHashSet();
        Items.Clear();
        foreach (var dto in showing) Items.Add(new NoticeItem(dto, Dismiss));

        // Only a notice the operator has not seen before is worth a toast; the bell
        // carries the rest quietly.
        var fresh = showing.Where(d => !known.Contains(d.Id!)).ToList();
        if (fresh.Count == 1) Toasts.Info(new NoticeItem(fresh[0], _ => { }).Title);
        else if (fresh.Count > 1) Toasts.Info(Loc.F("Notices.New", fresh.Count));
    }

    // Dismissed ids are remembered so a notice does not come back on the next start.
    public void Dismiss(NoticeItem item)
    {
        if (!_settings.DismissedNotices.Contains(item.Id))
        {
            _settings.DismissedNotices.Add(item.Id);
            _settings.Save();
        }
        Items.Remove(item);
        if (Items.Count == 0) IsOpen = false;
    }

    public void DismissAll()
    {
        foreach (var item in Items.ToList()) Dismiss(item);
    }

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }
}

// One notice, in the language the operator is reading.
public sealed class NoticeItem : ObservableObject
{
    private readonly NoticeDto _dto;

    public NoticeItem(NoticeDto dto, Action<NoticeItem> dismiss)
    {
        _dto = dto;
        Id = dto.Id!;
        DismissCommand = new RelayCommand(() => dismiss(this));
        OpenCommand = new RelayCommand(() => Browser.Open(Url!), () => HasUrl);
    }

    public string Id { get; }
    public string Title => Pick(_dto.Title);
    public string Body => Pick(_dto.Body);
    public string? Url => _dto.Url;
    public bool HasUrl => !string.IsNullOrWhiteSpace(_dto.Url);

    public bool IsCritical => _dto.Level == "critical";
    public bool IsWarning => _dto.Level == "warning";

    // Segoe MDL2: shield for critical, warning triangle, speech bubble for news.
    public string Glyph => _dto.Level switch
    {
        "critical" => "\uE730",
        "warning" => "\uE7BA",
        _ => "\uE90A"
    };

    public ICommand DismissCommand { get; }
    public ICommand OpenCommand { get; }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Body));
    }

    private static string Pick(NoticeTextDto? text)
    {
        if (text is null) return "";
        return (Loc.Instance.Language == "th" ? text.Th ?? text.En : text.En ?? text.Th) ?? "";
    }
}

// The shape of one entry in notices.json. Everything except the id is optional, so an
// older app never chokes on a field a newer feed adds.
public sealed class NoticeDto
{
    public string? Id { get; set; }
    public string? Level { get; set; }
    public NoticeTextDto? Title { get; set; }
    public NoticeTextDto? Body { get; set; }
    public string? Url { get; set; }
    public string? MinVersion { get; set; }
    public string? MaxVersion { get; set; }
    public DateTimeOffset? Expires { get; set; }
}

public sealed class NoticeTextDto
{
    public string? Th { get; set; }
    public string? En { get; set; }
}

// Comparing "3.0.1" with "3.0.10" as text gets it wrong, and the app's own version can
// carry a suffix ("3.0.0-dev"), so compare the numbers and ignore the rest.
public static class Versions
{
    public static bool InRange(string version, string? min, string? max) =>
        (min is null || Compare(version, min) >= 0) && (max is null || Compare(version, max) <= 0);

    public static int Compare(string a, string b)
    {
        var left = Parts(a);
        var right = Parts(b);
        for (var i = 0; i < Math.Max(left.Length, right.Length); i++)
        {
            var l = i < left.Length ? left[i] : 0;
            var r = i < right.Length ? right[i] : 0;
            if (l != r) return l.CompareTo(r);
        }
        return 0;
    }

    private static int[] Parts(string version)
    {
        var cut = version.AsSpan();
        var dash = cut.IndexOfAny('-', '+');
        if (dash >= 0) cut = cut[..dash];
        return cut.ToString()
            .Split('.')
            .Select(p => int.TryParse(p, out var n) ? n : 0)
            .ToArray();
    }
}
