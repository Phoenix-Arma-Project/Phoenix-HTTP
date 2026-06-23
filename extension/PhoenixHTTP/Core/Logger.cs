namespace PhoenixHttp.Core;

/// <summary>
/// Minimal append-only file logger. One log file is opened per process start, and writes are
/// serialized behind a lock so concurrent request workers never interleave a line. A <see cref="Null"/>
/// instance is used before initialization (and if the log directory cannot be created) so logging
/// calls are always safe to make and never throw.
/// </summary>
public sealed class Logger
{
    /// <summary>Target log file, or <see langword="null"/> for the no-op logger.</summary>
    private readonly string? path;

    /// <summary>Serializes writes so lines from different threads do not interleave.</summary>
    private readonly object gate = new object();

    /// <summary>Private to force construction through <see cref="Create"/> or <see cref="Null"/>.</summary>
    /// <param name="path">Target log file, or <see langword="null"/> to disable writing.</param>
    private Logger(string? path)
    {
        this.path = path;
    }

    /// <summary>Whether <see cref="Debug"/> lines are written; toggled from config at runtime.</summary>
    public bool DebugEnabled { get; set; }

    /// <summary>Shared logger that discards everything, used until a real one is created.</summary>
    public static Logger Null { get; } = new Logger(null);

    /// <summary>
    /// Creates a logger that writes to a timestamped file under <c>logs/</c> in the given directory.
    /// If the directory cannot be created the logger still returns and simply drops its writes, so a
    /// read-only install never breaks the extension.
    /// </summary>
    /// <param name="directory">Root directory under which the <c>logs/</c> folder is created.</param>
    /// <returns>A file logger; functional, or silently no-op if the path is unwritable.</returns>
    public static Logger Create(string directory)
    {
        string logsDirectory = Path.Combine(directory, "logs");
        try
        {
            Directory.CreateDirectory(logsDirectory);
        }
        catch
        {
        }

        string fileName = $"{DateTime.Now:yyyyMMddHHmmss}.log";
        return new Logger(Path.Combine(logsDirectory, fileName));
    }

    /// <summary>Writes an informational line.</summary>
    /// <param name="message">The message to record.</param>
    public void Information(string message) => Write("INFO", message);

    /// <summary>Writes an error line.</summary>
    /// <param name="message">The message to record.</param>
    public void Error(string message) => Write("ERROR", message);

    /// <summary>Writes a debug line, but only when <see cref="DebugEnabled"/> is set.</summary>
    /// <param name="message">The message to record.</param>
    public void Debug(string message)
    {
        if (DebugEnabled)
        {
            Write("DEBUG", message);
        }
    }

    /// <summary>
    /// Formats and appends a single timestamped line under the lock. Write failures are swallowed:
    /// losing a log line must never take down a request.
    /// </summary>
    /// <param name="level">Severity label, padded for column alignment.</param>
    /// <param name="message">The message to record.</param>
    private void Write(string level, string message)
    {
        if (path is null)
        {
            return;
        }

        string line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} [{level,-8}] {message}{Environment.NewLine}";
        try
        {
            lock (gate)
            {
                File.AppendAllText(path, line);
            }
        }
        catch
        {
        }
    }
}
