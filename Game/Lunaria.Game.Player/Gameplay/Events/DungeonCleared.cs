namespace Lunaria.Game.Player.Gameplay;

public readonly record struct DungeonCleared(ulong DungeonId) : IGameplayEvent;
