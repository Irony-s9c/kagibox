using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.UI.Dispatching;

namespace KagiBox.Core;

/// <summary>
/// Runs KagiBox-core (the Python service that owns the vault) and talks to it with one JSON message
/// per line over stdin / stdout. Replies complete the matching <see cref="Call"/>; events are raised on
/// the UI thread through <see cref="Event"/>. Closing stdin makes the core wipe the key and exit.
/// </summary>
public sealed class CoreClient
{
    public static CoreClient Instance { get; } = new();

    /// <summary>The core is Python: argument and field names are snake_case.</summary>
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private Process? _process;
    private StreamWriter? _stdin;
    private long _nextId;
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonNode?>> _pending = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private DispatcherQueue? _ui;

    /// <summary>Raised on the UI thread: (event name, payload).</summary>
    public event Action<string, JsonNode?>? Event;

    /// <summary>Raised on the UI thread when the core process ends.</summary>
    public event Action<int>? Exited;

    public bool IsRunning => _process is { HasExited: false };

    public void Start(DispatcherQueue ui)
    {
        _ui = ui;
        var (exe, arguments) = FindCore();
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardInputEncoding = new UTF8Encoding(false),
            WorkingDirectory = AppContext.BaseDirectory,
        };
        foreach (var a in arguments) psi.ArgumentList.Add(a);
        _process = Process.Start(psi) ?? throw new InvalidOperationException("KagiBox-core を起動できませんでした");
        _process.EnableRaisingEvents = true;
        _process.Exited += (_, _) => Post(() => Exited?.Invoke(_process.ExitCode));
        _stdin = _process.StandardInput;
        _stdin.AutoFlush = true;
        new Thread(ReadLoop) { IsBackground = true, Name = "core-stdout" }.Start();
        new Thread(() =>
        {
            // Keep stderr drained so the core never blocks on it.
            try { while (_process.StandardError.ReadLine() is not null) { } } catch { }
        }) { IsBackground = true, Name = "core-stderr" }.Start();
    }

    /// <summary>
    /// KagiBox-core.exe next to KagiBox.exe (installed build). During development the current source,
    /// core\kagibox_core.py, run with the project's .venv Python (so a stale build is never used);
    /// the PyInstaller output in build\core-dist only when there is no .venv.
    /// </summary>
    private static (string Exe, string[] Args) FindCore()
    {
        var dir = AppContext.BaseDirectory;
        var beside = Path.Combine(dir, "KagiBox-core.exe");
        if (File.Exists(beside)) return (beside, []);
        for (var d = new DirectoryInfo(dir); d is not null; d = d.Parent)
        {
            var script = Path.Combine(d.FullName, "core", "kagibox_core.py");
            if (!File.Exists(script)) continue;
            var python = Path.Combine(d.FullName, ".venv", "Scripts", "python.exe");
            if (File.Exists(python)) return (python, [script]);
            var built = Path.Combine(d.FullName, "build", "core-dist", "KagiBox-core", "KagiBox-core.exe");
            if (File.Exists(built)) return (built, []);
        }
        throw new FileNotFoundException("KagiBox-core.exe が見つかりません", beside);
    }

    private void ReadLoop()
    {
        var reader = _process!.StandardOutput;
        string? line;
        try
        {
            while ((line = reader.ReadLine()) is not null)
            {
                if (line.Length == 0) continue;
                JsonNode? msg;
                try { msg = JsonNode.Parse(line); }
                catch (JsonException) { continue; }
                if (msg is null) continue;

                if (msg["event"] is JsonNode ev)
                {
                    var name = ev.GetValue<string>();
                    var payload = msg["payload"];
                    Post(() => Event?.Invoke(name, payload));
                }
                else if (msg["id"] is JsonNode idNode && _pending.TryRemove(idNode.GetValue<long>(), out var tcs))
                {
                    if (msg["error"] is JsonNode err) tcs.TrySetException(new CoreException(err.GetValue<string>()));
                    else tcs.TrySetResult(msg["ok"]);
                }
            }
        }
        catch (IOException) { }
        foreach (var (_, tcs) in _pending) tcs.TrySetException(new CoreException("KagiBox の処理部分が終了しました"));
        _pending.Clear();
    }

    private void Post(Action a)
    {
        if (_ui is null || !_ui.TryEnqueue(() => a())) a();
    }

    /// <summary>Calls a core method. Arguments are an anonymous object; names become snake_case.</summary>
    public async Task<JsonNode?> Call(string method, object? args = null)
    {
        if (_stdin is null || !IsRunning) throw new CoreException("KagiBox の処理部分が動いていません");
        var id = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;
        var line = JsonSerializer.Serialize(new { id, method, args = args ?? new { } }, Json);
        await _writeLock.WaitAsync();
        try { await _stdin.WriteLineAsync(line); }
        finally { _writeLock.Release(); }
        return await tcs.Task;
    }

    public async Task<T> Call<T>(string method, object? args = null)
    {
        var node = await Call(method, args);
        return node is null ? default! : node.Deserialize<T>(Json)!;
    }

    /// <summary>Closing stdin tells the core that the UI is gone; it wipes the key and exits.</summary>
    public void CloseInput()
    {
        try { _stdin?.Close(); } catch { }
    }
}

public sealed class CoreException(string message) : Exception(message);
