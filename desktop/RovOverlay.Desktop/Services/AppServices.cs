using System.Text.Json;
using System.Text.Json.Nodes;
using RovOverlay.Desktop.Core;
using RovOverlay.Desktop.Models;

namespace RovOverlay.Desktop.Services;

// Everything a screen needs to talk to the backend, shared by all of them.
//
// Events are raised on the UI thread (the socket client posts to the context it was
// created on, and Connect runs on the UI thread), so view models can update
// bound collections directly.
public sealed class AppServices : IAsyncDisposable
{
    public AppServices(AppSettings settings)
    {
        Settings = settings;
        Backend = new BackendHost(settings.Port);
        Updates = new UpdateService(settings);
        Notices = new NoticeService(settings);
    }

    public AppSettings Settings { get; }
    public BackendHost Backend { get; }
    public UpdateService Updates { get; }
    public NoticeService Notices { get; }
    public ApiClient Api { get; private set; } = null!;
    public SocketIoClient? Socket { get; private set; }
    public JsonNode? LastState { get; private set; }

    public event Action<JsonNode>? StateUpdated;
    public event Action<DataChange>? DataChanged;
    public event Action<bool>? ConnectionChanged;

    // A system-wide key the server has acted on. Lives here rather than on the hotkey host
    // because screens are created before Connect() makes the host.
    public event Action<HotkeyFiredEventArgs>? HotkeyFired;

    public void Connect()
    {
        if (Socket is not null) return;

        Api = new ApiClient(Backend.BaseUri);
        var socket = new SocketIoClient(Backend.BaseUri);

        socket.On("stateUpdate", node =>
        {
            if (node is null) return;
            LastState = node;
            StateUpdated?.Invoke(node);
        });

        socket.On("dataChanged", node =>
        {
            DataChange? change = null;
            try { change = node?.Deserialize<DataChange>(ApiClient.Json); } catch { /* ignore a malformed push */ }
            if (change is not null) DataChanged?.Invoke(change);
        });

        socket.On("controlError", node => Toasts.Error(J.Str(node?["message"]) ?? "Control error"));

        // The data room carries "something changed, re-read it" pushes. Rooms do not
        // survive a reconnect, so every connect joins again.
        socket.Connected += () =>
        {
            _ = socket.EmitAsync("data:join");
            ConnectionChanged?.Invoke(true);
        };
        socket.Disconnected += () => ConnectionChanged?.Invoke(false);

        Socket = socket;
        socket.Start();

        // System-wide hotkeys live for as long as the app does, not just while the
        // Hotkeys screen is open.
        Hotkeys = new GlobalHotkeyHost(Api);
        Hotkeys.Fired += e => HotkeyFired?.Invoke(e);
        Hotkeys.Start();

        // Neither of these needs the backend or the network to be there. They start here
        // so they start once, and only after the app is actually up rather than while it
        // is still working out whether it can run at all.
        Updates.Start();
        Notices.Start();
    }

    public GlobalHotkeyHost? Hotkeys { get; private set; }

    public string Url(string route) => new Uri(Backend.BaseUri, route.TrimStart('/')).ToString();

    public async ValueTask DisposeAsync()
    {
        Hotkeys?.Dispose();
        Updates.Dispose();
        Notices.Dispose();
        if (Socket is not null) await Socket.DisposeAsync();
        Backend.Dispose();
    }
}
