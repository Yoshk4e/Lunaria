using System.Numerics;

namespace Lunaria.Game.Characters;

/// <summary>The two masks hold nodes 0-127. Check the ID before calling WithUnlock.</summary>
public sealed record TalentMasks
{
    public ulong Mask0 { get; init; }
    public ulong Mask1 { get; init; }

    public int Count => BitOperations.PopCount(Mask0) + BitOperations.PopCount(Mask1);

    public bool IsUnlocked(uint nodeId) => nodeId switch {
        <= 63 => (Mask0 & 1UL << (int)nodeId) != 0,
        <= 127 => (Mask1 & 1UL << (int)(nodeId - 64)) != 0,
        _ => false
    };

    public TalentMasks WithUnlock(uint nodeId) => nodeId switch {
        <= 63 => this with { Mask0 = Mask0 | 1UL << (int)nodeId },
        <= 127 => this with { Mask1 = Mask1 | 1UL << (int)(nodeId - 64) },
        _ => this
    };
}
