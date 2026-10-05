using Lunaria.Game.Player;
using Lunaria.Game.Player.Persistence.Saves;
using Lunaria.Game.Resources;
using Lunaria.Game.Wanted;
using Msg;
using Xunit;

namespace Lunaria.Tests;

[Collection("bundled-gameplay")]
public sealed class WantedDefinitionTests(BundledGameplayFixture fixture)
{
    [Fact]
    public void ServerDefinitions_LoadWithClientEnumAndAllFourParameters()
    {
        Assert.Equal(29, fixture.Data.Wanted.Awards.Count);
        var definition = fixture.Data.Wanted.Award(201)!;
        Assert.Equal(50u, definition.Probability);
        Assert.Equal(EWantedAwardType.AddBlessSelect, definition.Behavior);
        Assert.Equal(new uint[] { 2, 4 }, fixture.Data.Wanted.Award(105)!.Param1);
        Assert.Equal(new uint[] { 1000001, 1000002, 1000003 }, fixture.Data.Wanted.Award(100002)!.Param4);
    }

    [Fact]
    public void Blesses_AreOnlyThoseTheClientCanShow()
    {
        // CBT1 server data also lists blesses 1000001-1000003, but the CBT1 client P_WantedPosterBlessTable does not:
        // its settlement screen (SortBless) fails on them and the player is left frozen after the run.
        Assert.DoesNotContain(fixture.Data.Wanted.AllBlesses, b => b.Id is >= 1000001 and <= 1000003);
    }

    [Fact]
    public void IndependentPercentageRolls_CanProduceSeveralAwardsOrNone()
    {
        var hit = At(1011, 1, new ControlledRandom(0));
        Assert.True(hit.OnBattleEnded(true).Completed);
        Assert.Single(hit.CaptureRun()!.Current.Awards);
        Assert.Single(hit.ToResource().BlessIds);

        var miss = At(1011, 1, new ControlledRandom(99));
        Assert.True(miss.OnBattleEnded(true).Completed);
        Assert.Empty(miss.ToResource().BlessIds);
        Assert.Empty(miss.CaptureRun()!.Current.Awards);
        Assert.Equal(2u, miss.CurrentStep);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void RandomAwards_GrantConfiguredCounts_AndUseConfiguredCurrencyAmounts(bool maximum, int expected)
    {
        var manager = At(1012, 2, new ControlledRandom(0, maximum));
        var result = manager.OnBattleEnded(true);
        Assert.True(result.Completed);
        Assert.Equal(expected, manager.ToResource().BlessIds.Count);
        Assert.Empty(manager.ToResource().RelicsIds);
        var currency = fixture.Data.Items.CurrencyItemFor((int)MoneyType.ThoughtSand);
        Assert.Contains(result.StepDrop, g => g.ItemId == currency && g.Count == 200);
        Assert.Equal(3u, manager.CurrentStep);
    }

    [Fact]
    public void ChoicesAndRedemption_SurviveSave_WithoutRerollOrRepeatPayment()
    {
        var player = new Player(1, fixture.Data);
        var manager = At(1012, 1, new ControlledRandom(0));
        Assert.True(manager.OnBattleEnded(true).Completed);
        player.Wanted.Load([], manager.CaptureRun());
        player.Progress.Load(1, 0, 0, 200, DateTimeOffset.UtcNow);
        Assert.Equal(0, player.WantedStaminaExchange().Result);
        var stamina = player.Progress.Stamina;
        var snapshot = RoleSaveMapper.Capture(player);
        var reloaded = new Player(2, fixture.Data);
        RoleSaveMapper.Apply(reloaded, snapshot);
        Assert.Equal(player.Wanted.ToStepNotification(), reloaded.Wanted.ToStepNotification());
        Assert.NotEqual(0, reloaded.WantedStaminaExchange().Result);
        Assert.Equal(stamina, reloaded.Progress.Stamina);
    }

    [Fact]
    public void SellingCreature_PaysSchemaRecyclingPriceOnce()
    {
        var player = new Player(1, fixture.Data);
        player.Characters.GrantStarter(player.Guid);
        player.Teams.GrantStarter(player.Characters);
        Assert.Equal(0, player.EnterWanted(10101));
        // CBT1 prices every creature at 0: selling pays the table price, once.
        var creature = fixture.Data.Wanted.AllCreatures.MaxBy(c => c.Price)!;
        player.Wanted.Load([], player.Wanted.CaptureRun()! with { Bionics = [new WantedBionics(creature.Id, 7)] });
        var balance = player.Wallet.Balance((int)MoneyType.ThoughtSand);
        Assert.Equal(0, player.GiveUpWantedBionics(7));
        Assert.Equal(balance + creature.Price, player.Wallet.Balance((int)MoneyType.ThoughtSand));
        Assert.NotEqual(0, player.GiveUpWantedBionics(7));
        Assert.Equal(balance + creature.Price, player.Wallet.Balance((int)MoneyType.ThoughtSand));
    }

    private WantedManager At(uint route, uint step, Random random)
    {
        var entry = fixture.Data.Wanted.AllPosters.SelectMany(fixture.Data.Wanted.EntriesOf)
            .First(e => e.FristRouteId == route || e.NormalRouteId == route);
        var manager = new WantedManager(fixture.Data, random);
        manager.Enter(entry.Id);
        var process = fixture.Data.Wanted.StepsOf(route, step).First();
        var eventId = fixture.Data.Policy.Wanted.EventPools[process.Pool][0];
        manager.Load([], manager.CaptureRun()! with {
            RouteId = route, Step = step,
            Current = new WantedStepSnapshot(EnmWantedStepStatus.EnmWssStart, process.Id, eventId, false, [])
        });
        return manager;
    }

    [Fact]
    public void BossCompletion_StartsConfiguredSettlementTask_Once()
    {
        var player = new Player(1, fixture.Data);
        var manager = At(1011, fixture.Data.Wanted.MaxStep(1011), new ControlledRandom(0));
        Assert.True(manager.OnBattleEnded(true).Completed);
        player.Wanted.Load([], manager.CaptureRun());
        var started = player.SettleServerTargets().SelectMany(o => o.AllNotifications).OfType<SCTaskProgressUpdateNtf>();
        Assert.Contains(started, n => n.UpdateType == EnmTaskActionUpdateType.EtaskActionUpdateTypeNew && n.UpdatedData.TaskId == 10);
        Assert.True(player.Tasks.IsProcessing(Lunaria.Game.Resources.TaskAssets.Wanted, 10));
        Assert.DoesNotContain(player.SettleServerTargets().SelectMany(o => o.AllNotifications).OfType<SCTaskProgressUpdateNtf>(),
            n => n.UpdateType == EnmTaskActionUpdateType.EtaskActionUpdateTypeNew && n.UpdatedData.TaskId == 10);
    }

    private sealed class ControlledRandom(int percentage, bool maximum = false) : Random
    {
        public override int Next(int maxValue) => maxValue == 100 ? percentage : 0;
        public override int Next(int minValue, int maxValue) => maximum ? maxValue - 1 : minValue;
        public override long NextInt64(long maxValue) => 0;
    }
}
