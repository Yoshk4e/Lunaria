using Lunaria.Game.Resources;

namespace Lunaria.Game.Player.Rewards;

internal sealed record RewardResolution(IReadOnlyList<ItemGrant> Items, IReadOnlyList<ItemGrant> Unresolved);

/// <summary>Expands item packs without mutating a player. Invalid packs remain recoverable as whole grants.</summary>
internal static class RewardResolver
{
    private const int MaxExpansion = 4096;

    public static RewardResolution Resolve(ItemAssets itemAssets, DropAssets drops, IEnumerable<ItemGrant> grants)
    {
        var items = new List<ItemGrant>();
        var unresolved = new List<ItemGrant>();
        foreach (var root in grants.Where(g => g.Count > 0))
        {
            var expanded = new List<ItemGrant>();
            var pending = new Queue<(ItemGrant Grant, uint[] Ancestors)>();
            pending.Enqueue((root, []));
            var budget = MaxExpansion;
            var valid = true;
            while (pending.TryDequeue(out var entry))
            {
                if (--budget < 0) { valid = false; break; }
                if (itemAssets.Get(entry.Grant.ItemId) is not
                    { AutoUse: true, UseType: (int)ItemUseType.AddDrop, Param.Count: > 0 } pack)
                {
                    expanded.Add(entry.Grant);
                    continue;
                }
                var contents = drops.Bundle(pack.Param[0]);
                if (entry.Ancestors.Contains(entry.Grant.ItemId) || contents.Count == 0)
                { valid = false; break; }
                uint[] ancestors = [.. entry.Ancestors, entry.Grant.ItemId];
                foreach (var content in contents)
                {
                    var count = (ulong)content.Count * entry.Grant.Count;
                    var chunks = (count + uint.MaxValue - 1) / uint.MaxValue;
                    if (chunks > (ulong)Math.Max(0, budget - pending.Count)) { valid = false; break; }
                    while (count > 0)
                    {
                        var chunk = (uint)Math.Min(count, uint.MaxValue);
                        pending.Enqueue((content with { Count = chunk }, ancestors));
                        count -= chunk;
                    }
                }
                if (!valid) break;
            }
            if (valid) items.AddRange(expanded);
            else unresolved.Add(root);
        }
        return new RewardResolution(items.ToArray(), unresolved.ToArray());
    }
}
