using System.Collections.Frozen;
using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

public sealed class GuideAssets
{
    private readonly FrozenDictionary<uint, uint> _browseRewards;
    private readonly FrozenDictionary<ulong, List<uint>> _guidesForStep;
    private readonly FrozenSet<uint> _ids;

    public GuideAssets(IReadOnlyDictionary<string, PGraphicGuideTable> rows)
    {
        var ids = new HashSet<uint>();
        var browseRewards = new Dictionary<uint, uint>();
        var guidesForStep = new Dictionary<ulong, List<uint>>();

        foreach (var row in rows.Values)
        {
            ids.Add(row.Id);

            if (row.DropId != 0)
                browseRewards[row.Id] = row.DropId;

            if (row.StepId != 0)
            {
                if (!guidesForStep.TryGetValue(row.StepId, out var idsForStep))
                    guidesForStep[row.StepId] = idsForStep = [];
                idsForStep.Add(row.Id);
            }
        }

        if (ids.Count == 0)
            throw new ResourceException("P_GraphicGuideTable.json", "p_graphicguidetable has no rows");

        _ids = ids.ToFrozenSet();
        _browseRewards = browseRewards.ToFrozenDictionary();
        _guidesForStep = guidesForStep.ToFrozenDictionary();
    }

    public int Count => _ids.Count;

    public IReadOnlyList<uint> All => _ids.Order().ToList();

    public bool Exists(uint guideId) => _ids.Contains(guideId);

    public IReadOnlyList<uint> GuidesForStep(ulong stepId) =>
        _guidesForStep.TryGetValue(stepId, out var ids) ? ids : [];

    public uint BrowseRewardOf(uint guideId) => _browseRewards.GetValueOrDefault(guideId);
}
