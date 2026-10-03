using Lunaria.Common.Tracking;
using Lunaria.Game.Logging;
using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Inventory;

public sealed partial class ItemBagManager : TrackedObject
{
    public ItemAddResult Add(uint itemId, uint count)
    {
        if (count == 0)
            return ItemAddResult.Rejected((int)EnmTextCode.EnmTextItemUseZero);

        var limit = assets.Items.HoldLimit(itemId);

        if (limit == 0)
        {
            Log.Flag("bag grant refused for item {ItemId}, no valid holding limit", itemId);
            return ItemAddResult.Rejected((int)EnmTextCode.EnmTextTableNotFound);
        }

        var occupiesNewCell = !_stacks.ContainsKey(itemId);

        if (occupiesNewCell && IsFull)
        {
            Log.Stage("bag grant refused for item {ItemId} count {Count}, no free cell", itemId, count);
            return ItemAddResult.Rejected((int)EnmTextCode.EnmTextPackageFull);
        }

        var held = _stacks.TryGetValue(itemId, out var existing) ? existing.Count : 0;
        var room = limit - Math.Min(held, limit);
        var stored = Math.Min(count, room);
        var overflow = count - stored;
        if (overflow > 0)
            Log.Stage("bag grant limited for item {ItemId}, held {Held}, requested {Requested}, stored {Stored}, overflow {Overflow}, limit {Limit}",
                itemId, held, count, stored, overflow, limit);

        if (stored == 0)
            return new ItemAddResult((int)EnmTextCode.EnmTextItemHoldMax, Stored: 0, overflow);

        if (existing is null)
        {
            existing = new ItemStack { ItemId = itemId, Count = 0, IsNew = true };
            _stacks[itemId] = existing;
        }

        existing.Count = held + stored;
        existing.IsNew = true;

        MarkChanged(itemId);

        return new ItemAddResult(
            overflow == 0 ? 0 : (int)EnmTextCode.EnmTextItemHoldMax, stored, overflow);
    }

    public IReadOnlyList<ItemGrant> AddAll(IEnumerable<ItemGrant> grants)
    {
        var undelivered = new List<ItemGrant>();

        foreach (var grant in grants)
        {
            var result = Add(grant.ItemId, grant.Count);
            var missed = result.AnyStored ? result.Overflow : grant.Count;

            if (missed > 0)
                undelivered.Add(new ItemGrant(grant.ItemId, missed));
        }

        return undelivered;
    }
}
