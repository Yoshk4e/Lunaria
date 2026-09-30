namespace Lunaria.Game.Player.Gameplay;

internal sealed class TemporaryTeamHooks : IGameplayHook<TaskProgressed>, IGameplayHook<RoleLoggedIn>
{
    public void Handle(Player player, TaskProgressed change, PlayerChanges changes)
    {
        if (change.Progress.Recorded) player.ReconcileTemporaryTeam();
    }

    public void Handle(Player player, RoleLoggedIn change, PlayerChanges changes) =>
        player.ReconcileTemporaryTeam(notify: false);
}
