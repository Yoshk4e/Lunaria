namespace Lunaria.Game.Player.Gameplay;

public sealed record LevelDataChanged(Msg.PlayerLevelData Before, Msg.PlayerLevelData After) : IGameplayEvent;
