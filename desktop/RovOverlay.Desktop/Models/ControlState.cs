using System.Text.Json.Nodes;
using RovOverlay.Desktop.Core;
using RovOverlay.Desktop.Services;

namespace RovOverlay.Desktop.Models;

// The live overlay state, read out of the loose JSON the server pushes on
// "stateUpdate". Every field tolerates rubbish: this arrives once a second while a
// draft runs, and a bad value must never take the Control Panel down mid-broadcast.
public sealed class ControlState
{
    public const int PickCount = 5;
    public const int BanCount = 4;

    public SideState Blue { get; private init; } = new();
    public SideState Red { get; private init; } = new();
    public string Tournament { get; private init; } = "";
    public string MatchTitle { get; private init; } = "";
    public string Timer { get; private init; } = "";
    public string DraftLabel { get; private init; } = "";
    public int DraftPhaseIndex { get; private init; } = -1;
    public bool DraftRunning { get; private init; }
    public IReadOnlyList<string> ActiveSlots { get; private init; } = [];
    public string OverlaySize { get; private init; } = "1080";
    public bool OverlayVisible { get; private init; } = true;
    public int Round { get; private init; } = 1;
    public int RoundsOnBoard { get; private init; }
    public IReadOnlyDictionary<string, double> Sfx { get; private init; } = new Dictionary<string, double>();
    public IReadOnlyDictionary<string, HotkeyBinding> Hotkeys { get; private init; } = new Dictionary<string, HotkeyBinding>();

    // "coming soon" is the draft engine's way of saying nothing is running yet.
    public bool DraftIdle => DraftLabel is "" or "coming soon";

    public static ControlState From(JsonNode node)
    {
        var round = Math.Max(1, J.Int(node["round"], 1));
        var rounds = node["rounds"] as JsonArray;

        return new ControlState
        {
            Blue = SideState.From(node["teamBlue"], "BLUE"),
            Red = SideState.From(node["teamRed"], "RED"),
            Tournament = J.Str(node["matchInfo"]?["tournament"]) ?? "",
            MatchTitle = J.Str(node["matchInfo"]?["title"]) ?? "",
            Timer = J.Str(node["timer"]) ?? "",
            DraftLabel = J.Str(node["draftLabel"]) ?? "",
            DraftPhaseIndex = J.Int(node["draftPhaseIndex"], -1),
            DraftRunning = J.Bool(node["draftRunning"]) == true,
            ActiveSlots = (node["draftActiveSlots"] as JsonArray)?.Select(J.Str).OfType<string>().ToList() ?? [],
            OverlaySize = J.Str(node["overlaySize"]) == "1440" ? "1440" : "1080",
            OverlayVisible = J.Bool(node["overlayVisible"]) != false,
            Round = round,
            RoundsOnBoard = rounds?.Count(r => J.Int(r?["round"], 0) is var n && n > 0 && n < round) ?? 0,
            Sfx = ReadLevels(node["sfx"]),
            Hotkeys = ReadHotkeys(node["hotkeys"])
        };
    }

    private static Dictionary<string, double> ReadLevels(JsonNode? node)
    {
        var levels = new Dictionary<string, double>();
        foreach (var key in new[] { "pick", "ban", "timer" })
        {
            var value = node?[key] is JsonValue v && v.TryGetValue<double>(out var d) ? d : 1;
            levels[key] = Math.Clamp(value, 0, 1);
        }
        return levels;
    }

    private static Dictionary<string, HotkeyBinding> ReadHotkeys(JsonNode? node)
    {
        var bindings = new Dictionary<string, HotkeyBinding>();
        foreach (var (action, fallback) in HotkeyBinding.Defaults)
            bindings[action] = HotkeyBinding.From(node?[action]) ?? fallback;
        return bindings;
    }
}

public sealed class SideState
{
    public string Name { get; private init; } = "";
    public int Score { get; private init; }
    public long LogoVersion { get; private init; }
    public string LogoExt { get; private init; } = "";
    public string? LogoSource { get; private init; }
    public IReadOnlyList<string> Players { get; private init; } = [];
    public IReadOnlyList<string> Positions { get; private init; } = [];
    public IReadOnlyList<string?> Picks { get; private init; } = [];
    // ช่องพิคที่เลือกไว้แล้วแต่ยังไม่ได้กดยืนยัน overlay ขึ้นภาพแล้วแต่ยังเงียบอยู่
    public IReadOnlyList<bool> PicksPending { get; private init; } = [];
    public IReadOnlyList<string?> Bans { get; private init; } = [];

    public static SideState From(JsonNode? node, string fallbackName) => new()
    {
        Name = J.Str(node?["name"]) is { Length: > 0 } name ? name : fallbackName,
        Score = J.Int(node?["score"]),
        LogoVersion = node?["logo"]?["v"] is JsonValue v && v.TryGetValue<long>(out var version) ? version : 0,
        LogoExt = J.Str(node?["logo"]?["ext"]) ?? "",
        LogoSource = J.Str(node?["logo"]?["src"]),
        Players = Texts(node?["players"], ControlState.PickCount),
        Positions = Texts(node?["positions"], ControlState.PickCount),
        Picks = Heroes(node?["picks"], ControlState.PickCount),
        PicksPending = Flags(node?["picksPending"], ControlState.PickCount),
        Bans = Heroes(node?["bans"], ControlState.BanCount)
    };

    private static List<bool> Flags(JsonNode? node, int count)
    {
        var array = node as JsonArray;
        return Enumerable.Range(0, count)
            .Select(i => J.Bool(array?.ElementAtOrDefault(i)) == true)
            .ToList();
    }

    private static List<string> Texts(JsonNode? node, int count)
    {
        var array = node as JsonArray;
        return Enumerable.Range(0, count).Select(i => J.Str(array?.ElementAtOrDefault(i)) ?? "").ToList();
    }

    private static List<string?> Heroes(JsonNode? node, int count)
    {
        var array = node as JsonArray;
        return Enumerable.Range(0, count)
            .Select(i => J.Str(array?.ElementAtOrDefault(i)) is { Length: > 0 } hero ? hero : null)
            .ToList();
    }
}
