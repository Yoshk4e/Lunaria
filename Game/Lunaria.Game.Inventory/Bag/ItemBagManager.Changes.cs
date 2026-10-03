using Lunaria.Common.Tracking;
using Msg;

namespace Lunaria.Game.Inventory;

/// <summary>Inventory notifications stay queued after persistence accepts changes.</summary>
public sealed partial class ItemBagManager : TrackedObject
{
    [Untracked]
    private readonly HashSet<uint> _changed = [];

    private void MarkChanged(uint itemId) => _changed.Add(itemId);

    /// <summary>Emptied cells are sent with count 0 so the client removes them.</summary>
    public IReadOnlyList<CmdItem> ChangedItemsData() =>
        _changed
            .Select(itemId => {
                if (_stacks.TryGetValue(itemId, out var stack))
                {
                    return new CmdItem {
                        ItemId = stack.ItemId,
                        ItemNum = stack.Count,
                        IsNew = stack.IsNew,
                        BindId = stack.ItemId,
                        MotiveData = null
                    };
                }

                return new CmdItem {
                    ItemId = itemId,
                    ItemNum = 0,
                    IsNew = false,
                    BindId = itemId,
                    MotiveData = null
                };
            })
            .ToList();

    public IReadOnlyList<uint> DrainChanged()
    {
        if (_changed.Count == 0)
            return [];

        var ids = _changed.ToList();
        _changed.Clear();
        return ids;
    }

    public IReadOnlyList<uint> TakeChangedCells() => DrainChanged();
}
