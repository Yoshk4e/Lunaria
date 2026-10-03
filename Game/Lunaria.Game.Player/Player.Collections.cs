using Lunaria.Game.Logging;
using Lunaria.Game.Player.Gameplay;
using Msg;

namespace Lunaria.Game.Player;

public sealed record CollectionOutcome(OneCollectionData Item, RewardDelivery Delivery, int CollectType);

public sealed partial class Player
{
    public IReadOnlyList<OneCollectionData> GetCollections(ulong blockId, DateTimeOffset now)
    {
        using var operationTime = BeginOperation(now);
        return Collections.ListBlock(blockId, now)
            .Select(Collections.ToOneCollectionData)
            .ToList();
    }

    /// <summary>The client only re-lists a block when it loads it, so respawned objects are pushed.</summary>
    public SCCollectionDataNtf? RespawnedCollections(DateTimeOffset now)
    {
        using var operationTime = BeginOperation(now);
        var revived = Collections.RefreshDue(now);

        if (revived.Count == 0)
            return null;

        var ntf = new SCCollectionDataNtf();
        ntf.NtfList.AddRange(revived.Select(Collections.ToOneCollectionData));
        return ntf;
    }

    /// <summary>Latest state of an object, sent with refusals so the client does not stay in the opening state.</summary>
    public OneCollectionData? CollectionData(ulong uniq) =>
        Collections.Get(uniq) is {} node ? Collections.ToOneCollectionData(node) : null;

    public (int Code, CollectionOutcome? Outcome) Collect(ulong uniq, EnmCollectionOp op, DateTimeOffset now)
    {
        using var operationTime = BeginOperation(now);
        if (op is not (EnmCollectionOp.EnCollectionOpCollect or EnmCollectionOp.EnCollectionOpDestroy))
            return ((int)EnmTextCode.EnmTextCollectionOpIlegal, null);

        if (Collections.Get(uniq) is null)
            return ((int)EnmTextCode.EnmTextCollectionNoData, null);

        Collections.TryRefreshOne(uniq, now);

        if (Collections.Get(uniq) is not { Status: EnmCollectionStatus.EcsCanCollect } node)
            return ((int)EnmTextCode.EnmTextCollectionAlreadyOp, null);

        var level = assets.Maps.Map(node.Block)?.LevelPath;
        if (node.Block != Map.MapId && (string.IsNullOrEmpty(level) || level != assets.Maps.Map(Map.MapId)?.LevelPath))
        {
            Log.Flag("collection {Uniq} refused: block {Block} is not on map {Map} ({Phase})", uniq, node.Block, Map.MapId, Map.Phase);
            return ((int)EnmTextCode.EnmTextCollectionCondUnmeet, null);
        }

        // P_CollectionTable.Radius is the client's interaction sphere, but objects are also absorbed from afar (a
        // mailbox was taken at 8 m) and the synced position lags half a second, so UnlockCollectionRange is the floor.
        var range = Math.Max(
            assets.Collections.Radius(node.Cfg, assets.GlobalConfig.UnlockCollectionRange),
            assets.GlobalConfig.UnlockCollectionRange);
        var at = Map.Position;
        var dx = (double)at.X - node.X;
        var dy = (double)at.Y - node.Y;
        var dz = (double)at.Z - node.Z;

        if (dx * dx + dy * dy + dz * dz > (double)range * range)
        {
            Log.Flag(
                "collection {Uniq} refused: player at {Player} ({Phase}), object at {Object}, distance {Distance:F0} > range {Range}",
                uniq, at, Map.Phase, (node.X, node.Y, node.Z), Math.Sqrt(dx * dx + dy * dy + dz * dz), range);
            return ((int)EnmTextCode.EnmTextCollectionCondUnmeet, null);
        }

        if (op == EnmCollectionOp.EnCollectionOpDestroy)
        {
            var (destroyCode, destroyed) = Collections.ApplyDestroyed(uniq, now);

            if (destroyCode != 0 || destroyed is null)
                return (destroyCode, null);

            return (0, new CollectionOutcome(
                Collections.ToOneCollectionData(destroyed),
                RewardDelivery.Empty,
                assets.Collections.Get(destroyed.Cfg)?.CollectionType ?? 0));
        }

        // A missing definition is not an empty random roll. Keep the node and quota intact.
        if (!assets.Collections.CanResolveRewards(node.Cfg))
        {
            Log.Flag("collection {Uniq} refused: rewards of template {Cfg} cannot be resolved", uniq, node.Cfg);
            return ((int)EnmTextCode.EnmTextCollectionCondUnmeet, null);
        }

        var quota = Limits.Consume(assets.Collections.RewardLimitGroup(node.Cfg), count: 1, now);

        if (quota != 0)
            return (quota, null);

        var delivery = GrantRewards(assets.Collections.Rewards(node.Cfg, RandomSources.Loot), EnmItemReason.EnmItemChangeCollect);

        var (collectCode, collected) = Collections.ApplyCollected(uniq, now);

        if (collectCode != 0 || collected is null)
            return (collectCode, null);

        Gameplay.Publish(new CollectionGathered(assets.Collections.Get(collected.Cfg)?.CollectionType ?? 0));

        return (0, new CollectionOutcome(
            Collections.ToOneCollectionData(collected),
            delivery,
            assets.Collections.Get(collected.Cfg)?.CollectionType ?? 0));
    }
}
