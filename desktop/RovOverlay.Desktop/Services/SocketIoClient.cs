using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RovOverlay.Desktop.Services;

// A deliberately small Socket.IO v4 client over a raw WebSocket.
//
// The backend is Socket.IO because the HTML overlays in OBS are, and they must stay
// untouched. SignalR cannot talk to them, so the desktop app speaks Socket.IO too.
// Only what the operator app needs is implemented: the default namespace, text
// events, emit, ping/pong and reconnect. No binary packets, no acks, no polling.
//
// Wire format (Engine.IO 4 packet type, then Socket.IO packet type):
//   "0{...}"          engine open        -> reply "40" (+ auth JSON) to join "/"
//   "2" / "3"         ping / pong        -> answer every "2" with "3" or the server drops us
//   "40{...}"         namespace connected
//   "42[name,data]"   an event
//   "44{...}"         connect error (for example a wrong control token)
//
// Callbacks run on the SynchronizationContext captured at construction, so a client
// created on the UI thread delivers events straight to view models.
public sealed class SocketIoClient : IAsyncDisposable
{
    private readonly Uri _uri;
    private readonly string? _token;
    private readonly SynchronizationContext? _sync;
    private readonly Dictionary<string, List<Action<JsonNode?>>> _handlers = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private ClientWebSocket? _socket;
    private Task? _loop;

    public SocketIoClient(Uri serverUri, string? token = null)
    {
        var builder = new UriBuilder(serverUri)
        {
            Scheme = serverUri.Scheme == "https" ? "wss" : "ws",
            Path = "/socket.io/",
            Query = "EIO=4&transport=websocket"
        };
        _uri = builder.Uri;
        _token = string.IsNullOrEmpty(token) ? null : token;
        _sync = SynchronizationContext.Current;
    }

    public bool IsConnected { get; private set; }

    public event Action? Connected;
    public event Action? Disconnected;

    public void On(string eventName, Action<JsonNode?> handler)
    {
        if (!_handlers.TryGetValue(eventName, out var list))
            _handlers[eventName] = list = new List<Action<JsonNode?>>();
        list.Add(handler);
    }

    public void Start() => _loop ??= Task.Run(RunAsync);

    public async Task EmitAsync(string eventName, object? payload = null)
    {
        var socket = _socket;
        if (!IsConnected || socket is null) return;

        var packet = new JsonArray { eventName };
        if (payload is not null) packet.Add(payload as JsonNode ?? JsonSerializer.SerializeToNode(payload));
        await SendAsync(socket, "42" + packet.ToJsonString(), _stop.Token);
    }

    private async Task RunAsync()
    {
        var delay = TimeSpan.FromMilliseconds(500);
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await ConnectOnceAsync(_stop.Token);
                delay = TimeSpan.FromMilliseconds(500);
            }
            catch when (_stop.IsCancellationRequested)
            {
                return;
            }
            catch
            {
                // Server not up yet or went away; fall through to the retry.
            }

            if (IsConnected)
            {
                IsConnected = false;
                Post(() => Disconnected?.Invoke());
            }

            try { await Task.Delay(delay, _stop.Token); } catch { return; }
            delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, 4000));
        }
    }

    private async Task ConnectOnceAsync(CancellationToken ct)
    {
        using var socket = new ClientWebSocket();
        socket.Options.KeepAliveInterval = TimeSpan.Zero; // Engine.IO runs its own ping.
        await socket.ConnectAsync(_uri, ct);
        _socket = socket;

        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var text = await ReceiveTextAsync(socket, ct);
                if (text is null) return;
                await HandlePacketAsync(socket, text, ct);
            }
        }
        finally
        {
            _socket = null;
        }
    }

    private async Task HandlePacketAsync(ClientWebSocket socket, string text, CancellationToken ct)
    {
        if (text.Length == 0) return;
        switch (text[0])
        {
            case '0':
                var auth = _token is null ? "" : new JsonObject { ["token"] = _token }.ToJsonString();
                await SendAsync(socket, "40" + auth, ct);
                break;
            case '2':
                await SendAsync(socket, "3", ct);
                break;
            case '1':
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, ct);
                break;
            case '4':
                HandleMessage(text[1..]);
                break;
        }
    }

    private void HandleMessage(string body)
    {
        if (body.Length == 0) return;
        switch (body[0])
        {
            case '0':
                IsConnected = true;
                Post(() => Connected?.Invoke());
                break;
            case '2':
                // Skip an ack id, if any, which sits between the type and the array.
                var start = body.IndexOf('[');
                if (start < 0) return;
                if (JsonNode.Parse(body[start..]) is not JsonArray array || array.Count == 0) return;
                var name = array[0]?.GetValue<string>();
                if (name is null || !_handlers.TryGetValue(name, out var list)) return;
                var payload = array.Count > 1 ? array[1]?.DeepClone() : null;
                Post(() => list.ForEach(handler => handler(payload)));
                break;
            case '4':
                throw new InvalidOperationException("Socket.IO connect error: " + body[1..]);
        }
    }

    private static async Task<string?> ReceiveTextAsync(ClientWebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            message.Write(buffer, 0, result.Count);
            if (result.EndOfMessage) break;
        }
        return Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
    }

    private async Task SendAsync(ClientWebSocket socket, string text, CancellationToken ct)
    {
        await _sendLock.WaitAsync(ct);
        try
        {
            await socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, ct);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private void Post(Action action)
    {
        if (_sync is null) action();
        else _sync.Post(_ => action(), null);
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _socket?.Abort();
        if (_loop is not null)
        {
            try { await _loop; } catch { /* already stopping */ }
        }
        _stop.Dispose();
    }
}
