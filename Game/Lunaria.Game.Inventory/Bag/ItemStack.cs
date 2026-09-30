namespace Lunaria.Game.Inventory;

public sealed record ItemStack
{
    public required uint ItemId { get; init; }
    public required uint Count { get; set; }
    public bool IsNew { get; set; }
}
