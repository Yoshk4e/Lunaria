using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    public int InventoryCapacity => Bag.CellCap;
    public uint InventoryCount(uint itemId) => Bag.CountOf(itemId);
    public IReadOnlyList<CSMotiveElem> MotiveData() => Motives.ListData();
    public IReadOnlyList<(uint CdType, long ReadyUnix)> InventoryCooldowns() => Cooldowns.Active();

    public ulong OwnedItemCount(uint itemId)
    {
        if (assets.Items.MoneyTypeOf(itemId) is {} currency) return (ulong)Math.Max(0, Wallet.Balance(currency));
        return Bag.CountOf(itemId) + (ulong)Motives.All.Count(m => m.ItemId == itemId)
            + (ulong)SilverCreatures.Creatures.Values.Count(c => c.ItemId == itemId);
    }

    public IReadOnlyList<CmdItem> InventoryItems() =>
        Bag.ItemsData().Concat(Motives.All.Select(m => Motives.ToInventoryItem(m))).ToArray();

    internal IReadOnlyList<CmdItem> DrainInventoryChanges()
    {
        var items = Bag.ChangedItemsData().Concat(Motives.DrainInventoryChanges()).ToArray();
        Bag.DrainChanged();
        return items;
    }
}
