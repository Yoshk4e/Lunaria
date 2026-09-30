namespace Lunaria.Game.Player.Gameplay;

public readonly record struct StaminaSpent(uint Amount) : IGameplayEvent;
