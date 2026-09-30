namespace Lunaria.Game.Player.Gameplay;

public readonly record struct BattleCompleted(uint BattleId) : IGameplayEvent;
