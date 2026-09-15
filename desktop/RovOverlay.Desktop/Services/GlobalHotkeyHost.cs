using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using RovOverlay.Desktop.Models;

namespace RovOverlay.Desktop.Services;

// System-wide hotkeys: the job Electron's globalShortcut did in v2.
//
// The server owns which keys are bound and what each one does; this only registers them
// with Windows and tells the server when one is pressed. That split is why the overlay
// pages and the Hotkeys screen agree about the bindings without knowing anything about
// Win32.
//
// Accelerator strings ("Control+Alt+H") come from the server already formatted, and the
// list of the ones actually held goes back so the Hotkeys screen can say "another
// program is holding this key" instead of leaving a dead shortcut on screen.
public sealed class GlobalHotkeyHost : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;

    private readonly ApiClient _api;
    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly Dictionary<int, string> _actions = new();      // hotkey id -> action
    private readonly List<string> _held = [];
    private HwndSource? _window;
    private string _signature = "";
    private int _nextId = 1;

    public GlobalHotkeyHost(ApiClient api) => _api = api;

    public void Start()
    {
        // A message-only window: it never shows, it just receives WM_HOTKEY.
        _window = new HwndSource(new HwndSourceParameters("RovOverlayHotkeys")
        {
            ParentWindow = new IntPtr(-3),  // HWND_MESSAGE
            WindowStyle = 0
        });
        _window.AddHook(OnMessage);

        _poll.Tick += (_, _) => _ = RefreshAsync();
        _poll.Start();
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        GlobalHotkeysReply config;
        try
        {
            config = await _api.GetAsync<GlobalHotkeysReply>("/api/global-hotkeys");
        }
        catch
        {
            return; // server not up yet, or going away; the next tick asks again
        }

        var accelerators = config.Accelerators ?? new Dictionary<string, string>();
        var signature = config.Enabled + "|" + string.Join(",", accelerators.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value}"));
        if (signature == _signature)
        {
            // Nothing changed, but the server may have forgotten what we hold (a restart).
            if (config.Held is null || !config.Held.OrderBy(h => h).SequenceEqual(_held.OrderBy(h => h))) await ReportAsync();
            return;
        }
        _signature = signature;

        Release();
        if (config.Enabled)
        {
            foreach (var (action, accelerator) in accelerators) Register(action, accelerator);
        }
        await ReportAsync();
    }

    private void Register(string action, string accelerator)
    {
        if (_window is null || !TryParse(accelerator, out var modifiers, out var key)) return;

        var id = _nextId++;
        if (!RegisterHotKey(_window.Handle, id, modifiers | MOD_NOREPEAT, key))
        {
            // Another program holds it. Not an error worth interrupting anyone over: the
            // Hotkeys screen shows which ones did not take.
            return;
        }
        _actions[id] = action;
        _held.Add(accelerator);
    }

    private void Release()
    {
        if (_window is not null)
        {
            foreach (var id in _actions.Keys) UnregisterHotKey(_window.Handle, id);
        }
        _actions.Clear();
        _held.Clear();
    }

    private async Task ReportAsync()
    {
        try
        {
            await _api.PostAsync<OkReply>("/api/global-hotkeys/registered", new { held = _held.ToArray() });
        }
        catch
        {
            // Nobody is listening; the screen simply shows nothing about what is held.
        }
    }

    private IntPtr OnMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WM_HOTKEY || !_actions.TryGetValue(wParam.ToInt32(), out var action)) return IntPtr.Zero;
        handled = true;
        _ = FireAsync(action);
        return IntPtr.Zero;
    }

    // Raised on the UI thread after the server has acted on a key. The Control Panel takes
    // +1 and -1 (it shows SERIES OVER from them) and sets Handled; anything left over is
    // reported here, because a key pressed from OBS has no button whose state shows it.
    public event Action<HotkeyFiredEventArgs>? Fired;

    private async Task FireAsync(string action)
    {
        GlobalHotkeyFireReply reply;
        try
        {
            reply = await _api.PostAsync<GlobalHotkeyFireReply>("/api/global-hotkeys/fire", new { action });
        }
        catch (Exception error)
        {
            Toasts.Error(error.Message);
            return;
        }

        var args = new HotkeyFiredEventArgs(action, reply);
        Fired?.Invoke(args);
        if (!args.Handled) Report(args);
    }

    private static void Report(HotkeyFiredEventArgs e)
    {
        var reply = e.Reply;
        var side = Loc.T(e.Action.StartsWith("blue") ? "Control.BlueTeam" : "Control.RedTeam");
        switch (e.Action)
        {
            case "bluePlus" or "redPlus":
                if (reply.Finish is { } finish) Toasts.Info(GameFlowText.Finished(finish, side));
                else Toasts.Error(GameFlowText.FinishError(reply.Code, reply.Error ?? ""));
                break;
            case "blueMinus" or "redMinus":
                if (reply.Undo is { } undo) Toasts.Info(GameFlowText.Undone(undo, side));
                else Toasts.Error(GameFlowText.UndoError(reply.Code, reply.Error ?? ""));
                break;
            case "prevRound" or "nextRound":
                // Success needs no words: the board itself changes.
                if (!reply.Changed) Toasts.Error(GameFlowText.RoundError(reply.Code, reply.Error ?? ""));
                break;
        }
    }

    // "Control+Alt+H" as the server writes it (domain/settings.ts toAccelerator).
    private static bool TryParse(string accelerator, out uint modifiers, out uint key)
    {
        modifiers = 0;
        key = 0;
        var parts = accelerator.Split('+', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;

        for (var i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i])
            {
                case "Control": modifiers |= MOD_CONTROL; break;
                case "Alt": modifiers |= MOD_ALT; break;
                case "Shift": modifiers |= MOD_SHIFT; break;
                case "Super": modifiers |= MOD_WIN; break;
                default: return false;
            }
        }

        key = VirtualKey(parts[^1]);
        return modifiers != 0 && key != 0;
    }

    private static uint VirtualKey(string name)
    {
        if (name.Length == 1)
        {
            var c = char.ToUpperInvariant(name[0]);
            if (c is >= 'A' and <= 'Z') return c;
            if (c is >= '0' and <= '9') return c;
            return c switch
            {
                '-' => 0xBD, '=' => 0xBB, '[' => 0xDB, ']' => 0xDD, ';' => 0xBA, '\'' => 0xDE,
                '`' => 0xC0, '\\' => 0xDC, ',' => 0xBC, '.' => 0xBE, '/' => 0xBF,
                _ => 0
            };
        }

        if (name.StartsWith('F') && int.TryParse(name[1..], out var f) && f is >= 1 and <= 24) return (uint)(0x70 + f - 1);
        if (name.StartsWith("num") && int.TryParse(name[3..], out var n) && n is >= 0 and <= 9) return (uint)(0x60 + n);

        return name switch
        {
            "Space" => 0x20,
            "Return" => 0x0D,
            "Tab" => 0x09,
            "Backspace" => 0x08,
            "Escape" => 0x1B,
            "Delete" => 0x2E,
            "Insert" => 0x2D,
            "Home" => 0x24,
            "End" => 0x23,
            "PageUp" => 0x21,
            "PageDown" => 0x22,
            "Left" => 0x25,
            "Up" => 0x26,
            "Right" => 0x27,
            "Down" => 0x28,
            _ => 0
        };
    }

    public void Dispose()
    {
        _poll.Stop();
        Release();
        _window?.Dispose();
        _window = null;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}

public sealed class HotkeyFiredEventArgs(string action, GlobalHotkeyFireReply reply)
{
    public string Action { get; } = action;
    public GlobalHotkeyFireReply Reply { get; } = reply;
    public bool Handled { get; set; }
}
