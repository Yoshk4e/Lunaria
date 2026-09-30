using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

public sealed class CharacterAssets
{
    private readonly Dictionary<uint, BreakStep[]> _breaks = [];
    private readonly Dictionary<uint, PCharacterTable> _characters = [];

    private readonly Dictionary<(uint Character, uint Level), uint> _developIds = [];

    private readonly Dictionary<uint, uint> _levelCeilings = [];

    private readonly BreakStep[] _sharedBreaks;
    private readonly Dictionary<uint, PCharacterSkillGroupTable> _skillGroups = [];
    private readonly Dictionary<uint, PSkillGrowthTable> _skillGrowth = [];

    public CharacterAssets(
        IReadOnlyDictionary<string, PCharacterTable> characters,
        IReadOnlyDictionary<string, PCharacterSkillGroupTable> skillGroups,
        IReadOnlyDictionary<string, PSkillGrowthTable> skillGrowth,
        IReadOnlyDictionary<string, PBreakTemplateTable> breaks,
        IReadOnlyDictionary<string, PLevelUpTemplateTable> levelUps
    )
    {
        foreach (var row in characters.Values)
        {
            _characters[row.Id] = row;
        }

        foreach (var row in skillGroups.Values)
        {
            _skillGroups[row.Id] = row;
        }

        foreach (var row in skillGrowth.Values)
        {
            _skillGrowth[row.Id] = row;
        }

        foreach (var group in breaks.Values.GroupBy(r => r.TemplateId))
        {
            _breaks[group.Key] = group.OrderBy(r => r.BreakLevel).Select(ToBreakStep).ToArray();
        }

        if (_breaks.Count == 0)
            throw new ResourceException("P_BreakTemplateTable.json", "p_breaktemplatetable has no rows");

        _sharedBreaks = _breaks[_breaks.Keys.Min()];

        foreach (var row in levelUps.Values)
        {
            _developIds[(row.TemplateId, row.Level)] = row.DevelopAttributeId;

            if (row.Level > _levelCeilings.GetValueOrDefault(row.TemplateId))
                _levelCeilings[row.TemplateId] = row.Level;
        }

        if (_developIds.Count == 0)
            throw new ResourceException("P_LevelUpTemplateTable.json", "p_leveluptemplatetable has no rows");
    }

    public bool Exists(uint characterId) => _characters.ContainsKey(characterId);

    public CharacterRow? Get(uint characterId)
    {
        if (!_characters.TryGetValue(characterId, out var row) ||
            !_skillGroups.TryGetValue(characterId, out var groups))
            return null;

        if (groups.SkillGroup is not [{} a, {} b, {} c, {} d, {} e])
            return null;

        return new CharacterRow(
            row.Id,
            (uint)row.IdentityType,
            (uint)row.ElementType,
            (uint)row.RareType,
            [a, b, c, d, e]);
    }

    public IReadOnlyList<(uint Group, uint InitLevel)> StartingSkillGroups(uint characterId) =>
        Get(characterId)?.SkillGroups
            .Select(group => (group, _skillGrowth.GetValueOrDefault(group)?.InitLevel ?? 0))
            .ToList() ?? [];

    public uint LevelCap(uint characterId, uint breakLevel)
    {
        var ladder = Ladder(characterId);

        foreach (var step in ladder)
        {
            if (step.BreakLevel == breakLevel)
                return step.MaxLevel;
        }

        return breakLevel > ladder[^1].BreakLevel ? ladder[^1].MaxLevel : ladder[0].MaxLevel;
    }

    public uint MaxLevel(uint characterId)
    {
        var breakCap = Ladder(characterId).Max(step => step.MaxLevel);
        var priced = _levelCeilings.GetValueOrDefault(LevelTemplate(characterId));
        return priced == 0 ? breakCap : Math.Min(breakCap, priced);
    }

    public BreakStep? NextBreak(uint characterId, uint breakLevel) =>
        Ladder(characterId).FirstOrDefault(step => step.BreakLevel == breakLevel + 1);

    public bool IsFullyBroken(uint characterId, uint breakLevel) =>
        breakLevel >= Ladder(characterId)[^1].BreakLevel;

    public uint DevelopAttributeId(uint characterId, uint level)
    {
        var template = LevelTemplate(characterId);
        return _developIds.TryGetValue((template, level), out var id) ? id : template * 100u + Math.Min(level, 99u);
    }

    public uint FixedAttributeId(uint characterId) => _characters.GetValueOrDefault(characterId)?.FixedAttributeId is > 0 and var id ? id : characterId;

    private uint LevelTemplate(uint characterId) =>
        _characters.GetValueOrDefault(characterId)?.LevelUpTemplateId is > 0 and var id ? id : characterId;

    private BreakStep[] Ladder(uint characterId) =>
        _breaks.GetValueOrDefault(_characters.GetValueOrDefault(characterId)?.BreakUpTemplateId is > 0 and var id ? id : characterId) ?? _sharedBreaks;

    private static BreakStep ToBreakStep(PBreakTemplateTable row) => new(
        row.BreakLevel,
        row.MaxLevel,
        row.NeedWorldLevel,
        Pair(row.CostItemId, row.CostItemCount),
        row.CostCurrency);

    private static IReadOnlyList<ItemGrant> Pair(List<uint> ids, List<uint> counts) =>
        ids.Take(Math.Min(ids.Count, counts.Count))
            .Select((id, index) => new ItemGrant(id, counts[index]))
            .Where(grant => grant.ItemId != 0 && grant.Count > 0)
            .ToList();
}
