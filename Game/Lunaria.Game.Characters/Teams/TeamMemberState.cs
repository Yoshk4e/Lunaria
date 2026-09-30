namespace Lunaria.Game.Characters;

public sealed record TeamMemberState
{
    /// <summary>1-based slot on the wire.</summary>
    public required uint Slot { get; init; }
    public required ulong InstId { get; init; }
    public required uint CharacterId { get; init; }
}
