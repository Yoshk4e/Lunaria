namespace Lunaria.Game.Player.Gameplay;

public readonly record struct HousePurchased(uint HouseId) : IGameplayEvent;
