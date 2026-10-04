using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

/// <summary>
/// The client merges fixed attributes in OutsideAttributeData.lua. Sending them here would double their values.
/// </summary>
public sealed class AttribAssets
{
    private readonly Dictionary<uint, PDevelopAttributeTable> _develop = [];
    private readonly Dictionary<uint, PFixedAttributeTable> _fixed = [];
    private readonly InsideAttributeAssets _inside;
    private readonly (int Id, int Value)[] _outsideDefaults;

    public AttribAssets(
        IReadOnlyDictionary<string, PDevelopAttributeTable> develop,
        IReadOnlyDictionary<string, PFixedAttributeTable> fixedTable,
        IReadOnlyDictionary<string, POutsideAttributeTable> outside,
        InsideAttributeAssets inside
    )
    {
        foreach (var row in develop.Values)
        {
            _develop[row.Id] = row;
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

    public int MaxHp(uint characterId, uint developAttributeId) =>
        DevelopRow(characterId, developAttributeId)?.Maxhp ?? 0;

    public int PermanentLiquidMax(uint characterId) =>
        (int)((_fixed.GetValueOrDefault(characterId) ?? _fixed.GetValueOrDefault(1001u))?.PermanentLiquidMax ?? 100);

    public IReadOnlyList<(int Id, int Value)> ForCharacter(
        uint characterId,
        uint developAttributeId,
        int? currentHp = null,
        int? currentPermanentLiquid = null,
        uint? fixedAttributeId = null,
        int? maxHpCeiling = null
    )
    {
        var output = new List<(int, int)>(10 + _outsideDefaults.Length);
        var attr = _inside.Attr;

        if (DevelopRow(characterId, developAttributeId) is {} row)
        {
            // Send current HP explicitly. The default of 0 makes the client spawn the character dead.
            // maxHpCeiling is MAXHP with the Motive and talent bonuses, which current HP may fill.
            var maxHp = maxHpCeiling ?? row.Maxhp;
            var permanentLiquidMax = PermanentLiquidMax(fixedAttributeId ?? characterId);
            var hp = Math.Clamp(currentHp ?? maxHp, min: 0, maxHp);
            var permanentLiquid = Math.Clamp(currentPermanentLiquid ?? permanentLiquidMax, min: 0, permanentLiquidMax);

            output.Add((attr.Maxhp, Scale(attr.Maxhp, row.Maxhp)));
            output.Add((attr.Hp, Scale(attr.Hp, hp)));
            output.Add((attr.Atk, Scale(attr.Atk, row.Atk)));
            output.Add((attr.Def, Scale(attr.Def, row.Def)));
            output.Add((attr.AtkCriticalChance, Scale(attr.AtkCriticalChance, row.AtkCriticalChance)));
            output.Add((attr.AtkCriticalDamage, Scale(attr.AtkCriticalDamage, row.AtkCriticalDamage)));
            output.Add((attr.AtkBodyPartBreakAddRate, Scale(attr.AtkBodyPartBreakAddRate, row.AtkBodyPartBreakAddRate)));
            output.Add((attr.Shield, 0));
            output.Add((attr.PermanentLiquid, Scale(attr.PermanentLiquid, permanentLiquid)));
            output.Add((attr.PermanentLiquidMax, Scale(attr.PermanentLiquidMax, permanentLiquidMax)));
        }

        output.AddRange(_outsideDefaults.Select(d => (d.Id, Scale(d.Id, d.Value))));
        return output;
    }
}
