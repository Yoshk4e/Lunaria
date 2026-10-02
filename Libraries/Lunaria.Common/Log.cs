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
    public static void Event(this ILogger logger, string message, params object?[] args) =>
        logger.LogTrace(message, args);

    public static void Stage(this ILogger logger, string message, params object?[] args) =>
        logger.LogDebug(message, args);

    public static void State(this ILogger logger, string message, params object?[] args) =>
        logger.LogInformation(message, args);

    public static void Flag(this ILogger logger, string message, params object?[] args) =>
        logger.LogWarning(message, args);
}
