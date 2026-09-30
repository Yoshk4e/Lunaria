namespace Lunaria.Game.Player.Gameplay;

public readonly record struct WantedCleared(uint EntryId) : IGameplayEvent;
