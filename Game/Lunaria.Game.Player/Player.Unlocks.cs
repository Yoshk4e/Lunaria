namespace Lunaria.Game.Player;

public sealed partial class Player
{
    public bool IsSystemUnlocked(uint systemType) =>
        assets.Unlocks.IsUnlocked(systemType, Achievements.IsEventFinished);

    public IReadOnlyList<uint> UnlockedSystems() =>
        assets.Unlocks.UnlockedSystems(Achievements.IsEventFinished);
}
