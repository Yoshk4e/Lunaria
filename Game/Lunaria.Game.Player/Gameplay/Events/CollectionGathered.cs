namespace Lunaria.Game.Player.Gameplay;

public readonly record struct CollectionGathered(int CollectionType, uint Cfg, ulong Block) : IGameplayEvent;
