using System.IO;
using System.Text.Json;

namespace RovOverlay.Desktop.Services;

// Desktop-app preferences, plus where v3 keeps the operator's data.
//
// The data lives under %APPDATA%\RovOverlayTool3, deliberately apart from v2's
// %APPDATA%\ROV Overlay Tool. v3 never reads or writes v2's folder on its own; moving
// data across is an explicit import the operator asks for.
//
// It is also apart from the install folder, because the updater replaces the install
// folder wholesale on every update.
public sealed class AppSettings
{
    public const int DefaultPort = 3000;

    public string Language { get; set; } = "th";
    public int Port { get; set; } = DefaultPort;

    // "stable" or "beta". Test versions arrive earlier and break more often, so this is
    // opt-in and lives here rather than being guessed from the version number.
    public string UpdateChannel { get; set; } = "stable";

    // Messages from the maker the operator has already read, so they stay gone.
    public List<string> DismissedNotices { get; set; } = new();

    // Which version of the licence has been agreed to. A number, not a flag, so a
    // changed licence can be shown again instead of being assumed.
    public int AgreedLicence { get; set; }

    // Which sections the operator folded away, by key, so a 128-team roster stays folded
    // the next time the page opens.
    public Dictionary<string, bool> Folds { get; set; } = new();

    public bool IsOpen(string key, bool fallback) => Folds.TryGetValue(key, out var open) ? open : fallback;

    public void SetOpen(string key, bool open)
    {
        Folds[key] = open;
        Save();
    }

    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RovOverlayTool3");

    public static string DataDir => Path.Combine(Root, "data");
    public static string MediaDir => Path.Combine(Root, "media");
    private static string FilePath => Path.Combine(Root, "settings.json");

    public static AppSettings Load()
    {
        AppSettings settings;
        try
        {
            settings = File.Exists(FilePath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings()
                : new AppSettings();
        }
        catch
        {
            // A damaged settings file costs the operator their language choice, nothing more.
            settings = new AppSettings();
        }

        if (settings.Language is not ("th" or "en")) settings.Language = "th";
        if (settings.Port is < 1024 or > 65535) settings.Port = DefaultPort;
        return settings;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Not being able to remember a preference is not worth interrupting anyone for.
        }
    }
}
