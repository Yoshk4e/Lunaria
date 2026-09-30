namespace Lunaria.Game.Motives;

public sealed record MotiveState
{
    public required ulong UniqId { get; init; }
    public required uint MotiveId { get; init; }

    public uint ItemId { get; init; }

    /// <summary>Claim time in Unix seconds.</summary>
    public ulong ClaimTime { get; init; }

    public uint Level { get; init; } = 1;
    public uint Exp { get; init; }
    public uint RefineLevel { get; init; }
    public uint BreakLevel { get; init; }

    public bool Locked { get; init; }

    /// <summary>Equipped character instance ID, or 0 when unequipped.</summary>
    public ulong EquipedTarget { get; init; }
}
