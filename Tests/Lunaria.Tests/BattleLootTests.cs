using System.Text.Json;
using Lunaria.Game.Battle;
using Lunaria.Game.Player;
using Lunaria.Game.Player.Persistence.Saves;
using Msg;
using Xunit;

namespace Lunaria.Tests;

[Collection("bundled-gameplay")]
public sealed class BattleLootTests(BundledGameplayFixture fixture)
{
    // Patrol battlefield 109101201 (harpy) belongs to reward group 2: packs 320041, 320042 and 320043.
    private const uint HarpyField = 109101201;
    private const uint HarpyCluster = 11210799;
    private static readonly DateTimeOffset Start = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private (Player Player, ManualClock Clock) Fresh()
    {
        var clock = new ManualClock(Start);
        var player = new Player(1, fixture.Data, clock);
        player.Characters.GrantStarter(player.Guid);
        player.Teams.GrantStarter(player.Characters);
        return (player, clock);
    }

    private static Player.BattleLeaveOutcome Fight(
        Player player, EBattleResultType result, EnmMonsterFromType from = EnmMonsterFromType.EmonsterFromTable,
        EBattleType type = EBattleType.EnmBattleTypePatrol, uint field = HarpyField, uint cluster = HarpyCluster)
    {
        // CBT1 patrol entries carry no identifiers; the leave report names the cluster.
        Assert.Equal(0, player.EnterBattle(type, field, 0, default));
        Assert.Equal(0, player.StartBattle(type, field));
        return player.LeaveBattle(new CSLeaveBattle {
            BattleType = type, BattleFieldId = field, BattleResult = result,
            BattleInstId = cluster, MonsterFromType = from
        });
    }

    [Theory]
    [InlineData(1u, 320041u)]
    [InlineData(2u, 320042u)]
    [InlineData(3u, 320043u)]
    [InlineData(9u, 320043u)]
    public void RewardTier_IsTheHighestLowerLimitNotAboveTheWorldLevel(uint worldLevel, uint drop) =>
        Assert.Equal(drop, fixture.Data.BattleRewards.DropFor(HarpyField, worldLevel));

    [Fact]
    public void RewardTier_UnknownBattlefieldHasNoLoot()
    {
        Assert.Equal(0u, fixture.Data.BattleRewards.DropFor(1000103, 1));
        Assert.Equal(0u, fixture.Data.BattleRewards.DropFor(HarpyField, 0));
    }

    [Fact]
    public void PatrolVictory_GrantsTheWorldLevelPackAndShowsItAsBattleLoot()
    {
        var (player, _) = Fresh();
        Assert.Equal(1u, player.Progress.WorldLevel);
        var gold = player.OwnedItemCount(1);
        var material = player.OwnedItemCount(14020002);

        var outcome = Fight(player, EBattleResultType.EnmBattleResultTypeSuccess);

        Assert.Equal(0, outcome.Result);
        Assert.Equal(EnmItemReason.EnmItemChangeBattleEnd, outcome.BattleReward.Reason);
        Assert.Equal(gold + 200, player.OwnedItemCount(1));
        Assert.Equal(material + 2, player.OwnedItemCount(14020002));
        var show = Assert.Single(outcome.BattleReward.Presentation.OfType<SCAwardShowNtf>());
        Assert.Equal(EnmItemReason.EnmItemChangeBattleEnd, show.Source);
        Assert.Equal([(1u, 200u), (14020002u, 2u)],
            show.Items.Select(item => (item.ItemId, (uint)item.ItemCount)).OrderBy(pair => pair.ItemId));
    }

    [Fact]
    public void PatrolVictory_CoolsTheClusterDownAcrossASaveAndBringsItBackAfterTheDelay()
    {
        var (player, clock) = Fresh();

        var outcome = Fight(player, EBattleResultType.EnmBattleResultTypeSuccess);

        var down = Assert.Single(outcome.Notifications.OfType<SCPatrolMonsterNtf>());
        Assert.Equal((HarpyCluster, false), (down.MonsterId, down.CdIsOk));
        Assert.Equal([(long)HarpyCluster], player.PatrolCooldowns);
        Assert.True(player.SaveDirty);

        var json = JsonSerializer.Serialize(RoleSaveMapper.Capture(player), SaveJson.Options);
        var restored = new Player(2, fixture.Data, clock);
        RoleSaveMapper.Apply(restored, JsonSerializer.Deserialize<RoleSaveDocument>(json, SaveJson.Options)!);
        Assert.Equal([(long)HarpyCluster], restored.PatrolCooldowns);

        clock.Now = Start + BattleManager.PatrolRespawnDelay - TimeSpan.FromSeconds(1);
        Assert.Empty(restored.AdvanceTime(clock.Now).OfType<SCPatrolMonsterNtf>());
        Assert.Equal([(long)HarpyCluster], restored.PatrolCooldowns);

        clock.Now = Start + BattleManager.PatrolRespawnDelay;
        var up = Assert.Single(restored.AdvanceTime(clock.Now).OfType<SCPatrolMonsterNtf>());
        Assert.Equal((HarpyCluster, true), (up.MonsterId, up.CdIsOk));
        Assert.Empty(restored.PatrolCooldowns);
        Assert.Empty(restored.AdvanceTime(clock.Now + TimeSpan.FromMinutes(1)).OfType<SCPatrolMonsterNtf>());
    }

    [Fact]
    public void PatrolDefeat_GivesNoLootAndNoCooldown()
    {
        var (player, _) = Fresh();
        var gold = player.OwnedItemCount(1);

        var outcome = Fight(player, EBattleResultType.EnmBattleResultTypeDeadFail);

        Assert.False(outcome.BattleReward.HasChanges);
        Assert.Empty(outcome.Notifications);
        Assert.Empty(player.PatrolCooldowns);
        Assert.Equal(gold, player.OwnedItemCount(1));
    }

    [Fact]
    public void QuestGroupVictory_GivesLootWithoutCooldown()
    {
        var (player, _) = Fresh();

        var outcome = Fight(player, EBattleResultType.EnmBattleResultTypeSuccess, EnmMonsterFromType.EmonsterFromTask);

        Assert.True(outcome.BattleReward.HasChanges);
        Assert.Empty(outcome.Notifications);
        Assert.Empty(player.PatrolCooldowns);
    }

    [Fact]
    public void ResetMonster_ClearsTheCooldownBeforeItEnds()
    {
        var (player, _) = Fresh();
        Fight(player, EBattleResultType.EnmBattleResultTypeSuccess);

        Assert.True(player.Battles.ResetMonster(HarpyCluster));

        Assert.Empty(player.PatrolCooldowns);
        Assert.False(player.Battles.ResetMonster(HarpyCluster));
    }
}
