namespace Lunaria.Game.Resources;

/// <summary>Index selects a claimed_rewards_mask bit, ordered by increasing draw count.</summary>
public sealed record RebateMilestone(int Index, uint DrawCount, ItemGrant Reward)
{
    public uint Bit => 1u << Index;
}
