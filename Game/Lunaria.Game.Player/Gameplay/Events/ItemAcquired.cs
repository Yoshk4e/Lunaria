namespace Lunaria.Game.Player.Gameplay;

public readonly record struct ItemAcquired(uint ItemId, uint Count) : IGameplayEvent;
