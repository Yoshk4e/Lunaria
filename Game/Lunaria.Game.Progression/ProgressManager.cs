using Lunaria.Common.Tracking;
using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Progression;

public sealed partial class ProgressManager(GameData assets) : TrackedObject
{
    private uint? __tracked_worldLevelSelection = default!;
    [Tracked]
    private partial uint? _worldLevelSelection { get; set; }

    private uint __trackedTeamLevel = Starter.TeamLevel;
    [Tracked]
    public partial uint TeamLevel { get; private set; }
    private uint __trackedTeamExp = default!;
    [Tracked]
    public partial uint TeamExp { get; private set; }
    private int __trackedSatiety = assets.Starter.Satiety;
    [Tracked]
    public partial int Satiety { get; private set; }
    private int __trackedStamina = assets.Starter.Stamina;
    [Tracked]
    public partial int Stamina { get; private set; }

    private DateTimeOffset __trackedStaminaTickAt = DateTimeOffset.UnixEpoch;
    [Tracked]
    public partial DateTimeOffset StaminaTickAt { get; private set; }

    /// <summary>Checks RequestTaskId in QuestMain. Null skips the gate until task state is loaded.</summary>
    [Untracked]
    public Func<uint, bool>? QuestGate { get; set; }

    public uint EarnedWorldLevel => assets.Progression.WorldLevelFor(TeamLevel, QuestGate);

    public uint WorldLevel => Math.Min(EarnedWorldLevel, _worldLevelSelection ?? EarnedWorldLevel);

    public uint TeamLevelCeiling => assets.Progression.TeamLevelCeiling(EarnedWorldLevel);

    public (int Result, uint WorldLevel) SelectWorldLevel(uint worldLevel)
    {
        if (worldLevel is < 1 || worldLevel > EarnedWorldLevel)
            return ((int)EnmTextCode.EnmTextWorldLevelNotEnough, WorldLevel);

        _worldLevelSelection = worldLevel;

        return (0, worldLevel);
    }

    public void Load(
        uint teamLevel,
        uint teamExp,
        int satiety,
        int stamina,
        DateTimeOffset staminaTickAt,
        uint? worldLevelSelection = null
    )
    {
        TeamLevel = Math.Clamp(teamLevel, Starter.TeamLevel, assets.Progression.TeamLevelLadderMax);
        TeamExp = Math.Min(teamExp, assets.Progression.TeamExpCap(TeamLevel));
        Satiety = Math.Clamp(satiety, min: 0, SatietyMax);
        Stamina = Math.Clamp(stamina, min: 0, StaminaMax);
        StaminaTickAt = staminaTickAt;

        _worldLevelSelection = worldLevelSelection is not null
                               && worldLevelSelection >= 1
                               && worldLevelSelection <= EarnedWorldLevel ?
            worldLevelSelection :
            null;
        AcceptLoadedState();
    }

}
