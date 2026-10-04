using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

/// <summary>
/// The client merges fixed attributes in OutsideAttributeData.lua. Sending them here would double their values.
/// </summary>
public sealed class AttribAssets
{
    private readonly Dictionary<uint, PDevelopAttributeTable> _develop = [];
    private readonly Dictionary<uint, AttributeModifier[]> _developStats = [];
    private readonly Dictionary<uint, PFixedAttributeTable> _fixed = [];
    private readonly InsideAttributeAssets _inside;
    private readonly (int Id, int Value)[] _outsideDefaults;

    public AttribAssets(
        IReadOnlyDictionary<string, PDevelopAttributeTable> develop,
        IReadOnlyDictionary<string, PFixedAttributeTable> fixedTable,
        IReadOnlyDictionary<string, POutsideAttributeTable> outside,
        InsideAttributeAssets inside,
        AttributeColumns columns
    )
    {
        foreach (var row in develop.Values)
        {
            _develop[row.Id] = row;
            _developStats[row.Id] = columns.Of(row);
        }

        foreach (var row in fixedTable.Values)
        {
            _fixed[row.Id] = row;
        }

        _outsideDefaults = outside.Values
            .Where(r => r.Default != 0)
            .OrderBy(r => r.Id)
            .Select(r => (r.AttrEnum, r.Default))
            .ToArray();

        _inside = inside;
    }

    public IReadOnlyList<(int Id, int Value)> OutsideDefaults => _outsideDefaults;

    private PDevelopAttributeTable? DevelopRow(uint characterId, uint developAttributeId) =>
        _develop.GetValueOrDefault(developAttributeId)
        ?? _develop.GetValueOrDefault(characterId * 100u + 1u)
        ?? _develop.GetValueOrDefault(characterId * 100);

    /// <summary>Multiply flat stats by 10000 for the wire. Permyriad rates are already scaled.</summary>
    private int Scale(int attrId, int value) => _inside.Scale(attrId, value);

    /// <summary>MAXHP in table units: the level row plus the cumulative row of the current break level.</summary>
    public int MaxHp(uint characterId, uint developAttributeId, uint breakDevelopAttributeId = 0) =>
        DevelopRow(characterId, developAttributeId) is {} row ?
            Stats(row, breakDevelopAttributeId).GetValueOrDefault(_inside.Attr.Maxhp) / Scale(_inside.Attr.Maxhp, 1) :
            0;

    public int PermanentLiquidMax(uint characterId) =>
        (int)((_fixed.GetValueOrDefault(characterId) ?? _fixed.GetValueOrDefault(1001u))?.PermanentLiquidMax ?? 100);

    /// <summary>
    /// Base attributes on the wire, as the client's GetAttributesWhenLevelUp builds them: every attribute column of
    /// the level row plus the break row of the current break level (cumulative, one row only).
    /// </summary>
    public IReadOnlyList<(int Id, int Value)> ForCharacter(
        uint characterId,
        uint developAttributeId,
        int? currentHp = null,
        int? currentPermanentLiquid = null,
        uint? fixedAttributeId = null,
        int? maxHpCeiling = null,
        uint breakDevelopAttributeId = 0
    )
    {
        var output = new List<(int Id, int Value)>(16 + _outsideDefaults.Length);
        var attr = _inside.Attr;

        if (DevelopRow(characterId, developAttributeId) is {} row)
        {
            var stats = Stats(row, breakDevelopAttributeId);

            // Send current HP explicitly. The default of 0 makes the client spawn the character dead.
            // maxHpCeiling is MAXHP with the Motive and talent bonuses, which current HP may fill.
            var maxHp = maxHpCeiling ?? stats.GetValueOrDefault(attr.Maxhp) / Scale(attr.Maxhp, 1);
            var permanentLiquidMax = PermanentLiquidMax(fixedAttributeId ?? characterId);
            var hp = Math.Clamp(currentHp ?? maxHp, min: 0, maxHp);
            var permanentLiquid = Math.Clamp(currentPermanentLiquid ?? permanentLiquidMax, min: 0, permanentLiquidMax);

            output.Add((attr.Maxhp, stats.GetValueOrDefault(attr.Maxhp)));
            output.Add((attr.Hp, Scale(attr.Hp, hp)));
            output.Add((attr.Atk, stats.GetValueOrDefault(attr.Atk)));
            output.Add((attr.Def, stats.GetValueOrDefault(attr.Def)));
            output.Add((attr.AtkCriticalChance, stats.GetValueOrDefault(attr.AtkCriticalChance)));
            output.Add((attr.AtkCriticalDamage, stats.GetValueOrDefault(attr.AtkCriticalDamage)));
            output.Add((attr.AtkBodyPartBreakAddRate, stats.GetValueOrDefault(attr.AtkBodyPartBreakAddRate)));
            output.Add((attr.Shield, 0));
            output.Add((attr.PermanentLiquid, Scale(attr.PermanentLiquid, permanentLiquid)));
            output.Add((attr.PermanentLiquidMax, Scale(attr.PermanentLiquidMax, permanentLiquidMax)));

            // Liquid_AbsorbRate and the element damage rates only come from these rows.
            var sent = output.Select(pair => pair.Id).ToHashSet();
            output.AddRange(stats.Where(pair => !sent.Contains(pair.Key)).OrderBy(pair => pair.Key)
                .Select(pair => (pair.Key, pair.Value)));
        }

        output.AddRange(_outsideDefaults.Select(d => (d.Id, Scale(d.Id, d.Value))));
        return output;
    }

    /// <summary>Wire values of the summed rows: floor(add * (1 + rate)), the client's base attribute formula.</summary>
    private Dictionary<int, int> Stats(PDevelopAttributeTable row, uint breakDevelopAttributeId)
    {
        var modifiers = _developStats[row.Id].AsEnumerable();

        if (breakDevelopAttributeId != 0 && _developStats.TryGetValue(breakDevelopAttributeId, out var broken))
            modifiers = modifiers.Concat(broken);

        return modifiers.GroupBy(m => m.AttrId).ToDictionary(group => group.Key, group => {
            var add = (long)Scale(group.Key, 1) * group.Sum(m => (long)m.Add);
            var raw = add * (10_000 + group.Sum(m => (long)m.Multi)) / 10_000;
            return (int)Math.Clamp(raw, int.MinValue, int.MaxValue);
        });
    }
}
