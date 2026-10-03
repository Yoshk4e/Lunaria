using Lunaria.Common.Tracking;
using Lunaria.Game.Logging;
using Lunaria.Game.Resources;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.Game.Inventory;

public sealed partial class ItemBagManager(GameData assets) : TrackedObject
{
    private static readonly ILogger Log = GameLog.Create("Lunaria.Game.Inventory.Bag");

    private readonly TrackedSortedDictionary<uint, ItemStack> __tracked_stacks = [];
    [Tracked]
    private partial TrackedSortedDictionary<uint, ItemStack> _stacks { get; }

    /// <summary>Must match the MaxBagCell value sent in global_conf.</summary>
    public int CellCap => assets.GlobalConfig.MaxBagCell;

    public int CellsUsed => _stacks.Count;

    public bool IsFull => CellsUsed >= CellCap;

    public bool IsEmpty => _stacks.Count == 0;

    public IReadOnlyList<uint> GrantStarter()
    {
        if (_stacks.Count > 0)
            return [];

        var refused = new List<uint>();

        foreach (var grant in assets.Starter.Items)
        {
            if (!Add(grant.ItemId, grant.Count).AnyStored)
                refused.Add(grant.ItemId);
        }

        return refused;
    }

    public void Load(IEnumerable<ItemStack> persisted)
    {
        _stacks.Clear();

        foreach (var stack in persisted)
        {
            var limit = assets.Items.HoldLimit(stack.ItemId);

            if (limit == 0 || stack.Count == 0 || _stacks.Count >= CellCap)
                continue;

            _stacks[stack.ItemId] = new ItemStack {
                ItemId = stack.ItemId,
                Count = Math.Min(stack.Count, limit),
                IsNew = stack.IsNew
            };
        }

        _changed.Clear();
        AcceptLoadedState();
    }

    public uint CountOf(uint itemId) => _stacks.TryGetValue(itemId, out var stack) ? stack.Count : 0;

    public bool IsNew(uint itemId) => _stacks.TryGetValue(itemId, out var stack) && stack.IsNew;

    public IEnumerable<ItemStack> All() => _stacks.Values;

    public void ClearNewFlags(IEnumerable<uint> itemIds)
    {
        foreach (var id in itemIds)
        {
            if (_stacks.TryGetValue(id, out var stack) && stack.IsNew)
            {
                stack.IsNew = false;

            }
        }
    }

    public IReadOnlyList<CmdItem> ItemsData() =>
        _stacks.Values
            .Take((int)EnmSizeLimit.MaxItemLen)
            .Select(stack => new CmdItem {
                ItemId = stack.ItemId,
                ItemNum = stack.Count,
                IsNew = stack.IsNew,
                BindId = stack.ItemId,
                MotiveData = null
            })
            .ToList();

}
