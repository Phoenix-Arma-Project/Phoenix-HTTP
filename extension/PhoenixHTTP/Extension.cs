using System.Text;
using PhoenixHttp.Arma;
using PhoenixHttp.Commands;
using PhoenixHttp.Core;
using PhoenixHttp.Http;

namespace PhoenixHttp;

/// <summary>
/// Process-wide composition root. It owns the long-lived services (config, logger, HTTP client,
/// queue and command router), wires them together once on load, and exposes the two operations the
/// native <see cref="EntryPoint"/> needs: dispatching a command and pushing an async result back to
/// the engine. Everything here is static because the engine loads exactly one instance of the DLL.
/// </summary>
public static unsafe class Extension
{
    /// <summary>Version string reported to the engine and useful for diagnosing deployments.</summary>
    public const string Version = "0.1.0";

    /// <summary>Name the engine tags every <c>ExtensionCallback</c> with, matched on the SQF side.</summary>
    public const string CallbackName = "PhoenixHttp";

    /// <summary>Absolute path of the loaded DLL, resolved once at startup.</summary>
    public static string DllPath { get; private set; } = string.Empty;

    /// <summary>Directory the DLL lives in; the search root for <c>config.json</c> and logs.</summary>
    public static string RootDirectory { get; private set; } = string.Empty;

    /// <summary>Active configuration. Replaced wholesale on reload, so reads are always consistent.</summary>
    public static Config Config { get; private set; } = Config.Empty;

    /// <summary>File logger, or the no-op logger until initialization has run.</summary>
    public static Logger Logger { get; private set; } = Logger.Null;

    /// <summary>Holds in-flight and completed transactions keyed by their id.</summary>
    public static RequestStore Store { get; } = new RequestStore();

    /// <summary>Shared HTTP client used for every outgoing request.</summary>
    public static Client Client { get; private set; } = null!;

    /// <summary>Bounds how many requests run at once and schedules the rest.</summary>
    public static RequestQueue Queue { get; private set; } = null!;

    /// <summary>Maps command verbs to their handlers; null until initialization completes.</summary>
    private static Router? router;

    /// <summary>Engine callback pointer used by <see cref="Invoke"/>, or zero before registration.</summary>
    private static IntPtr callback;

    /// <summary>Guards <see cref="Initialize"/> so the one-time wiring runs only once.</summary>
    private static bool initialized;

    /// <summary>
    /// Performs the one-time startup wiring. Safe to call more than once: the engine invokes the
    /// version hook before every other entry point, so we guard against repeating the work.
    /// </summary>
    /// <param name="dllPath">Absolute path of the loaded DLL, used to locate config and logs.</param>
    public static void Initialize(string dllPath)
    {
        if (initialized)
        {
            return;
        }

        initialized = true;

        DllPath = dllPath;
        RootDirectory = string.IsNullOrEmpty(dllPath)
            ? string.Empty
            : Path.GetDirectoryName(dllPath) ?? string.Empty;

        Config = Config.Load(RootDirectory);
        Logger = Logger.Create(RootDirectory);
        Logger.DebugEnabled = Config.Debug;
        Client = new Client();
        Queue = new RequestQueue(Config.MaxConcurrentRequests);
        router = new Router();

        Logger.Information($"Phoenix HTTP {Version} initialized at {RootDirectory}");
        Logger.Debug($"config: maxConcurrent={Config.MaxConcurrentRequests}, timeout={Config.RequestTimeoutSeconds}s, chunkSize={Config.ChunkSize}");
    }

    /// <summary>
    /// Re-reads <c>config.json</c> and applies the parts that can change at runtime (debug logging
    /// and the concurrency limit). The HTTP client is left untouched because its timeout is read
    /// per request from the fresh config.
    /// </summary>
    public static void Reload()
    {
        Config = Config.Load(RootDirectory);
        Logger.DebugEnabled = Config.Debug;
        Queue.SetMaxConcurrent(Config.MaxConcurrentRequests);
        Logger.Information("Configuration reloaded");
    }

    /// <summary>Stores the engine callback pointer so async results can be delivered later.</summary>
    /// <param name="engineCallback">Function pointer supplied by the engine.</param>
    public static void RegisterCallback(IntPtr engineCallback) => callback = engineCallback;

    /// <summary>Dispatches a command that carries no arguments.</summary>
    /// <param name="command">The command verb, for example <c>config:reload</c>.</param>
    /// <returns>The handler's reply string.</returns>
    public static string Call(string command) => Call(command, []);

    /// <summary>Dispatches a command with its arguments to the router.</summary>
    /// <param name="command">The command verb, for example <c>request:create</c>.</param>
    /// <param name="arguments">The decoded command arguments.</param>
    /// <returns>The handler's reply string.</returns>
    /// <exception cref="InvalidOperationException">Thrown if called before initialization completed.</exception>
    public static string Call(string command, string[] arguments)
    {
        if (router is null)
        {
            throw new InvalidOperationException("Extension was called before initialization completed.");
        }

        return router.Execute(command, arguments);
    }

    /// <summary>
    /// Pushes an asynchronous result into the game by invoking the engine callback, which raises the
    /// <c>ExtensionCallback</c> mission event handler on the next frame. No-ops until the engine has
    /// registered its callback. Failures are logged rather than thrown because there is no caller to
    /// surface them to.
    /// </summary>
    /// <param name="name">Identifier the script matches on; always <see cref="CallbackName"/>.</param>
    /// <param name="function">Logical channel, for example <c>response</c>.</param>
    /// <param name="data">Payload string passed to the script.</param>
    public static void Invoke(string name, string function, string data)
    {
        if (callback == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var invoke = (delegate* unmanaged[Cdecl]<byte*, byte*, byte*, int>)callback;

            fixed (byte* namePointer = Encode(name))
            fixed (byte* functionPointer = Encode(function))
            fixed (byte* dataPointer = Encode(data))
            {
                invoke(namePointer, functionPointer, dataPointer);
            }
        }
        catch (Exception exception)
        {
            Logger.Error($"Callback invocation failed: {exception}");
        }
    }

    /// <summary>Encodes a string as a null-terminated UTF-8 byte buffer for the engine callback.</summary>
    /// <param name="value">The string to encode.</param>
    /// <returns>A UTF-8 buffer ending in a single null byte.</returns>
    private static byte[] Encode(string value)
    {
        int length = Encoding.UTF8.GetByteCount(value);
        byte[] buffer = new byte[length + 1];
        Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, 0);
        return buffer;
    }

    /// <summary>
    /// Logs an unhandled exception from an entry point and returns a generic reply, so internal
    /// details never leak to the script while the full error is still captured in the log.
    /// </summary>
    /// <param name="exception">The exception that escaped an entry point.</param>
    /// <returns>A generic error reply for the engine.</returns>
    public static string DescribeFailure(Exception exception)
    {
        Logger.Error($"Unhandled failure: {exception}");
        return Reply.Error("internal failure");
    }
}
