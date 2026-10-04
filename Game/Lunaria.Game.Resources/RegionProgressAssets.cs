using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

/// <summary>Type determines whether ParamId lists POI tasks, collections, teleports, or creature growth IDs.</summary>
public sealed record RegionSequence(uint Id, uint Type, IReadOnlyList<uint> ParamId, uint ParamNum);

public sealed class RegionProgressAssets
{
    private readonly Dictionary<ulong, uint> _regionOf = [];
    private readonly Dictionary<uint, PRegionTable> _regions = [];
    private readonly Dictionary<uint, PRegionProgressTypeRegistTable> _registByType = [];
    private readonly Dictionary<uint, PRegionRewardDataTable> _rewardData = [];
    private readonly Dictionary<ulong, PRegionRewardTable> _rewards = [];
    private readonly Dictionary<(ulong SubRegion, uint Sequence), RegionSequence> _sequenceRows = [];
    private readonly Dictionary<ulong, List<uint>> _sequences = [];
    private readonly Dictionary<ulong, List<ulong>> _blockSubRegions = [];

    private readonly Dictionary<ulong, uint> _subRegionNames = [];

    public RegionProgressAssets(
        IReadOnlyDictionary<string, PRegionProgressTable> progress,
        IReadOnlyDictionary<string, PRegionRewardTable> rewards,
        IReadOnlyDictionary<string, PRegionRewardDataTable> rewardData,
        IReadOnlyDictionary<string, PRegionSequenceTable> sequences,
        IReadOnlyDictionary<string, PSubRegionTable> subRegions,
        IReadOnlyDictionary<string, PRegionTable> regions,
        IReadOnlyDictionary<string, PRegionProgressTypeRegistTable> regist,
        IReadOnlyDictionary<string, CSubRegionConfigReadTarget> readTargets,
        IReadOnlyDictionary<string, PRegionProgressTableMorgue> morgueProgress,
        IReadOnlyDictionary<string, PRegionProgressTableFour> fourProgress,
        IReadOnlyDictionary<string, PRegionProgressTableDayfair> dayfairProgress,
        IReadOnlyDictionary<string, PRegionSequenceTableMorgue> morgueSequences,
        IReadOnlyDictionary<string, PRegionSequenceTableFour> fourSequences,
        IReadOnlyDictionary<string, PRegionSequenceTableDayfair> dayfairSequences,
        IReadOnlyDictionary<string, PCollectionSubRegionMapping> collectionBlocks,
        DropAssets drops
    )
    {
        foreach (var row in collectionBlocks.Values)
        {
            _blockSubRegions[row.Id] = row.SubRegionList;
        }

        foreach (var row in rewards.Values)
        {
            _rewards[row.Id] = row;
        }

        foreach (var row in rewardData.Values)
        {
            _rewardData[row.Id] = row;
        }

        foreach (var row in regist.Values)
        {
            _registByType[row.RegistType] = row;
        }

        foreach (var row in subRegions.Values)
        {
            _subRegionNames[row.Id] = (uint)row.SubRegionName;
        }

        foreach (var row in regions.Values)
        {
            _regions[row.Id] = row;

            foreach (var subRegion in row.SubRegionId)
            {
                _regionOf[subRegion] = row.Id;
            }
        }

        if (_rewardData.Count == 0)
            throw new ResourceException("P_RegionRewardDataTable.json", "p_regionrewarddatatable has no rows");

        if (_regions.Count == 0)
            throw new ResourceException("P_RegionTable.json", "p_regiontable has no rows");

        if (_registByType.Count == 0)
            throw new ResourceException("P_RegionProgressTypeRegistTable.json", "p_regionprogresstyperegisttable has no rows");

        foreach (var target in readTargets.Values)
        {
            var progressRows = target.RegionProgressSheet switch {
                "P_RegionProgressTable_Morgue" => morgueProgress.Values.Select(row => (row.Id, Sequences: row.SequenceId)),
                "P_RegionProgressTable_Four" => fourProgress.Values.Select(row => (row.Id, row.SequenceId)),
                "P_RegionProgressTable_Dayfair" => dayfairProgress.Values.Select(row => (row.Id, row.SequenceId)),
                _ => []
            };

            foreach (var (id, sequenceIds) in progressRows)
            {
                _sequences[id] = [.. sequenceIds];
            }

            void Sheet<TSequence>(IReadOnlyDictionary<string, TSequence> rows, Func<TSequence, RegionSequence> map)
                where TSequence : TableRow
            {
                foreach (var row in rows.Values)
                {
                    var sequence = map(row);
                    _sequenceRows[(target.Id, sequence.Id)] = sequence;
                }
            }

            switch (target.RegionSequenceSheet)
            {
                case "P_RegionSequenceTable_Morgue":
                    Sheet(morgueSequences, row => new RegionSequence(row.Id, row.Type, row.ParamId, row.ParamNum));
                    break;
                case "P_RegionSequenceTable_Four":
                    Sheet(fourSequences, row => new RegionSequence(row.Id, row.Type, row.ParamId, row.ParamNum));
                    break;
                case "P_RegionSequenceTable_Dayfair":
                    Sheet(dayfairSequences, row => new RegionSequence(row.Id, row.Type, row.ParamId, row.ParamNum));
                    break;
            }
        }

        foreach (var row in progress.Values)
        {
            _sequences.TryAdd(row.Id, [.. row.SequenceId]);
        }

        foreach (var row in sequences.Values)
        {
            _sequenceRows.TryAdd((SubRegionOfName((uint)row.SubRegionName), row.Id),
                new RegionSequence(row.Id, row.Type, row.ParamId, row.ParamNum.FirstOrDefault()));
        }

        foreach (var subRegionId in _rewards.Keys)
        {
            if (_regions.Values.Any(region => region.SubRegionId.Contains(subRegionId)) &&
                _sequences.TryGetValue(subRegionId, out var sequenceIds))
                foreach (var sequence in sequenceIds)
                {
                    if (!_sequenceRows.ContainsKey((subRegionId, sequence)))
                        throw new ResourceException(
                            "RegionSequenceSheets", $"subregion {subRegionId} references missing sequence {sequence}");

                    if (!_registByType.ContainsKey(_sequenceRows[(subRegionId, sequence)].Type))
                        throw new ResourceException(
                            "P_RegionProgressTypeRegistTable.json",
                            $"subregion {subRegionId} sequence {sequence} has unregistered type {_sequenceRows[(subRegionId, sequence)].Type}");
                }

            foreach (var reward in _rewards[subRegionId].Reward)
            {
                if (!_rewardData.TryGetValue(reward, out var data))
                    throw new ResourceException(
                        "P_RegionRewardDataTable.json", $"subregion {subRegionId} references missing reward {reward}");

                if (data.DropId != 0 && !drops.Exists(data.DropId))
                    throw new ResourceException(
                        "P_FixedDropTable.json", $"region reward {reward} references missing drop {data.DropId}");
            }
        }
    }

    public IReadOnlyList<ulong> TrackedSubRegions => [.. _sequences.Keys.OrderBy(id => id)];

    public IReadOnlyList<uint> Regions => _regions.Keys.OrderBy(id => id).ToList();

    public bool SubRegionExists(ulong id) => _sequences.ContainsKey(id);
    public IReadOnlyList<uint> Sequences(ulong id) => _sequences.GetValueOrDefault(id) ?? [];

    public RegionSequence? SequenceRow(ulong subRegionId, uint sequenceId) =>
        _sequenceRows.GetValueOrDefault((subRegionId, sequenceId));

    public PRegionProgressTypeRegistTable? RegistOf(uint type) => _registByType.GetValueOrDefault(type);

    /// <summary>Chest and resource objectives (P_CollectionTable registries) count gathers, not distinct templates.</summary>
    public bool CountsGathers(RegionSequence sequence) => RegistOf(sequence.Type)?.RegistType is 2 or 7;

    /// <summary>Sub-regions whose objectives a gather on this collection block counts for.</summary>
    public IReadOnlyList<ulong> SubRegionsOfBlock(ulong block) =>
        SubRegionExists(block) ? [block] : _blockSubRegions.GetValueOrDefault(block) ?? [];

    public IReadOnlyList<PRegionRewardDataTable> Rewards(ulong id) =>
        (_rewards.GetValueOrDefault(id)?.Reward ?? [])
        .Select(reward => _rewardData.GetValueOrDefault(reward))
        .Where(row => row is not null)
        .Select(row => row!)
        .OrderBy(row => row.PrograssValue)
        .ToList();

    public uint RegionOf(ulong subRegionId) => _regionOf.GetValueOrDefault(subRegionId);

    public IReadOnlyList<ulong> SubRegions(uint regionId) =>
        _regions.GetValueOrDefault(regionId)?.SubRegionId ?? [];

    /// <summary>Default sheets use SubRegionName text IDs. Per-map sheets use subregion IDs.</summary>
    private ulong SubRegionOfName(uint subRegionName)
    {
        foreach (var region in _regions.Values)
        foreach (var subRegion in region.SubRegionId)
        {
            if (_subRegionNames.TryGetValue(subRegion, out var name) && name == subRegionName)
                return subRegion;
        }
        return 0;
    }
}
