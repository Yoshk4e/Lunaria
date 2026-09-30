using System.Collections.Frozen;
using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

/// <summary>For AddCoin items, Param[0] is the credited money type.</summary>
public sealed class ItemAssets
{
    public const int CharacterCardItemType = 4;
    public const int MotiveItemType = 5;

    private const int ExpItemShowType = 4;

    private const uint FirstLevelUpItemRow = 2;
    private const uint LastLevelUpItemRow = 4;
    private readonly FrozenDictionary<int, uint> _currencyItemFor;
    private readonly FrozenDictionary<uint, int> _currencyItems;
    private readonly FrozenDictionary<uint, int> _itemTypes;

    private readonly FrozenDictionary<uint, PItemTable> _items;
    private readonly FrozenSet<uint> _levelUpExpItems;
    private readonly FrozenSet<int> _moneyTypes;

    public ItemAssets(
        IReadOnlyDictionary<string, PItemTable> items,
        IReadOnlyDictionary<string, PItemTypeTable> itemTypes,
        IReadOnlyDictionary<string, PMoneyTable> money,
        IReadOnlyDictionary<string, PCharacterConstTable> characterConst
    )
    {
        var itemMap = new Dictionary<uint, PItemTable>();

        foreach (var row in items.Values)
        {
            itemMap[row.Id] = row;
        }

        _items = itemMap.ToFrozenDictionary();

        var typeMap = new Dictionary<uint, int>();

        foreach (var row in itemTypes.Values)
        {
            typeMap[row.Id] = row.ItemType;
        }

        _itemTypes = typeMap.ToFrozenDictionary();

        var moneyTypes = new HashSet<int>();

        foreach (var row in money.Values)
        {
            moneyTypes.Add(row.MoneyType);
        }

        _moneyTypes = moneyTypes.ToFrozenSet();

        if (_items.Count == 0)
            throw new ResourceException("P_ItemTable.json", "p_itemtable has no rows");

        if (_moneyTypes.Count == 0)
            throw new ResourceException("P_MoneyTable.json", "p_moneytable has no rows");

        // Motive XP stones share Show_Type with character XP items. Use the client's item whitelist to tell them
        // apart.
        var levelUpExpItems = new HashSet<uint>();

        foreach (var row in characterConst.Values)
        {
            if (row.Id is >= FirstLevelUpItemRow and <= LastLevelUpItemRow)
                levelUpExpItems.Add(row.Value);
        }

        _levelUpExpItems = levelUpExpItems.ToFrozenSet();

        var currencies = new Dictionary<uint, int>();
        var currencyFor = new Dictionary<int, uint>();

        foreach (var (itemId, row) in itemMap)
        {
            if ((ItemUseType)row.UseType != ItemUseType.AddCoin || row.Param.Count == 0)
                continue;

            var moneyType = (int)row.Param[0];

            if (_moneyTypes.Contains(moneyType))
            {
                currencies[itemId] = moneyType;

                if (!currencyFor.TryGetValue(moneyType, out var first) || itemId < first)
                    currencyFor[moneyType] = itemId;
            }
        }

        _currencyItems = currencies.ToFrozenDictionary();
        _currencyItemFor = currencyFor.ToFrozenDictionary();
    }

    public IReadOnlySet<int> MoneyTypes => _moneyTypes;

    public bool Exists(uint itemId) => _items.ContainsKey(itemId);

    public PItemTable? Get(uint itemId) => _items.GetValueOrDefault(itemId);

    public int? ItemTypeOf(uint itemId) =>
        _items.TryGetValue(itemId, out var item)
        && _itemTypes.TryGetValue((uint)item.ShowType, out var itemType) ?
            itemType :
            null;

    public uint HoldLimit(uint itemId) => _items.GetValueOrDefault(itemId)?.HoldLimit ?? 0;

    public int? MoneyTypeOf(uint itemId) =>
        _currencyItems.TryGetValue(itemId, out var moneyType) ? moneyType : null;

    public int? StaminaOf(uint itemId)
    {
        var item = _items.GetValueOrDefault(itemId);
        return item is { Param.Count: > 0 } && (ItemUseType)item.UseType == ItemUseType.AddStamina ? (int)item.Param[0] : null;
    }

    public bool IsCurrency(uint itemId) => _currencyItems.ContainsKey(itemId);

    public uint? ExpOf(uint itemId) =>
        _items.GetValueOrDefault(itemId) is { ShowType: ExpItemShowType, Param.Count: > 0 } item
        && _levelUpExpItems.Contains(itemId) ?
            item.Param[0] :
            null;

    public uint? BattlePassOf(uint itemId)
    {
        var item = _items.GetValueOrDefault(itemId);
        return item is { Param.Count: > 0 } && (ItemUseType)item.UseType == ItemUseType.AddBpExp ? item.Param[0] : null;
    }

    public uint? TeamExpOf(uint itemId)
    {
        var item = _items.GetValueOrDefault(itemId);
        return item is { Param.Count: > 0 } && (ItemUseType)item.UseType == ItemUseType.AddTeamExp ? item.Param[0] : null;
    }

    public uint? CurrencyItemFor(int moneyType) =>
        _currencyItemFor.TryGetValue(moneyType, out var itemId) ? itemId : null;

    public bool IsMoneyType(int moneyType) => _moneyTypes.Contains(moneyType);
}
