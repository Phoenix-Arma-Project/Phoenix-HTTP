using System.Text.Json;

namespace PhoenixHttp.Core;

/// <summary>
/// Strongly typed view of <c>config.json</c>. The numeric settings clamp themselves to safe lower
/// bounds as they are assigned, so a malformed or hostile config file can never put the extension
/// into a broken state (for example a zero concurrency limit, which would deadlock every request).
/// </summary>
public sealed class Config
{
    /// <summary>Backing field for <see cref="MaxConcurrentRequests"/>, kept at one or more.</summary>
    private readonly int maxConcurrentRequests = 8;

    /// <summary>Backing field for <see cref="RequestTimeoutSeconds"/>, kept at one or more.</summary>
    private readonly int requestTimeoutSeconds = 30;

    /// <summary>Backing field for <see cref="ChunkSize"/>, kept at one or more.</summary>
    private readonly int chunkSize = 8192;

    /// <summary>Named values substituted into requests through the <c>{{name}}</c> macro syntax.</summary>
    public Dictionary<string, string> Environments { get; init; } = new();

    /// <summary>How many requests may run at once; floored at one to keep the queue live.</summary>
    public int MaxConcurrentRequests
    {
        get => maxConcurrentRequests;
        init => maxConcurrentRequests = Math.Max(1, value);
    }

    /// <summary>Per-request timeout in seconds; floored at one to stay a positive duration.</summary>
    public int RequestTimeoutSeconds
    {
        get => requestTimeoutSeconds;
        init => requestTimeoutSeconds = Math.Max(1, value);
    }

    /// <summary>Maximum bytes per response chunk; floored at one so chunking always progresses.</summary>
    public int ChunkSize
    {
        get => chunkSize;
        init => chunkSize = Math.Max(1, value);
    }

    /// <summary>Whether debug-level log lines are written.</summary>
    public bool Debug { get; init; } = false;

    /// <summary>Shared all-defaults instance used before load and as the fallback on any error.</summary>
    public static Config Empty { get; } = new Config();

    /// <summary>
    /// Loads configuration from <c>config.json</c> in the given directory. A missing file is normal
    /// and yields defaults; a malformed file is swallowed and also yields defaults, because the
    /// extension must keep running with sane values rather than failing to load.
    /// </summary>
    /// <param name="directory">Directory to read <c>config.json</c> from.</param>
    /// <returns>The parsed configuration, or <see cref="Empty"/> when absent or invalid.</returns>
    public static Config Load(string directory)
    {
        string path = Path.Combine(directory, "config.json");
        if (!File.Exists(path))
        {
            return Empty;
        }

        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(path), ConfigJsonContext.Default.Config) ?? Empty;
        }
        catch
        {
            return Empty;
        }
    }
}
