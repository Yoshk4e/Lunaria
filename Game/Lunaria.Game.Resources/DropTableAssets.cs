using System.Collections.Frozen;
using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

internal sealed record DropGroup(uint DropId, uint GroupId, IReadOnlyList<SDropTable> Rows);

/// <summary>Fixed rows roll independently in basis points. Weighted rows make one pick per group.</summary>
public sealed class DropTableAssets
{
    private readonly FrozenDictionary<uint, List<uint>> _dropIdsOfItem;
    private readonly FrozenDictionary<uint, List<DropGroup>> _drops;

    public DropTableAssets(IReadOnlyDictionary<string, SDropTable> rows, ItemAssets items)
    {
        var drops = new Dictionary<uint, List<DropGroup>>();

        foreach (var group in rows.Values
                     .Where(row => items.Exists(row.ItemId) && row.ItemCount > 0)
                     .GroupBy(row => (row.DropId, row.GroupId)))
        {
            (drops.TryGetValue(group.Key.DropId, out var groups) ? groups : drops[group.Key.DropId] = []).Add(new DropGroup(group.Key.DropId,
                group.Key.GroupId, [.. group]));
        }

        if (drops.Count == 0)
            throw new ResourceException("S_DropTable.json", "s_droptable has no usable rows");

        var dropIdsOfItem = new Dictionary<uint, List<uint>>();

        foreach (var groups in drops.Values)
        foreach (var group in groups)
        foreach (var row in group.Rows)
        {
            if (!dropIdsOfItem.TryGetValue(row.ItemId, out var ids))
                dropIdsOfItem[row.ItemId] = ids = [];

            if (!ids.Contains(group.DropId))
                ids.Add(group.DropId);
        }

        _drops = drops.ToFrozenDictionary();
        _dropIdsOfItem = dropIdsOfItem.ToFrozenDictionary();
    }

    public bool Exists(uint dropId) => _drops.ContainsKey(dropId);

    public IReadOnlyList<uint> DropIdsOf(uint itemId) =>
        _dropIdsOfItem.TryGetValue(itemId, out var ids) ? ids : [];

    public IReadOnlyList<ItemGrant> Roll(uint dropId, Random random)
    {
        if (!_drops.TryGetValue(dropId, out var groups))
            return [];

        var result = new List<ItemGrant>();

        foreach (var group in groups)
        {
            var weighted = group.Rows.Where(row => row.Weight is not null).ToList();

            if (weighted.Count > 0)
            {
                var total = weighted.Sum(row => row.Weight!.Value);

                if (total > 0)
                {
                    var pick = random.NextInt64(total);

                    foreach (var row in weighted)
                    {
                        pick -= row.Weight!.Value;

                        if (pick < 0)
                        {
                            result.Add(new ItemGrant(row.ItemId, row.ItemCount));
                            break;
                        }
                    }
                }
            }

            foreach (var row in group.Rows.Where(row => row.Odds is not null))
            {
                if (row.Odds!.Value > 0 && random.Next(10_000) < row.Odds!.Value)
                    result.Add(new ItemGrant(row.ItemId, row.ItemCount));
            }
        }

        return result;
    }
}
