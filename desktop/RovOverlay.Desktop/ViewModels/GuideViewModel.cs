using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using RovOverlay.Desktop.Core;
using RovOverlay.Desktop.Services;

namespace RovOverlay.Desktop.ViewModels;

public sealed record GuideBlock(string Text, string Kind);   // heading, paragraph, bullet, number, code

public sealed class GuideSection
{
    public required string Title { get; init; }
    public required IReadOnlyList<GuideBlock> Blocks { get; init; }
}

// The user guide, read from the same Markdown that ships with the backend
// (backend/docs/USER_GUIDE.md). Each section holds an English half and a Thai half; the
// screen shows the half matching the app's language.
public sealed class GuideViewModel : ObservableObject, IClosablePage
{
    private readonly AppServices _s;
    private string _markdown = "";
    private string _search = "";

    public GuideViewModel(AppServices services)
    {
        _s = services;
        OpenWebCommand = new Core.RelayCommand(() => Browser.Open(services.Url("/guide")));
        Loc.Instance.Changed += Build;
        Load();
    }

    public ObservableCollection<GuideSection> Sections { get; } = new();
    public System.Windows.Input.ICommand OpenWebCommand { get; }
    public bool IsEmpty => Sections.Count == 0;
    public string EmptyText => Loc.T("Guide.Missing");

    public string Search
    {
        get => _search;
        set
        {
            if (Set(ref _search, value ?? "")) Build();
        }
    }

    private void Load()
    {
        var backend = _s.Backend.BackendDir;
        var candidates = new List<string>();
        if (backend is not null)
        {
            candidates.Add(Path.Combine(backend, "docs", "USER_GUIDE.md"));
            candidates.Add(Path.Combine(backend, "..", "docs", "v2", "USER_GUIDE.md"));
        }

        foreach (var path in candidates)
        {
            try
            {
                if (!File.Exists(path)) continue;
                _markdown = File.ReadAllText(path);
                break;
            }
            catch
            {
                // Try the next place; an unreadable guide is not worth a crash.
            }
        }

        Build();
    }

    private void Build()
    {
        Sections.Clear();
        var thai = Loc.Instance.Language == "th";
        var needle = Search.Trim();

        foreach (var chunk in Regex.Split(_markdown, @"^## ", RegexOptions.Multiline).Skip(1))
        {
            var lines = chunk.Replace("\r", "").Split('\n');
            var heading = lines[0].Trim();
            // "Open the app / เปิดโปรแกรม" - each half of the title in its own language.
            var slash = heading.IndexOf(" / ", StringComparison.Ordinal);
            var title = slash > 0 ? (thai ? heading[(slash + 3)..] : heading[..slash]) : heading;

            var blocks = new List<GuideBlock>();
            // Below **EN** / **TH** the marker decides the language. Some sections also
            // carry a lead above the first marker, sometimes one sentence in each
            // language, so there each line is judged by the script it is written in.
            // A section with no markers at all belongs to both.
            var hasMarkers = chunk.Contains("**EN**") || chunk.Contains("**TH**");
            var inLead = hasMarkers;
            var inWanted = !hasMarkers;
            foreach (var raw in lines.Skip(1))
            {
                var line = raw.TrimEnd();
                if (line is "**EN**") { inLead = false; inWanted = !thai; continue; }
                if (line is "**TH**") { inLead = false; inWanted = thai; continue; }
                if (line.Trim().Length == 0 || line.Trim() == "---") continue;
                if (inLead ? IsThai(line) != thai : !inWanted) continue;

                var text = Clean(line);
                if (text.Length == 0) continue;

                if (line.StartsWith("### ")) blocks.Add(new GuideBlock(Clean(line[4..]), "heading"));
                else if (Regex.IsMatch(line, @"^\s*[-*] ")) blocks.Add(new GuideBlock(Clean(Regex.Replace(line, @"^\s*[-*] ", "")), "bullet"));
                else if (Regex.IsMatch(line, @"^\s*\d+\. ")) blocks.Add(new GuideBlock(Clean(Regex.Replace(line, @"^\s*\d+\. ", "")), "number"));
                else if (line.StartsWith("    ") || line.StartsWith("\t")) blocks.Add(new GuideBlock(text, "code"));
                else blocks.Add(new GuideBlock(text, "paragraph"));
            }

            if (blocks.Count == 0) continue;
            if (needle.Length > 0
                && !title.Contains(needle, StringComparison.CurrentCultureIgnoreCase)
                && !blocks.Any(b => b.Text.Contains(needle, StringComparison.CurrentCultureIgnoreCase)))
                continue;

            Sections.Add(new GuideSection { Title = title, Blocks = blocks });
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
    }

    private static bool IsThai(string line) => line.Any(c => c is >= '฀' and <= '๿');

    // Markdown emphasis and links carry no meaning once this is WPF text.
    private static string Clean(string line) =>
        Regex.Replace(Regex.Replace(line.Trim(), @"\*\*(.+?)\*\*", "$1"), @"`(.+?)`", "$1")
            .Replace("&nbsp;", " ");

    public void OnClosed() => Loc.Instance.Changed -= Build;
}
