using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

public sealed class SkillAssets
{
    private readonly Dictionary<uint, uint> _ceilings = [];
    private readonly Dictionary<(uint Group, uint Level), SkillCost> _costs = [];
    private readonly Dictionary<uint, PSkillGrowthTable> _groups = [];

    public SkillAssets(
        IReadOnlyDictionary<string, PSkillGrowthTable> groups,
        IReadOnlyDictionary<string, PSkillGrowthCostTable> costs
    )
    {
        foreach (var row in groups.Values)
        {
            _groups[row.Id] = row;
        }

        if (_groups.Count == 0)
            throw new ResourceException("P_SkillGrowthTable.json", "p_skillgrowthtable has no rows");

        foreach (var row in costs.Values)
        {
            _costs[(row.GrowthId, row.Level)] = new SkillCost(Pair(row.CostItemId, row.CostItemNum), row.CostCoinNum);

            if (row.Level > _ceilings.GetValueOrDefault(row.GrowthId))
                _ceilings[row.GrowthId] = row.Level;
        }
    }

    public bool GroupExists(uint group) => _groups.ContainsKey(group);

    public uint InitLevel(uint group) => _groups.GetValueOrDefault(group)?.InitLevel ?? 0;

    public uint MaxLevel(uint group) =>
        _ceilings.TryGetValue(group, out var ceiling) ? Math.Max(ceiling, InitLevel(group)) : InitLevel(group);

    public SkillCost? CostOf(uint group, uint level) => _costs.GetValueOrDefault((group, level));

    public IReadOnlyList<uint> Skills(uint group) => _groups.GetValueOrDefault(group)?.Skills ?? [];

    private static IReadOnlyList<ItemGrant> Pair(List<uint> ids, List<uint> counts) =>
        ids.Take(Math.Min(ids.Count, counts.Count))
            .Select((id, index) => new ItemGrant(id, counts[index]))
            .Where(grant => grant.ItemId != 0 && grant.Count > 0)
            .ToList();
}
