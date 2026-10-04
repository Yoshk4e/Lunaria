using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

public sealed class MotiveAssets
{
    private readonly Dictionary<uint, PMotiveAttributeTable> _attributes = [];

    private readonly Dictionary<uint, BreakStep[]> _breaks = [];

    private readonly Dictionary<(uint Template, uint Level), uint> _levelAttributes = [];
    private readonly Dictionary<(uint Template, uint BreakLevel), uint> _breakAttributes = [];

    private readonly Dictionary<(uint Rare, uint Level), uint> _levelCosts = [];
    private readonly Dictionary<uint, PMotiveTable> _motives = [];

    private readonly Dictionary<uint, uint> _rarityCeilings = [];

    public MotiveAssets(
        IReadOnlyDictionary<string, PMotiveTable> motives,
        IReadOnlyDictionary<string, PMotiveLevelCostTable> levelCosts,
        IReadOnlyDictionary<string, PMotiveLevelTemplateTable> levelTemplates,
        IReadOnlyDictionary<string, PMotiveBreakTemplateTable> breaks,
        IReadOnlyDictionary<string, PMotiveAttributeTable> attributes
    )
    {
        foreach (var row in motives.Values)
        {
            _motives[row.Id] = row;
        }

        foreach (var row in attributes.Values)
        {
            _attributes[row.Id] = row;
        }

        foreach (var row in levelCosts.Values)
        {
            _levelCosts[(row.Rare, row.Level)] = row.MaxExp;

            if (row.Level > _rarityCeilings.GetValueOrDefault(row.Rare))
                _rarityCeilings[row.Rare] = row.Level;
        }

        foreach (var row in levelTemplates.Values)
        {
            _levelAttributes[(row.TemplateId, row.Level)] = row.AddAttributeId;
        }

        foreach (var row in breaks.Values.Where(r => r.AddAttributeId != 0))
        {
            _breakAttributes[(row.TemplateId, row.BreakLevel)] = row.AddAttributeId;
        }

        foreach (var group in breaks.Values.GroupBy(r => r.TemplateId))
        {
            _breaks[group.Key] = group
                .OrderBy(r => r.BreakLevel)
                .Select(r => new BreakStep(
                    r.BreakLevel,
                    r.MaxLevel,
                    r.NeedWorldLevel,
                    Pair(r.CostItemId, r.CostItemCount),
                    r.CostCurrency))
                .ToArray();
        }

        if (_motives.Count == 0)
            throw new ResourceException("P_MotiveTable.json", "p_motivetable has no rows");

        if (_breaks.Count == 0)
            throw new ResourceException("P_MotiveBreakTemplateTable.json", "p_motivebreaktemplatetable has no rows");
    }

    public int Count => _motives.Count;

    public bool Exists(uint motiveId) => _motives.ContainsKey(motiveId);

    public uint Rarity(uint motiveId) => _motives.GetValueOrDefault(motiveId)?.Rare ?? 0;

    public uint Identity(uint motiveId) => _motives.GetValueOrDefault(motiveId)?.Identity ?? 0;

    public uint? ExpToAdvance(uint motiveId, uint level) =>
        _motives.GetValueOrDefault(motiveId) is {} motive
        && _levelCosts.TryGetValue((motive.Rare, level), out var cost)
        && cost > 0 ?
            cost :
            null;

    public uint LevelCap(uint motiveId, uint breakLevel)
    {
        var ladder = Ladder(motiveId);

        if (ladder.Length == 0)
            return 1;

        foreach (var step in ladder)
        {
            if (step.BreakLevel == breakLevel)
                return step.MaxLevel;
        }

        return breakLevel > ladder[^1].BreakLevel ? ladder[^1].MaxLevel : ladder[0].MaxLevel;
    }

    public uint MaxLevel(uint motiveId)
    {
        var ladder = Ladder(motiveId);
        var breakCap = ladder.Length == 0 ? 1 : ladder.Max(step => step.MaxLevel);
        var priced = _rarityCeilings.GetValueOrDefault(Rarity(motiveId));
        return priced == 0 ? breakCap : Math.Min(breakCap, priced);
    }

    public BreakStep? NextBreak(uint motiveId, uint breakLevel) => BreakStep.Next(Ladder(motiveId), breakLevel);

    public bool IsFullyBroken(uint motiveId, uint breakLevel)
    {
        var ladder = Ladder(motiveId);
        return ladder.Length == 0 || breakLevel >= ladder[^1].BreakLevel;
    }

    public uint AddAttributeId(uint motiveId, uint level) =>
        _motives.GetValueOrDefault(motiveId) is {} motive ? _levelAttributes.GetValueOrDefault((motive.LevelTemplateId, level)) : 0;

    public uint BreakAddAttributeId(uint motiveId, uint breakLevel) =>
        _motives.GetValueOrDefault(motiveId) is {} motive ? _breakAttributes.GetValueOrDefault((motive.BreakTemplateId, breakLevel)) : 0;

    public PMotiveAttributeTable? Attributes(uint addAttributeId) =>
        _attributes.GetValueOrDefault(addAttributeId);

    private BreakStep[] Ladder(uint motiveId) =>
        _motives.GetValueOrDefault(motiveId) is {} motive ? _breaks.GetValueOrDefault(motive.BreakTemplateId) ?? [] : [];

    private static IReadOnlyList<ItemGrant> Pair(List<uint> ids, List<uint> counts) =>
        ids.Take(Math.Min(ids.Count, counts.Count))
            .Select((id, index) => new ItemGrant(id, counts[index]))
            .Where(grant => grant.ItemId != 0 && grant.Count > 0)
            .ToList();
}
