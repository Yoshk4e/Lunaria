namespace Lunaria.Game.Player.Gameplay;

public sealed record CharactersChanged(IReadOnlyList<ulong> InstIds) : IGameplayEvent;
