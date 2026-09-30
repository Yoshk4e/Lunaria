using System.Globalization;
using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

public readonly record struct ExposeBattleConfig(ulong BattleId, uint Weight);

public sealed record ExposeNpcGroupConfig(
    ulong NpcGroupId,
    IReadOnlyList<ExposeBattleConfig> Battles
);

public sealed class ExposeAssets
{
    private const string SubregionFile = "P_SubRegionNPCGroup.json";
    private const string BattleFile = "P_NPCGroupEnterBattle.json";
    private readonly Dictionary<ulong, IReadOnlyList<ExposeNpcGroupConfig>> _subregions = [];

    public ExposeAssets(
        IReadOnlyDictionary<string, PSubRegionNPCGroup> subregions,
        IReadOnlyDictionary<string, PNPCGroupEnterBattle> battles,
        IReadOnlyDictionary<string, CNPCGroupTable> npcGroups
    )
    {
        if (subregions.Count == 0)
            throw new ResourceException(SubregionFile, "p_subregionnpcgroup has no rows");

        if (battles.Count == 0)
            throw new ResourceException(BattleFile, "p_npcgroupenterbattle has no rows");

        var knownNpcGroups = npcGroups.Values.Select(row => row.Id).ToHashSet();
        var battlesByGroup = new Dictionary<ulong, IReadOnlyList<ExposeBattleConfig>>();

        foreach (var row in battles.Values)
        {
            if (row.NpcGroupId == 0 || !knownNpcGroups.Contains(row.NpcGroupId))
                throw new ResourceException(BattleFile,
                    $"row {row.Id} references an unknown NPC group {row.NpcGroupId}");

            var parsed = row.EnterBattleList.Select(value => ParseBattle(row.Id, value)).ToArray();

            if (parsed.Length == 0)
                throw new ResourceException(BattleFile, $"row {row.Id} has no enter battles");

            if (!battlesByGroup.TryAdd(row.NpcGroupId, parsed))
                throw new ResourceException(BattleFile,
                    $"multiple rows describe NPC group {row.NpcGroupId}");
        }

        foreach (var row in subregions.Values)
        {
            if (row.Id == 0 || row.NpcGroupIdList.Count == 0)
                throw new ResourceException(SubregionFile, $"subregion row {row.Id} is unusable");

            if (row.NpcGroupIdList.Distinct().Count() != row.NpcGroupIdList.Count)
                throw new ResourceException(SubregionFile, $"subregion {row.Id} repeats an NPC group");

            var groups = new ExposeNpcGroupConfig[row.NpcGroupIdList.Count];

            for (var i = 0; i < row.NpcGroupIdList.Count; i++)
            {
                var groupId = row.NpcGroupIdList[i];

                if (!battlesByGroup.TryGetValue(groupId, out var groupBattles))
                    throw new ResourceException(SubregionFile,
                        $"subregion {row.Id} references NPC group {groupId} with no battle row");

                groups[i] = new ExposeNpcGroupConfig(groupId, groupBattles);
            }

            if (!_subregions.TryAdd(row.Id, groups))
                throw new ResourceException(SubregionFile, $"duplicate subregion {row.Id}");
        }
    }

    public int SubregionCount => _subregions.Count;

    public IReadOnlyList<ExposeNpcGroupConfig>? ForSubregion(ulong subregion) =>
        _subregions.GetValueOrDefault(subregion);

    private static ExposeBattleConfig ParseBattle(ulong rowId, string value)
    {
        var parts = value.Split(separator: ',', StringSplitOptions.TrimEntries);

        if (parts.Length != 2
            || !ulong.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var battleId)
            || !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var weight)
            || battleId == 0)
        {
            throw new ResourceException(BattleFile, $"row {rowId} has malformed enter battle '{value}'");
        }
        return new ExposeBattleConfig(battleId, weight);
    }
}
