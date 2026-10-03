using Lunaria.Common.Tracking;
using Lunaria.Game.Player.Gameplay;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    [Untracked]
    private PlayerLevelData? _syncedLevelData;

    private void EnsureLevelBaseline() => _syncedLevelData ??= LevelData();

    /// <summary>Capture each level change, including quest unlocks that grant no XP.</summary>
    private void SynchronizeLevelData()
    {
        var after = LevelData();
        var before = _syncedLevelData ?? after;
        _syncedLevelData = after;
        if (before.Equals(after)) return;
        Gameplay.Publish(new LevelDataChanged(before, after));
        if (before.TeamLevel != after.TeamLevel || before.WorldLevelMax != after.WorldLevelMax)
            Gameplay.Publish(new TeamLevelChanged());
    }

    public (int Result, uint WorldLevel) SelectWorldLevel(uint worldLevel)
    {
        using var operationTime = BeginOperation();
        EnsureLevelBaseline();
        var result = Progress.SelectWorldLevel(worldLevel);
        if (result.Result == 0) SynchronizeLevelData();
        return result;
    }

    public PlayerLevelData LevelData() => new() {
        WorldLevelMax = Progress.EarnedWorldLevel,
        WorldLevelCur = Progress.WorldLevel,
        TeamLevel = Progress.TeamLevel,
        TeamExp = Progress.TeamExp
    };

    public uint TeamExpFor(EnmItemReason reason) =>
        assets.TeamExpAwards.TeamExpFor((uint)reason);

    public (PlayerLevelData Before, PlayerLevelData After) GrantTeamExp(ulong exp)
    {
        using var operationTime = BeginOperation();
        EnsureLevelBaseline();
        var before = LevelData();

        ApplyTeamExperience(exp);
        var after = LevelData();

        SynchronizeLevelData();
        return (before, after);
    }

    private void ApplyTeamExperience(ulong exp)
    {
        EnsureLevelBaseline();
        while (exp > 0)
        {
            var chunk = (uint)Math.Min(exp, uint.MaxValue);
            Progress.AddTeamExp(chunk);
            exp -= chunk;
        }
    }
}
