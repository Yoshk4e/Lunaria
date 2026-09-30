namespace Lunaria.Game.Characters;

public sealed record CharacterState
{
    public required ulong InstId { get; init; }
    public required uint CharacterId { get; init; }
    public uint Level { get; init; }
    public uint Exp { get; init; }
    public uint BreakLevel { get; init; }

    /// <summary>HP in table units. Null means full health, including in older saves.</summary>
    public int? Hp { get; init; }

    /// <summary>Permanent liquid in table units. Null means full.</summary>
    public int? PermanentLiquid { get; init; }

    public ulong MotiveUniqId { get; init; }
}
