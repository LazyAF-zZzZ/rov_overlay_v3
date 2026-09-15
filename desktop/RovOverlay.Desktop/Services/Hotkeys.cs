using System.Text.Json.Nodes;
using System.Windows.Input;
using RovOverlay.Desktop.Core;

namespace RovOverlay.Desktop.Services;

// The operator's keyboard shortcuts, stored on the server so the overlay pages and this
// app agree on them. Bindings use the browser's KeyboardEvent.code names ("KeyZ",
// "ArrowLeft", "Space") because that is what v2 wrote into state.json and what the
// Hotkeys page still edits; this maps WPF keys onto those names.
//
// A binding whose code is a bare modifier ("Alt") means "tap that key on its own",
// which is how the banner is toggled without reaching for the mouse.
public sealed record HotkeyBinding(string Code, bool Ctrl, bool Shift, bool Alt, bool Meta)
{
    public static readonly IReadOnlyDictionary<string, HotkeyBinding> Defaults = new Dictionary<string, HotkeyBinding>
    {
        ["toggleBanner"] = new("Alt", false, false, false, false),
        ["pauseResume"] = new("Space", false, false, false, false),
        ["prevPhase"] = new("ArrowLeft", false, false, false, false),
        ["nextPhase"] = new("ArrowRight", false, false, false, false),
        ["undo"] = new("KeyZ", true, false, false, false)
    };

    public static readonly string[] ModifierCodes = ["Alt", "Control", "Shift", "Meta"];

    public bool IsModifierOnly => ModifierCodes.Contains(Code);

    public static HotkeyBinding? From(JsonNode? node)
    {
        var code = J.Str(node?["code"]);
        if (string.IsNullOrEmpty(code)) return null;
        return new HotkeyBinding(
            code,
            J.Bool(node?["ctrl"]) == true,
            J.Bool(node?["shift"]) == true,
            J.Bool(node?["alt"]) == true,
            J.Bool(node?["meta"]) == true);
    }

    public string Label()
    {
        if (IsModifierOnly) return Loc.F("Keys.Tap", CodeLabel(Code));
        var parts = new List<string>();
        if (Ctrl) parts.Add("Ctrl");
        if (Shift) parts.Add("Shift");
        if (Alt) parts.Add("Alt");
        if (Meta) parts.Add("Win");
        parts.Add(CodeLabel(Code));
        return string.Join(" + ", parts);
    }

    public bool Matches(KeyEventArgs e)
    {
        if (IsModifierOnly) return false;
        var code = Hotkeys.CodeOf(e);
        if (code is null || code != Code) return false;
        var keyboard = Keyboard.Modifiers;
        return Ctrl == keyboard.HasFlag(ModifierKeys.Control)
               && Shift == keyboard.HasFlag(ModifierKeys.Shift)
               && Alt == keyboard.HasFlag(ModifierKeys.Alt)
               && Meta == keyboard.HasFlag(ModifierKeys.Windows);
    }

    private static string CodeLabel(string code) => code switch
    {
        _ when code.Length == 4 && code.StartsWith("Key") => code[3..],
        _ when code.Length == 6 && code.StartsWith("Digit") => code[5..],
        "Space" => "Space",
        "Escape" => "Esc",
        "ArrowLeft" => "←",
        "ArrowRight" => "→",
        "ArrowUp" => "↑",
        "ArrowDown" => "↓",
        "PageUp" => "PgUp",
        "PageDown" => "PgDn",
        _ when code.StartsWith("Numpad") => "Num " + code[6..],
        "Control" => "Ctrl",
        "Meta" => "Win",
        _ => code
    };
}

public static class Hotkeys
{
    // WPF key to the browser code name the binding uses.
    public static string? CodeOf(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        return key switch
        {
            >= Key.A and <= Key.Z => "Key" + key,
            >= Key.D0 and <= Key.D9 => "Digit" + (key - Key.D0),
            Key.Space => "Space",
            Key.Enter => "Enter",
            Key.Escape => "Escape",
            Key.Tab => "Tab",
            Key.Back => "Backspace",
            Key.Left => "ArrowLeft",
            Key.Right => "ArrowRight",
            Key.Up => "ArrowUp",
            Key.Down => "ArrowDown",
            Key.OemMinus => "Minus",
            Key.OemPlus => "Equal",
            Key.OemComma => "Comma",
            Key.OemPeriod => "Period",
            Key.OemQuestion => "Slash",
            Key.OemSemicolon => "Semicolon",
            Key.OemQuotes => "Quote",
            Key.OemOpenBrackets => "BracketLeft",
            Key.OemCloseBrackets => "BracketRight",
            Key.OemBackslash or Key.OemPipe => "Backslash",
            Key.OemTilde => "Backquote",
            // Keys a system-wide binding can use (the server's accelerator table has them);
            // without these the default Ctrl+Alt+PageDown could not be recorded back.
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Home => "Home",
            Key.End => "End",
            Key.Insert => "Insert",
            Key.Delete => "Delete",
            >= Key.F1 and <= Key.F24 => "F" + (key - Key.F1 + 1),
            >= Key.NumPad0 and <= Key.NumPad9 => "Numpad" + (key - Key.NumPad0),
            _ => null
        };
    }

    // The bare modifier a key event is, if any: used for "tap Alt".
    public static string? ModifierOf(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        return key switch
        {
            Key.LeftAlt or Key.RightAlt => "Alt",
            Key.LeftCtrl or Key.RightCtrl => "Control",
            Key.LeftShift or Key.RightShift => "Shift",
            Key.LWin or Key.RWin => "Meta",
            _ => null
        };
    }
}
