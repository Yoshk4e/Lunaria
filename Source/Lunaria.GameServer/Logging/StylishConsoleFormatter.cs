using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Lunaria.GameServer.Logging;

internal enum Palette
{
    Dim = 90,
    Red = 91,
    Green = 92,
    Yellow = 93,
    Blue = 94,
    Magenta = 95,
    Cyan = 96
}

internal static class PaletteExtensions
{
    public static string Code(this Palette color) => $"\x1b[{(int)color}m";

    public static string Paint(this Palette color, string text) => $"\x1b[{(int)color}m{text}\x1b[0m";
}

public sealed class StylishConsoleLoggerProvider : ILoggerProvider
{
    private static readonly string NarrowTimeFormat = "HH:mm:ss.fff";

    private static readonly (string Tag, Palette Color)[] LevelStyles =
    [
        ("trc", Palette.Dim),
        ("dbg", Palette.Blue),
        ("INF", Palette.Green),
        ("WRN", Palette.Yellow),
        ("ERR", Palette.Red),
        ("CRT", Palette.Magenta)
    ];

    private readonly ConcurrentDictionary<string, StylishLogger> _loggers = new();

    public ILogger CreateLogger(string category) => _loggers.GetOrAdd(category, c => new StylishLogger(c));

    public void Dispose() => _loggers.Clear();

    private sealed class StylishLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel is not LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? error,
            Func<TState, Exception?, string> formatter
        )
        {
            if (!IsEnabled(logLevel))
                return;

            var (tag, color) = LevelStyles[(int)logLevel];
            var timestamp = DateTime.Now.ToString(NarrowTimeFormat);
            var label = Shorten(category);
            var message = formatter(state, error);
            var payload = string.IsNullOrWhiteSpace(message) ? "-" : message.ReplaceLineEndings(" | ");
            var line = new StringBuilder(160)
                .Append(timestamp).Append(' ')
                .Append(color.Code()).Append(tag).Append("\x1b[0m ")
                .Append(Palette.Dim.Code()).Append(label).Append("\x1b[0m ")
                .Append(payload)
                .ToString();

            Console.Out.WriteLine(line);

            if (error is not null)
                Console.Out.WriteLine(
                    $"     {Palette.Red.Paint(error.GetType().Name)} {error.Message.ReplaceLineEndings(" | ")}");
        }

        private static string Shorten(string category)
        {
            var cut = 0;

            for (var index = 0; index < category.Length; index++)
            {
                if (category[index] == '.' && index + 1 < category.Length)
                    cut = index + 1;
            }

            var head = category[cut..];
            return head.Length > 30 ? head[^30..] : head;
        }
    }
}
