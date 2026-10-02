using Lunaria.Game.Player.Gameplay;
using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    public (int Result, SilverCreatureChange? Change) CombineSilverCreatures(IReadOnlyList<uint> ids)
    {
        using var operationTime = BeginOperation();
        var result = SilverCreatures.Combine(ids);
        if (result.Result == 0 && result.Change is {} change)
        {
            foreach (var added in change.AddList)
                Gameplay.Publish(new CreatureAcquired(added.ItemId, 1));
            change.AddList.AddRange(CollectStoredCreatures());
        }
        return result;
    }

    public (int Result, SilverCreatureChange? Change) ReleaseSilverCreatures(IReadOnlyList<uint> ids)
    {
        using var operationTime = BeginOperation();
        var result = SilverCreatures.Release(ids);
        if (result.Result == 0 && result.Change is {} change)
            change.AddList.AddRange(CollectStoredCreatures());
        return result;
    }

    private void RetryStoredCreatures()
    {
        var collected = CollectStoredCreatures();
        if (collected.Count > 0) Gameplay.Publish(new CreatureRosterChanged(collected));
    }

    private IReadOnlyList<CmdSilverCreatureItem> CollectStoredCreatures()
    {
        var collected = new List<CmdSilverCreatureItem>();
        var stored = Bag.All().Where(stack => assets.Items.Get(stack.ItemId) is
                { AutoUse: true, UseType: (int)ItemUseType.AddSilverCreature })
            .Select(stack => new ItemGrant(stack.ItemId, stack.Count)).ToArray();
        foreach (var grant in stored)
        {
            uint added = 0;
            while (added < grant.Count)
            {
                var (ok, creature) = SilverCreatures.TryCollect(grant.ItemId);
                if (!ok || creature is null) break;
                collected.Add(creature);
                added++;
            }
            if (added > 0) Bag.Remove(grant.ItemId, added);
        }

        if (collected.Count > 0)
        {
            // Acquisition was already counted when the reward entered the bag.
            // Update the roster without granting reward XP or publishing ItemAcquired again.
            Gameplay.Publish(new BagChanged(EnmItemReason.EnmItemChangeNormal));
            foreach (var group in collected.GroupBy(creature => creature.ItemId))
                Gameplay.Publish(new CreatureAcquired(group.Key, (uint)group.Count()));
        }
        return collected;
    }
}
