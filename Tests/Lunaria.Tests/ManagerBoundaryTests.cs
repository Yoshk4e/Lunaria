using Lunaria.Game.Player;
using Lunaria.Game.Player.Managers;
using Lunaria.Game.Player.Persistence.Saves;
using Lunaria.Game.Resources;
using Msg;
using Xunit;

namespace Lunaria.Tests;

public sealed partial class RoleSessionTests
{
    [Fact]
    public void ShopPrice_MustNotWrapAnUnrepresentableBasketToFree()
    {
        var manager = new Lunaria.Game.Shop.ShopManager(_assets);
        var price = manager.PriceOf([
            new(1, 2147483648, new(1, 1, 1, 1, 1, uint.MaxValue, 0, 0, 0)),
            new(2, 2147483648, new(2, 1, 1, 1, 1, uint.MaxValue, 0, 0, 0)),
            new(3, 1, new(3, 1, 1, 1, 1, uint.MaxValue, 0, 0, 0)),
            new(4, 1, new(4, 1, 1, 1, 1, 1, 0, 0, 0))
        ], false);
        Assert.True(Assert.Single(price.Currencies).Item2 > 0);
        Assert.True(price.ExceedsBalanceLimit);
    }

    [Fact]
    public async Task ReleasedCreatureId_IsNeverReused_EvenAfterAnEmptyInventoryReload()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var manager = ctx.Player.SilverCreatures;
        var (collected, first) = manager.TryCollect(29900001);
        Assert.True(collected);
        Assert.Equal(0, manager.Release([first!.UniqId]).Result);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 2));
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        manager = ctx.Player.SilverCreatures;
        var next = manager.TryCollect(29900001);
        Assert.True(next.Collected);
        Assert.True(next.Added!.UniqId > first.UniqId);
        Assert.NotEqual(0, manager.Release([first.UniqId]).Result);
        Assert.Single(manager.Creatures);
    }

    [Fact]
    public void ExhaustedCreatureIds_RefuseAcquisitionWithoutThrowingOrLosingSources()
    {
        var manager = new SilverCreatureManager(_assets);
        manager.Load([(uint.MaxValue, 29900001u, 1u)], 0);
        Assert.False(manager.TryCollect(29900001).Collected);
        Assert.Single(manager.Creatures);
    }

    [Fact]
    public async Task SuspendedTrialTeam_KeepsDamageAcrossRunAndReconnect()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var player = ctx.Player;
        EnterStoryTeam(player);
        var trial = player.CurrentTeamMembers()[0];
        player.SetTeamCharacterVitals(trial, hp: 1, liquid: 3);
        var before = player.CurrentTeamData();
        Assert.Equal(0, player.EnterWanted(10101));
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 2));
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        Assert.Equal(0, ctx.Player.LeaveWanted());
        Assert.Equal(before, ctx.Player.CurrentTeamData());
    }
}
