namespace Lunaria.Game.Characters;

public sealed record TeamMemberState
{
    /// <summary>1-based slot on the wire.</summary>
    public required uint Slot { get; init; }
    public required ulong InstId { get; init; }
    public required uint CharacterId { get; init; }

    /// <summary>Catalyst item per gem slot: index 0 is gem slot 1, and 0 means the slot is empty.</summary>
    public IReadOnlyList<uint> Gems { get; init; } = [];
}
