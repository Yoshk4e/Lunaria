using Lunaria.Game.Buff;
using Lunaria.Game.Limits;
using Lunaria.Game.Player;
using Lunaria.Tests.Support;
using Msg;
using Xunit;

namespace Lunaria.Tests;

[Collection(AssetsCollection.Name)]
public sealed class ManagerStateRegressionTests(TestAssets fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ScheduledBuffExpiry_PublishesRemovalOnce()
    {
        var player = new Player(1, fixture.Data);
        player.Buffs.Apply(TestAssets.TimedBuff, Now);
        player.AdvanceTime(Now.AddSeconds(60));
        var notification = Assert.Single(player.DrainGameplayChanges().OfType<SCBuffDel>());
        Assert.Equal(TestAssets.TimedBuff, notification.Id);
        player.AdvanceTime(Now.AddSeconds(61));
        Assert.Empty(player.DrainGameplayChanges().OfType<SCBuffDel>());
        Assert.Empty(player.Buffs.Buffs);
    }

    [Fact]
    public void BattleBuff_SurvivesReloadAndExpiresAfterExactlyItsRemainingBattles()
    {
        var buffs = new BuffManager(fixture.Data);
        var applied = buffs.Apply(110013, Now);
        var duration = applied.Updated!.LeftBattle;
        Assert.True(duration > 1);
        Assert.Empty(buffs.BattleEnded(Now).Removed);
        var restored = new BuffManager(fixture.Data);
        restored.Load(buffs.Buffs.Values.Select(b => (b.BuffId, b.AttachedAt.ToUnixTimeSeconds(), b.LeftBattle)), Now);
        for (var i = duration - 1; i > 1; i--)
        {
            var result = restored.BattleEnded(Now);
            Assert.Equal(i - 1, Assert.Single(result.Updated).LeftBattle);
            Assert.Empty(result.Removed);
        }
        Assert.Equal(110013u, Assert.Single(restored.BattleEnded(Now).Removed));
        Assert.Empty(restored.Buffs);
    }

    [Fact]
    public void Quota_BackwardClockCannotReopenAnAlreadyConsumedDay()
    {
        var limits = new LimitGroupManager(fixture.Data);
        Assert.Equal(0, limits.Consume(TestAssets.DailyGroup, 1, Now));
        Assert.Equal(0, limits.Consume(TestAssets.DailyGroup, 1, Now.AddDays(-1)));
        Assert.Equal(2u, limits.CountOf(TestAssets.DailyGroup, Now));
        Assert.NotEqual(0, limits.Consume(TestAssets.DailyGroup, 1, Now));
        Assert.Equal(0u, limits.CountOf(TestAssets.DailyGroup, Now.AddDays(1)));
    }

    [Fact]
    public void UnlimitedQuota_SaturatesItsDisplayCounterWithoutRefusingConsumption()
    {
        var limits = new LimitGroupManager(fixture.Data);
        limits.Load([(TestAssets.UnlimitedGroup, uint.MaxValue - 1, Now)]);
        Assert.Equal(0, limits.Consume(TestAssets.UnlimitedGroup, 2, Now));
        Assert.Equal(uint.MaxValue, limits.CountOf(TestAssets.UnlimitedGroup, Now));
        Assert.Equal(0, limits.Consume(TestAssets.UnlimitedGroup, 1, Now));
    }

    [Fact]
    public void TimedBuff_ReapplicationAfterExpiryIsANewApplication()
    {
        var buffs = new BuffManager(fixture.Data);
        Assert.Equal(0, buffs.Apply(TestAssets.TimedBuff, Now).Result);
        var live = buffs.Apply(TestAssets.TimedBuff, Now.AddSeconds(30));
        Assert.True(live.Refreshed);
        Assert.Equal(30, live.Updated!.LeftTime);

        var reapplied = buffs.Apply(TestAssets.TimedBuff, Now.AddSeconds(60));
        Assert.Equal(0, reapplied.Result);
        Assert.False(reapplied.Refreshed);
        Assert.Equal(60, reapplied.Updated!.LeftTime);
        Assert.Equal(TestAssets.TimedBuff, Assert.Single(reapplied.Removed));
        Assert.Single(buffs.ToBuffData(Now.AddSeconds(61)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BattleBuff_LoadDropsExhaustedCounters(int remaining)
    {
        var buffs = new BuffManager(fixture.Data);
        buffs.Load([(110013u, Now.ToUnixTimeSeconds(), remaining)], Now);
        Assert.Empty(buffs.ToBuffData(Now));
    }

    [Fact]
    public void BattleEnd_RemovesTimedBuffsThatExpiredBeforeTheScheduledSweep()
    {
        var buffs = new BuffManager(fixture.Data);
        buffs.Apply(TestAssets.TimedBuff, Now);
        var result = buffs.BattleEnded(Now.AddSeconds(60));
        Assert.Equal(TestAssets.TimedBuff, Assert.Single(result.Removed));
        Assert.Empty(result.Updated);
        Assert.Empty(buffs.Buffs);
        Assert.Empty(buffs.BattleEnded(Now.AddSeconds(61)).Removed);
    }
}
