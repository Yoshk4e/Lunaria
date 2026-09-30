using System.Collections.Frozen;
using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

public sealed class ItemEffectAssets
{
    public const int Blood = 1;
    public const int PermanentLiquid = 2;
    public const int LiquidSilver = 3;
    private readonly FrozenDictionary<uint, POutsideBuffTable> _buffs;
    private readonly FrozenDictionary<uint, PItemEffectTypeTable> _effectTypes;
    private readonly FrozenDictionary<uint, PItemEffectTable> _effects;

    private readonly FrozenDictionary<uint, PItemTable> _items;

    public ItemEffectAssets(
        IReadOnlyDictionary<string, PItemTable> items,
        IReadOnlyDictionary<string, PItemEffectTable> effects,
        IReadOnlyDictionary<string, PItemEffectTypeTable> effectTypes,
        IReadOnlyDictionary<string, POutsideBuffTable> buffs
    )
    {
        var itemMap = new Dictionary<uint, PItemTable>();

        foreach (var row in items.Values)
        {
            itemMap[row.Id] = row;
        }

        _items = itemMap.ToFrozenDictionary();

        var effectMap = new Dictionary<uint, PItemEffectTable>();

        foreach (var row in effects.Values)
        {
            effectMap[row.Id] = row;
        }

        _effects = effectMap.ToFrozenDictionary();

        var effectTypeMap = new Dictionary<uint, PItemEffectTypeTable>();

        foreach (var row in effectTypes.Values)
        {
            effectTypeMap[row.Id] = row;
        }

        _effectTypes = effectTypeMap.ToFrozenDictionary();

        var buffMap = new Dictionary<uint, POutsideBuffTable>();

        foreach (var row in buffs.Values)
        {
            buffMap[row.Id] = row;
        }

        _buffs = buffMap.ToFrozenDictionary();

        if (_items.Count == 0)
            throw new ResourceException("P_ItemTable.json", "p_itemtable has no rows");

        if (_effects.Count == 0)
            throw new ResourceException("P_ItemEffectTable.json", "p_itemeffecttable has no rows");

        if (_effectTypes.Count == 0)
            throw new ResourceException("P_ItemEffectTypeTable.json", "p_itemeffecttypetable has no rows");

        if (_buffs.Count == 0)
            throw new ResourceException("P_OutsideBuffTable.json", "p_outsidebufftable has no rows");

        foreach (var item in _items.Values)
        {
            if ((ItemUseType)item.UseType != ItemUseType.AddBuffEffect || item.Param.Count == 0)
                continue;

            if (!_effects.TryGetValue(item.Param[0], out var effect))
                throw new ResourceException(
                    "P_ItemEffectTable.json",
                    $"p_itemtable {item.Id} references missing item effect {item.Param[0]}");

            if (!_effectTypes.TryGetValue(effect.TypeId, out var type))
                throw new ResourceException(
                    "P_ItemEffectTypeTable.json",
                    $"p_itemeffecttable {effect.Id} references missing effect type {effect.TypeId}");

            if (type.EffectType is not (Blood or PermanentLiquid or LiquidSilver))
                throw new ResourceException(
                    "P_ItemEffectTypeTable.json",
                    $"p_itemeffecttypetable {type.Id} has unknown effect type {type.EffectType}");

            if (effect.BuffList.Count == 0)
                throw new ResourceException(
                    "P_ItemEffectTable.json",
                    $"p_itemeffecttable {effect.Id} has no outside buffs");

            foreach (var buffId in effect.BuffList)
            {
                if (!_buffs.ContainsKey(buffId))
                    throw new ResourceException(
                        "P_OutsideBuffTable.json",
                        $"p_itemeffecttable {effect.Id} references missing outside buff {buffId}");
            }
        }
    }

    public ItemUseEffect? Effect(uint itemId)
    {
        if (!_items.TryGetValue(itemId, out var item)
            || (ItemUseType)item.UseType != ItemUseType.AddBuffEffect
            || item.Param.Count == 0
            || !_effects.TryGetValue(item.Param[0], out var effect)
            || !_effectTypes.TryGetValue(effect.TypeId, out var type))
            return null;

        var result = new ItemUseEffect(itemId, type.EffectType, effect.Satiety, type.Revive != 0);

        foreach (var buffId in effect.BuffList)
        {
            var buff = _buffs[buffId];
            result.Add(ImmediateEffect(buff));

            if (buff.TempAttribute1.Count > 0 || buff.TempAttribute2.Count > 0)
                result.AddTimed(buffId);
        }

        return result;
    }

    /// <summary>Cooldown type and duration in seconds, or null for no cooldown.</summary>
    public (uint TypeId, uint Seconds)? Cooldown(uint itemId) =>
        _items.TryGetValue(itemId, out var item)
        && (ItemUseType)item.UseType == ItemUseType.AddBuffEffect
        && item.Param.Count > 0
        && _effects.TryGetValue(item.Param[0], out var effect)
        && _effectTypes.TryGetValue(effect.TypeId, out var type)
        && type.Cd > 0 ?
            (effect.TypeId, (uint)type.Cd) :
            null;

    public POutsideBuffTable? Buff(uint buffId) => _buffs.GetValueOrDefault(buffId);

    private static ImmediateEffect ImmediateEffect(POutsideBuffTable buff)
    {
        var first = Attribute(buff.ImmediateAttribute1);
        var second = Attribute(buff.ImmediateAttribute2);
        var element = buff.TemporaryLiquid.Count > 0 ? buff.TemporaryLiquid[0] : 0;
        var amount = buff.TemporaryLiquid.Count > 1 ? buff.TemporaryLiquid[1] : 0;

        return new ImmediateEffect(
            buff.Id,
            buff.TargetType,
            first.AttributeId,
            first.Value,
            first.Ratio,
            second.AttributeId,
            second.Value,
            second.Ratio,
            element,
            amount);
    }

    private static (int AttributeId, int Value, int Ratio) Attribute(List<int> row) =>
        row.Count switch {
            >= 3 => (row[0], row[1], row[2]),
            2 => (row[0], row[1], 0),
            _ => (0, 0, 0)
        };
}

public sealed record ItemUseEffect(
    uint ItemId,
    int Kind,
    int Satiety,
    bool Revive
)
{
    private readonly List<ImmediateEffect> _effects = [];
    private readonly List<uint> _timedBuffs = [];

    public IReadOnlyList<ImmediateEffect> Effects => _effects;

    public IReadOnlyList<uint> TimedBuffs => _timedBuffs;

    internal void Add(ImmediateEffect effect) => _effects.Add(effect);
    internal void AddTimed(uint buffId) => _timedBuffs.Add(buffId);
}

/// <summary>Resolve attribute IDs through InsideAttributeAssets. Zero means absent.</summary>
public sealed record ImmediateEffect(
    uint BuffId,
    int TargetType,
    int FirstAttributeId,
    int FirstValue,
    int FirstRatio,
    int SecondAttributeId,
    int SecondValue,
    int SecondRatio,
    int TemporaryLiquidElement,
    int TemporaryLiquidAmount
);
