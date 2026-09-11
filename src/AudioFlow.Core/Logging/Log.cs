using System.Text;

namespace AudioFlow.Core.Logging;

public enum LogLevel
{
    Debug,
    Info,
    Warn,
    Error
}

/// <summary>
/// Minimal thread-safe logger. Writes to the console and, optionally, to a file.
/// Never logs personal data: only audio/device/process information.
/// </summary>
public static class Log
{
    private static readonly object Sync = new();
    private static StreamWriter? _fileWriter;

    public static LogLevel MinimumLevel { get; set; } = LogLevel.Info;

    /// <summary>Raised for every accepted log line, so a UI can display it.</summary>
    public static event Action<LogLevel, string>? LineWritten;

    public static void UseFile(string path)
    {
        lock (Sync)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            _fileWriter?.Dispose();
            _fileWriter = new StreamWriter(path, append: true) { AutoFlush = true };
        }
    }

    public static void Debug(string message) => Write(LogLevel.Debug, message);
    public static void Info(string message) => Write(LogLevel.Info, message);
    public static void Warn(string message) => Write(LogLevel.Warn, message);
    public static void Error(string message) => Write(LogLevel.Error, message);

    public static void Error(Exception ex, string message) =>
        Write(LogLevel.Error, $"{message}: {ex.GetType().Name}: {ex.Message}");

    private static void Write(LogLevel level, string message)
    {
        if (level < MinimumLevel)
        {
            return;
        }

        var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] [{level.ToString().ToUpperInvariant()}] {message}";

        lock (Sync)
        {
            var previous = Console.ForegroundColor;
            Console.ForegroundColor = ColorFor(level);
            Console.WriteLine(line);
            Console.ForegroundColor = previous;

            try
            {
                _fileWriter?.WriteLine(line);
            }
            catch (IOException)
            {
                // Logging must never crash the application.
            }
        }

        LineWritten?.Invoke(level, message);
    }

    private static ConsoleColor ColorFor(LogLevel level) => level switch
    {
        LogLevel.Debug => ConsoleColor.DarkGray,
        LogLevel.Info => ConsoleColor.Gray,
        LogLevel.Warn => ConsoleColor.Yellow,
        LogLevel.Error => ConsoleColor.Red,
        _ => ConsoleColor.Gray
    };
}
