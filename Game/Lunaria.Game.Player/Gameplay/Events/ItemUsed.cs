namespace Lunaria.Game.Player.Gameplay;

public readonly record struct ItemUsed(uint ItemId, uint Count) : IGameplayEvent;
