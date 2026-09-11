using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using RovOverlay.Desktop.Models;

namespace RovOverlay.Desktop.Services;

public sealed class BackendException(string message) : Exception(message);

// Starts and owns the Node backend: the HTTP API, Socket.IO, and the HTML overlays
// that OBS loads. The desktop app is only ever a client of it.
//
// Where things are looked for:
//   backend folder   the first "backend" folder containing server.js, walking up from
//                    the exe. Installed: next to the exe. Development: the repo's backend/.
//   node.exe         backend\runtime\node.exe when bundled, otherwise node on PATH.
//
// If a v3 backend already answers on the port (for example `npm run dev` while
// developing), the app attaches to it instead of starting a second one. Anything else
// on the port is refused, never attached to: see /api/app-info in the backend.
public sealed class BackendHost : IDisposable
{
    private const string AppId = "rov-overlay-v3";
    private const int LogLines = 400;
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(20);
    private static readonly HttpClient Probe = new() { Timeout = TimeSpan.FromMilliseconds(900) };

    private readonly ProcessJob _job = new();
    private readonly Queue<string> _log = new();
    private Process? _process;
    private bool _disposed;

    public BackendHost(int port)
    {
        Port = port;
        BaseUri = new Uri($"http://127.0.0.1:{port}/");
    }

    public int Port { get; }
    public Uri BaseUri { get; }
    public string? BackendDir { get; private set; }
    public string? NodePath { get; private set; }
    public AppInfo? Info { get; private set; }

    // True when the backend was already running and this app attached to it.
    public bool Attached { get; private set; }

    // Raised on a worker thread when a backend this app started stops unexpectedly.
    public event Action? Exited;

    public IReadOnlyList<string> RecentLog()
    {
        lock (_log) return _log.ToList();
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        // Found whoever starts the server: screens that read files shipped with the
        // backend (the guide) need this even when we only attach to a running one.
        BackendDir ??= FindBackendDir();

        if (_process is { HasExited: false } && Info is not null) return;

        var existing = await ProbeAsync(ct);
        if (existing?.App == AppId)
        {
            Info = existing;
            Attached = true;
            Append($"-- attached to a backend already running on port {Port} (pid {existing.Pid})");
            return;
        }

        if (await IsPortTakenAsync()) throw new BackendException(Loc.F("Error.PortBusy", Port));

        if (BackendDir is null) throw new BackendException(Loc.T("Error.NoBackend"));
        if (!File.Exists(Path.Combine(BackendDir, "build", "server", "index.js")))
            throw new BackendException(Loc.F("Error.NotBuilt", BackendDir));
        NodePath = FindNode(BackendDir) ?? throw new BackendException(Loc.T("Error.NoNode"));

        Directory.CreateDirectory(AppSettings.DataDir);
        Directory.CreateDirectory(AppSettings.MediaDir);

        var start = new ProcessStartInfo(NodePath)
        {
            WorkingDirectory = BackendDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            // stdin stays open while the app runs; closing it asks the server to save and
            // exit (backend/server/lifecycle.ts).
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        start.ArgumentList.Add("server.js");
        start.Environment["PORT"] = Port.ToString();
        start.Environment["HOST"] = "127.0.0.1";
        start.Environment["ROV_USER_DATA_DIR"] = AppSettings.DataDir;
        start.Environment["ROV_USER_MEDIA_DIR"] = AppSettings.MediaDir;
        start.Environment["ROV_EXIT_WITH_PARENT"] = "1";

        var process = new Process { StartInfo = start, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) Append(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) Append("! " + e.Data); };
        process.Exited += (_, _) => OnProcessExited(process);

        Append($"-- starting {NodePath} server.js in {BackendDir}");
        if (!process.Start()) throw new BackendException(Loc.T("Error.Crashed"));
        _job.Add(process);
        _process = process;
        Attached = false;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var deadline = DateTime.UtcNow + StartTimeout;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            if (process.HasExited)
            {
                var tail = string.Join(Environment.NewLine, RecentLog().TakeLast(12));
                throw new BackendException(Loc.T("Error.Crashed") + Environment.NewLine + Environment.NewLine + tail);
            }

            var info = await ProbeAsync(ct);
            if (info?.App == AppId)
            {
                Info = info;
                return;
            }
            await Task.Delay(200, ct);
        }

        throw new BackendException(Loc.T("Error.Timeout"));
    }

    private void OnProcessExited(Process process)
    {
        int? code = null;
        try { code = process.ExitCode; } catch { /* not available */ }
        Append($"-- server exited (code {code?.ToString() ?? "?"})");
        if (!ReferenceEquals(process, _process) || _disposed) return;
        Info = null;
        Exited?.Invoke();
    }

    private async Task<AppInfo?> ProbeAsync(CancellationToken ct)
    {
        try
        {
            using var reply = await Probe.GetAsync(new Uri(BaseUri, "api/app-info"), ct);
            if (!reply.IsSuccessStatusCode) return null;
            return await reply.Content.ReadFromJsonAsync<AppInfo>(ApiClient.Json, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    // Two checks, because either alone can miss: binding fails when the port is held on
    // 127.0.0.1, and connecting succeeds when something listens on any address.
    private async Task<bool> IsPortTakenAsync()
    {
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, Port);
            listener.Start();
            listener.Stop();
        }
        catch (SocketException)
        {
            return true;
        }

        try
        {
            using var client = new TcpClient();
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
            await client.ConnectAsync(IPAddress.Loopback, Port, timeout.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindBackendDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; dir is not null && depth < 8; depth++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "backend");
            if (File.Exists(Path.Combine(candidate, "server.js"))) return candidate;
        }
        return null;
    }

    private static string? FindNode(string backendDir)
    {
        var bundled = Path.Combine(backendDir, "runtime", "node.exe");
        if (File.Exists(bundled)) return bundled;

        foreach (var entry in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            try
            {
                var candidate = Path.Combine(entry.Trim().Trim('"'), "node.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch
            {
                // A malformed PATH entry is not our problem to report.
            }
        }
        return null;
    }

    private void Append(string line)
    {
        lock (_log)
        {
            _log.Enqueue(line);
            while (_log.Count > LogLines) _log.Dequeue();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (_process is { HasExited: false })
            {
                // Ask first: the server flushes the last state change and closes the
                // database. Kill only if it does not go within a few seconds.
                _process.StandardInput.Close();
                if (!_process.WaitForExit(3000))
                {
                    _process.Kill(entireProcessTree: true);
                    _process.WaitForExit(2000);
                }
            }
        }
        catch
        {
            // Already gone. The job object is the backstop either way.
        }
        _job.Dispose();
    }
}
