using Lunaria.Common.Tracking;

namespace Lunaria.Game.Inventory;

public sealed partial class ItemStack : TrackedObject
{
    public required uint ItemId { get; init; }
    private uint __trackedCount;
    [Tracked] public required partial uint Count { get; set; }
    private bool __trackedIsNew;
    [Tracked] public partial bool IsNew { get; set; }
}
