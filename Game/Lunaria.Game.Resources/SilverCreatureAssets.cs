using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

public sealed class SilverCreatureAssets
{
    private readonly Dictionary<uint, PSilverCreatureCombineTable> _combines = [];
    private readonly Dictionary<uint, PSilverCreatureGrowthTable> _growth = [];

    public SilverCreatureAssets(
        IReadOnlyDictionary<string, PSilverCreatureCombineTable> combines,
        IReadOnlyDictionary<string, PSilverCreatureGrowthTable> growth
    )
    {
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
}
