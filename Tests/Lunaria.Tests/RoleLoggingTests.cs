using Lunaria.Game.Logging;
using Lunaria.GameServer.Logging;
using Lunaria.GameServer.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Lunaria.Tests;

public sealed partial class RoleSessionTests
{
    [Fact]
    public async Task SessionLogging_FollowsLoginRoleReplacementAndLogout()
    {
        using var output = new LogOutputCapture();
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(new StylishConsoleLoggerProvider()));
        var logger = factory.CreateLogger("Lunaria.Game.Player");
        var ctx = Context();
        using (logger.BeginPlayerScope(ctx.Player.SessionId, () => ctx.Player.Roles.Active()?.Id))
        {
            logger.LogInformation("before role login");
            Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
            logger.LogInformation("after first role login");
            Assert.Equal(0, await _sessions.ActivateAsync(ctx, 2));
            logger.LogInformation("after role switch");
            Assert.NotEqual(0, await _sessions.ActivateAsync(ctx, 999));
            logger.LogInformation("after rejected role switch");
            Assert.Equal(0, await _sessions.LogoutAsync(ctx, 2));
            logger.LogInformation("after role logout");
        }
        logger.LogInformation("outside player session");

        Assert.EndsWith("[SessionId=1 RoleId=-] before role login", output.Line("before role login"));
        Assert.EndsWith("[SessionId=1 RoleId=1] after first role login", output.Line("after first role login"));
        Assert.EndsWith("[SessionId=1 RoleId=2] after role switch", output.Line("after role switch"));
        Assert.EndsWith("[SessionId=1 RoleId=2] after rejected role switch", output.Line("after rejected role switch"));
        Assert.EndsWith("[SessionId=1 RoleId=-] after role logout", output.Line("after role logout"));
        Assert.DoesNotContain("SessionId=", output.Line("outside player session"));
    }

    [Fact]
    public async Task RouterFailure_IncludesPlayerIdentityAndDisposesScope()
    {
        using var output = new LogOutputCapture();
        using var services = RouterServices();
        var factory = services.GetRequiredService<ILoggerFactory>();
        factory.AddProvider(new StylishConsoleLoggerProvider());
        var router = services.GetRequiredService<Router>();
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));

        await Assert.ThrowsAsync<IOException>(() => router.DispatchAsync(ctx, [0x0a, 0xff]));
        factory.CreateLogger("Lunaria.GameServer.Router").LogInformation("after malformed request");

        Assert.Contains("[SessionId=1 RoleId=1] malformed CSMsgPkg", output.Line("malformed CSMsgPkg"));
        Assert.DoesNotContain("SessionId=", output.Line("after malformed request"));
    }
}
