using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Windows;
using RovOverlay.Desktop.Services;

namespace RovOverlay.Desktop.Core;

// Reading the live overlay state, which arrives as loose JSON from the server.
// Every reader tolerates a missing key or the wrong kind of value, because a bad
// field must never take the operator UI down in the middle of a broadcast.
public static class J
{
    public static string? Str(JsonNode? node)
    {
        if (node is not JsonValue value) return null;
        if (value.TryGetValue<string>(out var text)) return text;
        if (value.TryGetValue<double>(out var number)) return number.ToString(CultureInfo.InvariantCulture);
        return null;
    }

    public static int Int(JsonNode? node, int fallback = 0)
    {
        if (node is not JsonValue value) return fallback;
        if (value.TryGetValue<int>(out var whole)) return whole;
        if (value.TryGetValue<double>(out var number)) return (int)number;
        if (value.TryGetValue<string>(out var text) && int.TryParse(text, out var parsed)) return parsed;
        return fallback;
    }

    public static bool? Bool(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : null;
}

public static class Browser
{
    public static void Open(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception error)
        {
            Toasts.Error(error.Message);
        }
    }

    public static void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { path }, UseShellExecute = false });
        }
        catch (Exception error)
        {
            Toasts.Error(error.Message);
        }
    }
}

public static class Clip
{
    // The clipboard is a shared resource another program can be holding open, in
    // which case SetText throws. That is a "try again" moment, not a crash.
    public static void Copy(string text)
    {
        try
        {
            Clipboard.SetText(text);
            Toasts.Info(Loc.T("Obs.Copied"));
        }
        catch
        {
            Toasts.Error(Loc.T("Common.CopyFailed"));
        }
    }
}

public static class AppVersion
{
    public static string Text { get; } = Read();

    private static string Read()
    {
        var version = typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        var plus = version.IndexOf('+');
        return plus < 0 ? version : version[..plus];
    }
}
