namespace Lunaria.Game.Player.Rewards;

/// <summary>Completed state mutations awaiting their progression and client notifications.</summary>
internal sealed record RewardMutation(
    RewardDelivery Delivery,
    IReadOnlyDictionary<uint, uint> ItemsAcquired,
    IReadOnlyDictionary<uint, uint> MotivesAcquired);
