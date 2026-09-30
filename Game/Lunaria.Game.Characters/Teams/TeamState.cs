namespace Lunaria.Game.Characters;

public sealed record TeamState
{
    public required uint TeamId { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<TeamMemberState> Members { get; init; }
    public required TeamLiquid TemporaryLiquid { get; init; }
    public required TeamLiquid TemporaryLiquidLv2 { get; init; }

    public TeamMemberState? Leader() => Members.Count > 0 ? Members[0] : null;

    public bool Has(ulong instId) => Members.Any(m => m.InstId == instId);
}
