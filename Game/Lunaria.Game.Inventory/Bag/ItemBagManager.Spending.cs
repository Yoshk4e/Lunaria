using Lunaria.Common.Tracking;
using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Inventory;

public sealed partial class ItemBagManager : TrackedObject
{
    public int Remove(uint itemId, uint count)
    {
        if (count == 0)
            return 0;

        if (!_stacks.TryGetValue(itemId, out var stack) || stack.Count < count)
            return (int)EnmTextCode.EnmTextItemNotEnough;

        stack.Count -= count;

        if (stack.Count == 0)
            _stacks.Remove(itemId);

        MarkChanged(itemId);
        return 0;
    }

    public bool CanAfford(IEnumerable<ItemGrant> cost) =>
        cost.GroupBy(grant => grant.ItemId)
            .All(group => CountOf(group.Key) >= group.Aggregate(seed: 0ul, (sum, grant) => sum + grant.Count));

    public int RemoveAll(IReadOnlyList<ItemGrant> cost)
    {
        if (!CanAfford(cost))
            return (int)EnmTextCode.EnmTextItemNotEnough;

        foreach (var grant in cost)
        {
            Remove(grant.ItemId, grant.Count);
        }

        return 0;
    }
}
