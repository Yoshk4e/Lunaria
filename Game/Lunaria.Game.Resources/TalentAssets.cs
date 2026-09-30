using System.Collections.Frozen;
using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

public sealed class TalentAssets
{
    public const uint MaxNodeId = 127;

    private readonly FrozenDictionary<uint, PTalentNodeTable> _nodes;

    public TalentAssets(IReadOnlyDictionary<string, PTalentNodeTable> rows)
    {
        var nodes = new Dictionary<uint, PTalentNodeTable>();

        foreach (var row in rows.Values)
        {
            if (row.Id > MaxNodeId)
                throw new ResourceException("P_TalentNodeTable.json",
                    $"p_talentnodetable node {row.Id} exceeds the {MaxNodeId}-bit unlock mask");

            nodes[row.Id] = row;
        }

        if (nodes.Count == 0)
            throw new ResourceException("P_TalentNodeTable.json", "p_talentnodetable has no rows");

        _nodes = nodes.ToFrozenDictionary();
    }

    public int Count => _nodes.Count;

    public IReadOnlyList<uint> All => _nodes.Keys.Order().ToList();

    public bool NodeExists(uint nodeId) => _nodes.ContainsKey(nodeId);

    public PTalentNodeTable? Node(uint nodeId) => _nodes.GetValueOrDefault(nodeId);

    public IReadOnlyList<uint> Prerequisites(uint nodeId) => _nodes.GetValueOrDefault(nodeId)?.ParentIdList ?? [];

    public uint UnlockLevel(uint nodeId) => Math.Max(_nodes.GetValueOrDefault(nodeId)?.UnlockLevel ?? 1, val2: 1);
}
