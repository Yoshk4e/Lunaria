namespace Lunaria.Game.Player.Gameplay;

public readonly record struct MotiveAcquired(uint MotiveId, uint Count) : IGameplayEvent;
