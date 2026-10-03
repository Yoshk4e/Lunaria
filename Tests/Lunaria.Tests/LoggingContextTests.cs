using System.Text.RegularExpressions;
using Lunaria.Game.Logging;
using Lunaria.GameServer.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Lunaria.Tests;

[Collection("bundled-gameplay")]
public sealed class LoggingContextTests
{
    [Fact]
    public async Task ConcurrentSessions_KeepTheirContextAcrossAwaitAndLoggerCategories()
    {
        using var output = new LogOutputCapture();
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(new StylishConsoleLoggerProvider()));
        var sessionLog = factory.CreateLogger("Lunaria.GameServer.Session");
        var gameplayLog = factory.CreateLogger("Lunaria.Game.Inventory.Wallet");
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;

        async Task Run(ulong sessionId, long roleId)
        {
            using (sessionLog.BeginPlayerScope(sessionId, roleId))
            {
                if (Interlocked.Increment(ref entered) == 2) ready.SetResult();
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                await Task.Yield();
                gameplayLog.LogInformation("scoped action {Id}", sessionId);
            }
            gameplayLog.LogInformation("finished action {Id}", sessionId);
        }

        await Task.WhenAll(Run(101, 1001), Run(202, 2002));
        gameplayLog.LogInformation("outside sessions");

        Assert.EndsWith("[SessionId=101 RoleId=1001] scoped action 101", output.Line("scoped action 101"));
        Assert.EndsWith("[SessionId=202 RoleId=2002] scoped action 202", output.Line("scoped action 202"));
        Assert.DoesNotContain("SessionId=", output.Line("finished action 101"));
        Assert.DoesNotContain("SessionId=", output.Line("finished action 202"));
        Assert.DoesNotContain("SessionId=", output.Line("outside sessions"));
    }

    [Fact]
    public async Task NestedRoleScope_OverridesAndRestoresIdentityEvenAfterFailure()
    {
        using var output = new LogOutputCapture();
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(new StylishConsoleLoggerProvider()));
        var logger = factory.CreateLogger("Lunaria.Game.Persistence");
        using (logger.BeginPlayerScope(101, 1001))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () => {
                using var loading = logger.BeginPlayerScope(101, 2002);
                await Task.Yield();
                logger.LogError(new InvalidOperationException("load fault detail"), "target role load failed");
                throw new InvalidOperationException();
            });
            logger.LogInformation("previous role retained");
            using (logger.BeginPlayerScope(101))
                logger.LogInformation("role cleared");
        }
        logger.LogInformation("scope ended");

        Assert.EndsWith("[SessionId=101 RoleId=2002] target role load failed", output.Line("target role load failed"));
        Assert.Contains("[SessionId=101 RoleId=2002] InvalidOperationException load fault detail", output.Line("load fault detail"));
        Assert.EndsWith("[SessionId=101 RoleId=1001] previous role retained", output.Line("previous role retained"));
        Assert.EndsWith("[SessionId=101 RoleId=-] role cleared", output.Line("role cleared"));
        Assert.DoesNotContain("SessionId=", output.Line("scope ended"));
        Assert.DoesNotContain("RoleId=1001", output.Line("target role load failed"));
    }
}

// This collection runs without other test collections because Console.Out is process wide.
internal sealed class LogOutputCapture : IDisposable
{
    private readonly TextWriter _previous = Console.Out;
    private readonly StringWriter _output = new();

    public LogOutputCapture() => Console.SetOut(TextWriter.Synchronized(_output));

    public string Line(string marker) => Assert.Single(
        Regex.Replace(_output.ToString(), "\u001b\\[[0-9]+m", "")
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.Contains(marker, StringComparison.Ordinal)));

    public void Dispose()
    {
        Console.SetOut(_previous);
        _output.Dispose();
    }
}
