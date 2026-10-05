using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

public sealed class SilverCreatureAssets
{
    private readonly Dictionary<uint, PSilverCreatureCombineTable> _combines = [];
    private readonly Dictionary<uint, PSilverCreatureGrowthTable> _growth = [];
    private readonly Dictionary<ulong, CSilverCreatureFightAttribute> _fight = [];
    private readonly AttributeColumns? _columns;

    public SilverCreatureAssets(
        IReadOnlyDictionary<string, PSilverCreatureCombineTable> combines,
        IReadOnlyDictionary<string, PSilverCreatureGrowthTable> growth,
        IReadOnlyDictionary<string, CSilverCreatureFightAttribute>? fight = null,
        AttributeColumns? columns = null
    )
    {
        _columns = columns;
        foreach (var row in fight?.Values ?? [])
        {
            _fight[row.Id] = row;
        }

        foreach (var row in combines.Values)
        {
            _combines[row.Id] = row;
        }

        foreach (var row in growth.Values)
        {
            _growth[row.Id] = row;
        }

        if (_combines.Count == 0)
            throw new ResourceException("P_SilverCreatureCombineTable.json", "p_silvercreaturecombinetable has no rows");

        if (_growth.Count == 0)
            throw new ResourceException("P_SilverCreatureGrowthTable.json", "p_silvercreaturegrowthtable has no rows");
    }

    public PSilverCreatureCombineTable? Combine(uint sourceItemId) => _combines.GetValueOrDefault(sourceItemId);
    public PSilverCreatureGrowthTable? Growth(uint itemId) => _growth.GetValueOrDefault(itemId);

    /// <summary>
    /// The level the client gives a silver creature (s_CSM_SC_SCData): Level + (world level - 1) * WorldLevelCoef from
    /// P_SilverCreatureGrowthTable.
    /// </summary>
    public uint LevelAt(uint silverCreatureId, uint worldLevel) =>
        _growth.GetValueOrDefault(silverCreatureId) is {} row
            ? row.Level + (Math.Max(worldLevel, 1) - 1) * row.WorldLevelCoef
            : 0;

    /// <summary>
    /// Fight attributes of a silver creature at a quality and level (C_SilverCreatureFightAttribute). Above the last
    /// level the table lists, the last one is used.
    /// </summary>
    public IReadOnlyList<AttributeModifier> FightAttributes(uint silverCreatureId, uint quality, uint level)
    {
        if (_columns is null || level == 0)
            return [];

        // The level takes the last two digits of the row ID.
        for (var at = Math.Min(level, 99u); at >= 1; at--)
        {
            if (_fight.GetValueOrDefault((ulong)silverCreatureId * 100000 + quality * 100UL + at) is {} row)
                return _columns.Of(row);
        }

        return [];
    }
}
