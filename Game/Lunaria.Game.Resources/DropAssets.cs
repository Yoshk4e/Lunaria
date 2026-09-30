using System.Collections.Frozen;
using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

public sealed class DropAssets
{
    private readonly FrozenDictionary<uint, ItemGrant[]> _bundles;

    public DropAssets(IReadOnlyDictionary<string, PFixedDropTable> rows)
    {
        var bundles = new Dictionary<uint, ItemGrant[]>();

        foreach (var group in rows.Values.Where(r => r.DropId != 0).GroupBy(r => r.DropId))
        {
            bundles[group.Key] = group
                .Where(r => r.ItemId != 0 && r.ItemCount > 0)
                .OrderBy(r => r.Id)
                .Select(r => new ItemGrant(r.ItemId, r.ItemCount))
                .ToArray();
        }

        if (bundles.Count == 0)
            throw new ResourceException("P_FixedDropTable.json", "p_fixeddroptable has no rows");

        _bundles = bundles.ToFrozenDictionary();
    }

    public int Count => _bundles.Count;

    public bool Exists(uint dropId) => _bundles.ContainsKey(dropId);

    public IReadOnlyList<ItemGrant> Bundle(uint dropId) =>
        _bundles.TryGetValue(dropId, out var bundle) ? bundle : [];

    public IReadOnlyList<ItemGrant> Bundles(IEnumerable<uint> dropIds) =>
        dropIds.SelectMany(Bundle).ToList();
}
