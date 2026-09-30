using Msg;
using Xunit;

namespace Lunaria.Tests;

public sealed partial class RoleSessionTests
{
    [Fact]
    public async Task RedPointRead_AloneTriggersPersistenceAndRemainsRoleScoped()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        await _store.SaveAsync(ctx.Player);
        ctx.Player.RedPoints.MarkExchangeActivityRead();
        Assert.True(ctx.Player.SaveDirty);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 2));
        Assert.False(ctx.Player.RedPoints.ExchangeActivityRead);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        Assert.True(ctx.Player.RedPoints.ExchangeActivityRead);
        ctx.Player.RedPoints.ClearDirty();
        ctx.Player.RedPoints.MarkExchangeActivityRead();
        Assert.False(ctx.Player.RedPoints.IsDirty);
    }

    [Fact]
    public async Task GuideReadAndCooldownDeadlineSurviveAnActualRoleReload()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var now = DateTimeOffset.UtcNow;
        var guide = _assets.Guides.All.First();
        Assert.True(ctx.Player.Guides.Unlock(guide, now.ToUnixTimeSeconds()));
        Assert.Equal(guide, Assert.Single(ctx.Player.ReadGuides([guide]).Changed));
        var deadline = ctx.Player.Cooldowns.Start(1, 3600, now);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 2));
        Assert.False(ctx.Player.Guides.IsUnlocked(guide));
        Assert.Equal(DateTimeOffset.UnixEpoch, ctx.Player.Cooldowns.ReadyAt(1));
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        Assert.Equal(EnmGuideState.Readed, Assert.Single(ctx.Player.Guides.InfosOf([guide])).State);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(deadline), ctx.Player.Cooldowns.ReadyAt(1));
    }
}
