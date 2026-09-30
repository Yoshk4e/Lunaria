namespace Lunaria.Game.Player.Gameplay;

public sealed record GuidesChanged(IReadOnlyList<uint> Ids) : IGameplayEvent;
