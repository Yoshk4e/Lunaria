namespace Lunaria.Game.Player.Gameplay;

/// <summary>Runs on the player's event loop without I/O.</summary>
public interface IGameplayHook<in TEvent> where TEvent : IGameplayEvent
{
    void Handle(Player player, TEvent occurrence, PlayerChanges changes);
}
