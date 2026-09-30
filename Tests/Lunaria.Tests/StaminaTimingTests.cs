using Lunaria.Game.Player;
using Lunaria.Game.Progression;
using Lunaria.Game.Resources;
using Msg;
using Xunit;

namespace Lunaria.Tests;

[Collection("bundled-gameplay")]
public sealed class StaminaTimingTests(BundledGameplayFixture fixture)
{
    private GameData Assets => fixture.Data;
    private static readonly DateTimeOffset Start = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
    private int Interval => Assets.GlobalConfig.StaminaRegenInterval;

    private ProgressManager Meter(int stamina)
    {
        var meter = new ProgressManager(Assets);
        meter.Load(1, 0, 0, stamina, Start);
        return meter;
    }

    [Fact]
    public void Spend_UsesEarnedStaminaAndPreservesPartialInterval()
    {
        var meter = Meter(0);
        var now = Start.AddSeconds(Interval * 2.5);
        Assert.Equal(0, meter.SpendStamina(2, now));
        Assert.Equal(0, meter.Stamina);
        Assert.Equal(Start.AddSeconds(Interval * 2), meter.StaminaTickAt);
        Assert.Equal(1, meter.Regenerate(Start.AddSeconds(Interval * 3)));
    }

    [Fact]
    public void ItemGrant_SettlesNaturalRegenerationBeforeAdding()
    {
        var meter = Meter(0);
        Assert.Equal(0, meter.AddStamina(10, Start.AddSeconds(Interval * 2.5)));
        Assert.Equal(12, meter.Stamina);
        Assert.Equal(Start.AddSeconds(Interval * 2), meter.StaminaTickAt);
    }

    [Fact]
    public void ReachingNaturalCap_RetiresIdleTimeBeforeTheNextSpend()
    {
        var meter = Meter(Assets.GlobalConfig.StaminaRegenMax - 1);
        var now = Start.AddSeconds(Interval * 2.5);
        Assert.Equal(1, meter.Regenerate(now));
        Assert.Equal(now, meter.StaminaTickAt);
        Assert.Equal(0, meter.SpendStamina(1, now));
        Assert.Equal(0, meter.Regenerate(now.AddSeconds(Interval - 1)));
        Assert.Equal(1, meter.Regenerate(now.AddSeconds(Interval)));
    }

    [Fact]
    public void BackwardClock_SpendingFullMeterCannotReopenEarnedTime()
    {
        var meter = Meter(Assets.GlobalConfig.StaminaRegenMax);
        Assert.Equal(0, meter.SpendStamina(2, Start.AddSeconds(-Interval * 2)));
        Assert.Equal(Start, meter.StaminaTickAt);
        Assert.Equal(0, meter.Regenerate(Start));
    }

    [Fact]
    public void InvalidAmount_DoesNotSettleTime_InsufficientFundsStillKeepsEarnedStamina()
    {
        var meter = Meter(0);
        var now = Start.AddSeconds(Interval * 2);
        Assert.NotEqual(0, meter.SpendStamina(0, now));
        Assert.NotEqual(0, meter.AddStamina(-1, now));
        Assert.False(meter.IsDirty);
        Assert.NotEqual(0, meter.SpendStamina(3, now));
        Assert.Equal(2, meter.Stamina);
        Assert.Equal(now, meter.StaminaTickAt);
    }

    [Fact]
    public void ItemUse_PreservesAccruedStaminaAndPublishesFinalBalance()
    {
        var player = new Player(1, Assets);
        var now = DateTimeOffset.UtcNow;
        player.Progress.Load(1, 0, 0, 0, now.AddSeconds(-Interval * 2.5));
        player.Bag.Add(21308001, 1);
        var gain = Assets.Items.StaminaOf(21308001)!.Value;
        Assert.Equal(0, player.UseItem(21308001, 1, []).Code);
        Assert.Equal(gain + 2, player.Progress.Stamina);
        Assert.Equal(0u, player.Bag.CountOf(21308001));
        Assert.Contains(player.DrainGameplayChanges().OfType<SCPlayerAttrUpdateNtf>().SelectMany(n => n.UpdateAttrs),
            a => a.AttrType == (int)PlayerAttrType.EnmPlayerAttrStaminaCur && a.ValueInt32 == gain + 2);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void DungeonEntry_SettlesStaminaForEntryAndReconnect(bool adopt, bool sufficient)
    {
        var player = new Player(1, Assets);
        player.Characters.GrantStarter(player.Guid);
        player.Teams.GrantStarter(player.Characters);
        const ulong dungeonId = 201001;
        var cost = (int)Assets.Dungeons.Type(Assets.Dungeons.Dungeon(dungeonId)!.DungeonType)!.VitalityCost;
        var now = DateTimeOffset.UtcNow;
        player.Progress.Load(1, 0, 0, sufficient ? cost - 1 : 0, now.AddSeconds(-Interval * 1.5));
        var result = adopt ? player.AdoptDungeonCurrent(dungeonId, Assets.Dungeons.Dungeon(dungeonId)!.BattleId[0])
            : player.EnterDungeon(dungeonId);
        Assert.Equal(sufficient, result.Code == 0);
        Assert.Equal(sufficient ? 0 : 1, player.Progress.Stamina);
        Assert.Equal(sufficient, player.Dungeons.Current is not null);
        Assert.Equal(sufficient ? (uint)cost : 0u, result.StaminaSpent);
        Assert.Contains(player.DrainGameplayChanges().OfType<SCPlayerAttrUpdateNtf>().SelectMany(n => n.UpdateAttrs),
            a => a.AttrType == (int)PlayerAttrType.EnmPlayerAttrStaminaCur && a.ValueInt32 == player.Progress.Stamina);
    }

    [Fact]
    public void ItemUse_CalculatesConsumptionAfterNaturalRegeneration()
    {
        var player = new Player(1, Assets);
        var now = DateTimeOffset.UtcNow;
        var gain = Assets.Items.StaminaOf(21308001)!.Value;
        player.Progress.Load(1, 0, 0, 0, now.AddSeconds(-Interval * 1.5));
        player.Bag.Add(21308001, 10000);
        var uses = (uint)Math.Ceiling((double)(player.Progress.StaminaMax - 1) / gain);
        var result = player.UseItem(21308001, uint.MaxValue, []);
        Assert.Equal(0, result.Code);
        Assert.Equal(10000u - uses, player.Bag.CountOf(21308001));
        Assert.Equal(player.Progress.StaminaMax, player.Progress.Stamina);
    }
}
