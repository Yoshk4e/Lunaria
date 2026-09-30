namespace Lunaria.Game.Player.Gameplay;

public readonly record struct CreatureAcquired(uint ItemId, uint Count) : IGameplayEvent;
