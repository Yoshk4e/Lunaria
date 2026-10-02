using Lunaria.Game.Collections;
using Lunaria.Game.Player.Gameplay;
using Msg;

namespace Lunaria.Game.Player;

public sealed record CollectionOutcome(OneCollectionData Item, RewardDelivery Delivery, int CollectType);

public sealed partial class Player
{
    public IReadOnlyList<OneCollectionData> GetCollections(ulong blockId, DateTimeOffset now)
    {
        using var operationTime = BeginOperation(now);
        Collections.EnsureBlock(blockId, now, Map.Position, Guid.Next);

        return Collections.ListBlock(blockId, now)
            .Select(CollectionManager.ToOneCollectionData)
            .ToList();
    }

    public (int Code, CollectionOutcome? Outcome) Collect(ulong uniq, EnmCollectionOp op, DateTimeOffset now)
    {
        using var operationTime = BeginOperation(now);
        if (op is not (EnmCollectionOp.EnCollectionOpCollect or EnmCollectionOp.EnCollectionOpDestroy))
            return ((int)EnmTextCode.EnmTextCollectionOpIlegal, null);

        if (Collections.Get(uniq) is null)
            return ((int)EnmTextCode.EnmTextCollectionNoData, null);

        Collections.TryRefreshOne(uniq, now);

        if (op == EnmCollectionOp.EnCollectionOpDestroy)
        {
            var (destroyCode, destroyed) = Collections.ApplyDestroyed(uniq, now);

            if (destroyCode != 0 || destroyed is null)
                return (destroyCode, null);

            return (0, new CollectionOutcome(
                CollectionManager.ToOneCollectionData(destroyed),
                RewardDelivery.Empty,
                assets.Collections.Get(destroyed.Cfg)?.CollectionType ?? 0));
        }

        if (Collections.Get(uniq) is not { Status: EnmCollectionStatus.EcsCanCollect } node)
            return ((int)EnmTextCode.EnmTextCollectionAlreadyOp, null);

        var range = assets.Collections.Radius(node.Cfg, assets.GlobalConfig.UnlockCollectionRange);
        var at = Map.Position;
        var dx = (double)at.X - node.X;
        var dy = (double)at.Y - node.Y;
        var dz = (double)at.Z - node.Z;

        if (dx * dx + dy * dy + dz * dz > (double)range * range)
            return ((int)EnmTextCode.EnmTextCollectionCondUnmeet, null);

        // A missing definition is not an empty random roll. Keep the node and quota intact.
        if (!assets.Collections.CanResolveRewards(node.Cfg))
            return ((int)EnmTextCode.EnmTextCollectionCondUnmeet, null);

        var quota = Limits.Consume(assets.Collections.RewardLimitGroup(node.Cfg), count: 1, now);

        if (quota != 0)
            return (quota, null);

        var delivery = GrantRewards(assets.Collections.Rewards(node.Cfg, RandomSources.Loot), EnmItemReason.EnmItemChangeCollect);

        var (collectCode, collected) = Collections.ApplyCollected(uniq, now);

        if (collectCode != 0 || collected is null)
            return (collectCode, null);

        Gameplay.Publish(new CollectionGathered(assets.Collections.Get(collected.Cfg)?.CollectionType ?? 0));

        return (0, new CollectionOutcome(
            CollectionManager.ToOneCollectionData(collected),
            delivery,
            assets.Collections.Get(collected.Cfg)?.CollectionType ?? 0));
    }
}
