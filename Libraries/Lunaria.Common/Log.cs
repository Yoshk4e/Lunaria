using System.Collections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lunaria.Game.Logging;


public static class GameLog
{
    private static volatile ILoggerFactory _factory = NullLoggerFactory.Instance;

    public const string Category = "Lunaria.Game";

    public static void Configure(ILoggerFactory factory) =>
        _factory = factory ?? NullLoggerFactory.Instance;

    public static ILogger Create(string category) => _factory.CreateLogger(category);
}

public static class GameLogExtensions
{
    public static IDisposable? BeginPlayerScope(this ILogger logger, ulong sessionId, long? roleId = null) =>
        logger.BeginPlayerScope(sessionId, () => roleId);

    // Read the active role when a message is written because login and logout can replace the player.
    public static IDisposable? BeginPlayerScope(this ILogger logger, ulong sessionId, Func<long?> activeRole) =>
        logger.BeginScope(new PlayerScope(sessionId, activeRole));

    public static void Event(this ILogger logger, string message, params object?[] args) =>
        logger.LogTrace(message, args);

    public static void Stage(this ILogger logger, string message, params object?[] args) =>
        logger.LogDebug(message, args);

    public static void State(this ILogger logger, string message, params object?[] args) =>
        logger.LogInformation(message, args);

    public static void Flag(this ILogger logger, string message, params object?[] args) =>
        logger.LogWarning(message, args);

    private sealed class PlayerScope(ulong sessionId, Func<long?> activeRole) : IReadOnlyList<KeyValuePair<string, object?>>
    {
        public int Count => 2;

        public KeyValuePair<string, object?> this[int index] => index switch {
            0 => new("SessionId", sessionId),
            1 => new("RoleId", activeRole()),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            yield return this[0];
            yield return this[1];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() => $"SessionId={sessionId} RoleId={activeRole()?.ToString() ?? "-"}";
    }
}
