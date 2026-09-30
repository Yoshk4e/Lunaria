using Lunaria.Game.Characters;
using Lunaria.Game.Inventory;
using Lunaria.Game.Player;
using Lunaria.Game.World;
using Lunaria.Tests.Support;
using Msg;
using Xunit;

namespace Lunaria.Tests;

[Collection(AssetsCollection.Name)]
public sealed class UtilityManagerTests(TestAssets fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Cooldown_SharedTypeCannotBeShortenedByASecondStart()
    {
        var manager = new ItemCooldownManager();
        var first = manager.Start(1, 60, Now);
        Assert.Equal(first, manager.Start(1, 10, Now.AddSeconds(1)));
        Assert.Equal(Now.AddSeconds(60), manager.ReadyAt(1));
        Assert.Equal((uint)Now.AddSeconds(70).ToUnixTimeSeconds(), manager.Start(1, 60, Now.AddSeconds(10)));
    }

    [Fact]
    public void Cooldown_PublishedReadySecondMatchesTheServerDeadline()
    {
        var manager = new ItemCooldownManager();
        var started = Now.AddMilliseconds(750);
        var wire = manager.Start(1, 60, started);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(wire), manager.ReadyAt(1));
        Assert.True(manager.ReadyAt(1) >= started.AddSeconds(60));
        Assert.Equal(0u, manager.Start(1, 0, started));
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(wire), manager.ReadyAt(1));
    }

    [Fact]
    public void Map_TeleportMustBelongToTheDestinationMap()
    {
        var map = new MapManager(fixture.Data);
        Assert.Equal(0, map.UnlockTeleport(TestAssets.Teleport));
        map.ClearDirty();
        Assert.NotEqual(0, map.BeginEnter(TestAssets.SecondMap, TestAssets.Teleport).Code);
        Assert.Equal(TestAssets.StarterMap, map.MapId);
        Assert.Equal(0ul, map.TeleportId);
        Assert.False(map.IsDirty);
    }

    [Fact]
    public void Map_RespawnClearsThePreviousTeleportDestination()
    {
        var map = new MapManager(fixture.Data);
        map.UnlockTeleport(TestAssets.Teleport);
        Assert.Equal(0, map.BeginEnter(TestAssets.StarterMap, TestAssets.Teleport).Code);
        Assert.Equal(0, map.FinishEnter());
        map.Respawn();
        Assert.Equal(0ul, map.TeleportId);
        Assert.Equal(EnmBornPosType.EnmBornSavePoint, map.BornPosType);
        Assert.Equal(fixture.Data.Maps.SavepointPos(map.Savepoint), map.Position);
    }

    [Fact]
    public void Map_RebornPointStaysOnTheCurrentMapWithoutRebinding()
    {
        var map = new MapManager(fixture.Data);
        Assert.Equal((TestAssets.StarterMap, TestAssets.DefaultSavepoint), map.RebornPoint());

        // Bound point on another map and nothing unlocked here: the old pair is kept.
        Assert.Equal(0, map.BeginEnter(TestAssets.SecondMap, 0).Code);
        Assert.Equal((TestAssets.SecondMap, TestAssets.DefaultSavepoint), map.RebornPoint());

        // An unlocked point on the current map wins, and the bound one is left alone.
        Assert.Equal(0, map.UnlockSavepoint(11));
        Assert.Equal((TestAssets.SecondMap, 11ul), map.RebornPoint());
        Assert.Equal(TestAssets.DefaultSavepoint, map.Savepoint);
    }

    [Fact]
    public void Map_FirstPositionSyncAtTheArrivalCoordinatesStillMarksARealPosition()
    {
        var map = new MapManager(fixture.Data);
        Assert.Equal(0, map.BeginEnter(TestAssets.StarterMap, 0).Code);
        Assert.False(map.SyncPosition((99, 99, 99)));
        Assert.Equal(0, map.FinishEnter());
        map.ClearDirty();
        Assert.True(map.SyncPosition(map.Position));
        Assert.Equal(EnmBornPosType.EnmBornPosition, map.BornPosType);
        Assert.True(map.IsDirty);
        map.ClearDirty();
        Assert.True(map.SyncPosition(map.Position));
        Assert.False(map.IsDirty);
    }

    [Fact]
    public void Map_UnlockDoesNotRebindAndTrackedPinsRemainDistinctAcrossReload()
    {
        var map = new MapManager(fixture.Data);
        Assert.Equal(0, map.UnlockSavepoint(TestAssets.OtherSavepoint));
        Assert.Equal(TestAssets.DefaultSavepoint, map.Savepoint);
        Assert.Equal(0, map.BindSavepoint(TestAssets.OtherSavepoint));
        Assert.NotEqual(0, map.BindSavepoint(ulong.MaxValue));
        Assert.Equal(0, map.TrackTarget(TestAssets.StarterMap, 123, 1));
        Assert.Equal(0, map.TrackTarget(TestAssets.StarterMap, 123, 1));
        Assert.Equal(0, map.TrackTarget(TestAssets.SecondMap, 123, 1));
        Assert.NotEqual(0, map.TrackTarget(TestAssets.ClientOnlyMap, 123, 1));
        var restored = new MapManager(fixture.Data);
        restored.Load(map.MapId, map.Savepoint, map.UnlockedSavepoints, map.UnlockedTeleports, map.Position,
            map.TrackedTargets.Select(t => (t.MapId, t.TagId, t.TagType)));
        Assert.Equal(map.Savepoint, restored.Savepoint);
        Assert.Equal(2, restored.TrackedTargets.Count);
        Assert.Equal(0, restored.CancelTrackTarget(TestAssets.StarterMap, 123, 1));
        Assert.Equal(TestAssets.SecondMap, Assert.Single(restored.TrackedTargets).MapId);
    }

    [Fact]
    public void SkillGrowth_RejectsInsufficientPaymentAndStopsAtItsPricedCeiling()
    {
        var player = Fresh();
        var group = TestAssets.PricedGroup;
        var initial = player.Skills.GroupLevel(group);
        Assert.NotEqual(0, player.RaiseSkillGroup(group));
        Assert.Equal(initial, player.Skills.GroupLevel(group));
        player.Wallet.Credit(TestAssets.CoinMoneyType, 300);
        player.Bag.Add(TestAssets.MaterialItem, 3);
        Assert.Equal(0, player.RaiseSkillGroup(group));
        Assert.Equal(200, player.Wallet.Balance(TestAssets.CoinMoneyType));
        Assert.Equal(2u, player.Bag.CountOf(TestAssets.MaterialItem));
        Assert.Equal(0, player.RaiseSkillGroup(group));
        Assert.Equal(0, player.Wallet.Balance(TestAssets.CoinMoneyType));
        Assert.Equal(0u, player.Bag.CountOf(TestAssets.MaterialItem));
        Assert.NotEqual(0, player.RaiseSkillGroup(group));
        Assert.NotEqual(0, player.RaiseSkillGroup(TestAssets.LockedGroup));
        Assert.Equal(3u, player.Skills.GroupLevel(group));
    }

    [Fact]
    public void Talents_RequireOwnershipLevelAndParents_AndNotifyOnlySuccessfulUnlocks()
    {
        var player = Fresh();
        var owned = Assert.Single(player.Characters.All);
        Assert.NotEqual(0, player.UnlockTalent(ulong.MaxValue, 0));
        Assert.NotEqual(0, player.UnlockTalent(owned.InstId, 1));
        Assert.Empty(player.DrainGameplayChanges());
        player.Characters.Load([owned with { Level = 3 }]);
        Assert.NotEqual(0, player.UnlockTalent(owned.InstId, 1));
        Assert.Equal(0, player.UnlockTalent(owned.InstId, 0));
        Assert.NotEmpty(player.DrainGameplayChanges());
        Assert.Equal(0, player.UnlockTalent(owned.InstId, 1));
        Assert.NotEmpty(player.DrainGameplayChanges());
        Assert.NotEqual(0, player.UnlockTalent(owned.InstId, 1));
        Assert.NotEqual(0, player.UnlockTalent(owned.InstId, 128));
        Assert.Empty(player.DrainGameplayChanges());
    }

    [Fact]
    public void Guides_OnlyUnlockedEntriesCanBeReadAndNotificationIsNotRepeated()
    {
        var player = Fresh();
        Assert.Empty(player.ReadGuides([TestAssets.Guide, TestAssets.UnknownGuide]).Changed);
        Assert.Empty(player.DrainGameplayChanges());
        Assert.True(player.Guides.Unlock(TestAssets.Guide, Now.ToUnixTimeSeconds()));
        Assert.Equal(TestAssets.Guide, Assert.Single(player.ReadGuides([TestAssets.Guide, TestAssets.Guide, TestAssets.UnknownGuide]).Changed));
        Assert.Single(player.DrainGameplayChanges().OfType<SCUnlockGuideNtf>());
        Assert.Empty(player.ReadGuides([TestAssets.Guide]).Changed);
        Assert.Empty(player.DrainGameplayChanges());
    }

    private Player Fresh()
    {
        var player = new Player(1, fixture.Data);
        player.Characters.GrantStarter(player.Guid);
        player.Teams.GrantStarter(player.Characters);
        player.Skills.GrantStarter(player.Characters);
        return player;
    }
}
