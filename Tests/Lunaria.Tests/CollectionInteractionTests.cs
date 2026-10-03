using Lunaria.Game.Player;
using Lunaria.Game.Player.Gameplay;
using Lunaria.Game.Player.Persistence.Saves;
using Lunaria.Game.Resources;
using Lunaria.Game.Resources.Tables;
using Lunaria.GameServer.Handlers.Recv;
using Lunaria.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Msg;
using Xunit;

namespace Lunaria.Tests;

[Collection("bundled-gameplay")]
public sealed class CollectionInteractionTests(BundledGameplayFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
    private GameData Assets => fixture.Data;

    private PWorldCollectObjTable Herb => Assets.Collections.WorldObjects(0)
        .First(row => row.TemplateId == 1010101 && row.CollectUnlockType == 0);

    private Player At(PWorldCollectObjTable placed, ulong? map = null)
    {
        var player = new Player(1, Assets);
        player.Characters.GrantStarter(player.Guid);
        player.Teams.GrantStarter(player.Characters);
        player.Map.Load(map ?? placed.BlockId, Assets.Starter.Savepoint, [], [],
            ((int)MathF.Round(placed.PosX), (int)MathF.Round(placed.PosY), (int)MathF.Round(placed.PosZ)));
        return player;
    }

    [Theory]
    [InlineData(EnmCollectionOp.EnCollectionOpCollect)]
    [InlineData(EnmCollectionOp.EnCollectionOpDestroy)]
    public void DistantPlacement_CannotBeCollectedOrDestroyed(EnmCollectionOp op)
    {
        var placed = Herb;
        var player = At(placed);
        var position = player.Map.Position;
        player.Map.Load(placed.BlockId, Assets.Starter.Savepoint, [], [],
            (position.X + 1_000_000, position.Y, position.Z));

        Assert.Equal((int)EnmTextCode.EnmTextCollectionCondUnmeet, player.Collect(placed.Id, op, Now).Code);
        Assert.Empty(player.Collections.Entries);
        Assert.Empty(player.Limits.Entries);
    }

    [Fact]
    public void SmallInteractionRadius_StillAcceptsAnAbsorbWithinTheUnlockRange()
    {
        var placed = Assets.Collections.WorldObjects(0).First(row =>
            Assets.Collections.Get(row.TemplateId) is { Radius: > 0 } template
            && template.Radius < Assets.GlobalConfig.UnlockCollectionRange
            && row.CollectUnlockType == 0 && Assets.Collections.CanResolveRewards(row.TemplateId));
        var player = At(placed);
        var position = player.Map.Position;
        player.Map.Load(placed.BlockId, Assets.Starter.Savepoint, [], [],
            (position.X + Assets.GlobalConfig.UnlockCollectionRange - 1, position.Y, position.Z));

        Assert.Equal(0, player.Collect(placed.Id, EnmCollectionOp.EnCollectionOpCollect, Now).Code);
    }

    [Theory]
    [InlineData(EnmCollectionOp.EnCollectionOpCollect)]
    [InlineData(EnmCollectionOp.EnCollectionOpDestroy)]
    public void MatchingCoordinatesOnAnotherLevel_DoNotAllowInteraction(EnmCollectionOp op)
    {
        var placed = Herb;
        var foreignMap = Assets.Collections.WorldObjects(0)
            .First(row => Assets.Maps.Map(row.BlockId)?.LevelPath != Assets.Maps.Map(placed.BlockId)?.LevelPath).BlockId;
        var player = At(placed, foreignMap);

        Assert.Equal((int)EnmTextCode.EnmTextCollectionCondUnmeet, player.Collect(placed.Id, op, Now).Code);
        Assert.Empty(player.Collections.Entries);
        Assert.Empty(player.Limits.Entries);
    }

    [Theory]
    [InlineData(EnmCollectionOp.EnCollectionOpCollect)]
    [InlineData(EnmCollectionOp.EnCollectionOpDestroy)]
    public void LockedPlacement_RemainsLockedAfterListingAndLoading(EnmCollectionOp op)
    {
        var placed = Assets.Collections.WorldObjects(0).First(row => row.CollectUnlockType != 0);
        var player = At(placed);
        Assert.Equal(EnmCollectionStatus.EcsLock,
            player.GetCollections(placed.BlockId, Now).Single(row => row.UniqId == placed.Id).Status);
        Assert.NotEqual(0, player.Collect(placed.Id, op, Now).Code);
        Assert.Empty(player.Collections.Entries);

        player.Collections.Load([(placed.Id, placed.TemplateId, (int)EnmCollectionStatus.EcsCanCollect, Now, 0UL, (0, 0, 0))]);
        Assert.Equal(EnmCollectionStatus.EcsLock, player.CollectionData(placed.Id)!.Status);
        Assert.NotEqual(0, player.Collect(placed.Id, op, Now).Code);
        Assert.Empty(player.Limits.Entries);
    }

    [Fact]
    public void NearbyPlacement_InAnotherSubregionOnTheSameLevel_CanBeCollected()
    {
        var placed = Herb;
        var otherBlock = Assets.Collections.WorldObjects(0).First(row => row.BlockId != placed.BlockId
            && Assets.Maps.Map(row.BlockId)?.LevelPath == Assets.Maps.Map(placed.BlockId)?.LevelPath).BlockId;
        var player = At(placed, otherBlock);

        var result = player.Collect(placed.Id, EnmCollectionOp.EnCollectionOpCollect, Now);

        Assert.Equal(0, result.Code);
        Assert.Equal(1u, player.OwnedItemCount(41030001));
        Assert.NotNull(result.Outcome);
    }

    [Fact]
    public void OpenedChest_CannotGrantAgainAfterSaveReload()
    {
        var placed = Assets.Collections.WorldObjects(0).First(row => row.TemplateId == 202 && row.CollectUnlockType == 0);
        var player = At(placed);
        Assert.Equal(0, player.Collect(placed.Id, EnmCollectionOp.EnCollectionOpCollect, Now).Code);
        var saved = RoleSaveMapper.Capture(player);
        var restored = At(placed);
        RoleSaveMapper.Apply(restored, saved);

        Assert.NotEqual(0, restored.Collect(placed.Id, EnmCollectionOp.EnCollectionOpCollect, Now.AddDays(8)).Code);
        Assert.Equal(EnmCollectionStatus.EcsDestroyed, restored.CollectionData(placed.Id)!.Status);
        Assert.Empty(restored.DrainGameplayChanges());
    }
}

[Collection(AssetsCollection.Name)]
public sealed class CollectionRewardValidationTests(TestAssets fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    private Player Fresh()
    {
        var player = new Player(1, fixture.Data, random: new GameplayRandom(loot: new FirstRoll()));
        player.Map.Load(TestAssets.StarterMap, TestAssets.DefaultSavepoint, [], [], (0, 0, 0));
        return player;
    }

    [Theory]
    [InlineData(7100ul)] // missing direct drop
    [InlineData(7110ul)] // missing chest group
    [InlineData(7120ul)] // unknown collection in the chest group
    [InlineData(7130ul)] // missing drop on a chest reward
    public void MissingRewardDefinition_PreservesTheNodeQuotaAndInventory(ulong id)
    {
        var player = Fresh();
        var before = player.InventoryItems();

        var result = player.Collect(id, EnmCollectionOp.EnCollectionOpCollect, Now);

        Assert.NotEqual(0, result.Code);
        Assert.Null(result.Outcome);
        Assert.Equal(EnmCollectionStatus.EcsCanCollect, player.CollectionData(id)!.Status);
        Assert.Empty(player.Collections.Entries);
        Assert.Empty(player.Limits.Entries);
        Assert.Equal(before, player.InventoryItems());
        Assert.Equal(0ul, player.OwnedItemCount(TestAssets.CurrencyItem));
        Assert.Empty(player.DrainGameplayChanges());
    }

    [Fact]
    public void ValidChest_UsesInjectedRandomnessAndConsumesOneAllowance()
    {
        var player = Fresh();

        Assert.Equal(0, player.Collect(7000, EnmCollectionOp.EnCollectionOpCollect, Now).Code);
        Assert.Equal(3u, player.OwnedItemCount(TestAssets.MaterialItem));
        Assert.Equal(50u, player.OwnedItemCount(TestAssets.CurrencyItem));
        Assert.Equal(1u, player.Limits.Entries[TestAssets.DailyGroup].Count);
    }

    private sealed class FirstRoll : Random
    {
        public override int Next(int maxValue) => 0;
        public override long NextInt64(long maxValue) => 0;
    }
}

public sealed partial class RoleSessionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CollectionRefusal_IncludesTheRequestedObject(bool activeRole, bool knownObject)
    {
        var ctx = Context();
        if (activeRole) Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var id = knownObject ? _assets.Collections.WorldObjects(0)[0].Id : ulong.MaxValue;

        var response = await new HandleCollectionOperate(NullLogger<HandleCollectionOperate>.Instance)
            .OnPacket(ctx, new CSCollectionOperate { UniqId = id, Op = EnmCollectionOp.EnCollectionOpCollect });

        Assert.NotEqual(0, response.Result);
        Assert.NotNull(response.CollectionItem);
        Assert.Equal(id, response.CollectionItem.UniqId);
        if (knownObject) Assert.Equal(ctx.Player.CollectionData(id), response.CollectionItem);
    }
}
