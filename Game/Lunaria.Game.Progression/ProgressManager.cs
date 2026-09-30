using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Progression;

public sealed partial class ProgressManager(GameData assets)
{
    private uint? _worldLevelSelection;
    public bool IsDirty { get; private set; }

    public uint TeamLevel { get; private set; } = Starter.TeamLevel;
    public uint TeamExp { get; private set; }
    public int Satiety { get; private set; } = assets.Starter.Satiety;
    public int Stamina { get; private set; } = assets.Starter.Stamina;

    public DateTimeOffset StaminaTickAt { get; private set; } = DateTimeOffset.UnixEpoch;

    /// <summary>Checks RequestTaskId in QuestMain. Null skips the gate until task state is loaded.</summary>
    public Func<uint, bool>? QuestGate { get; set; }

    public uint EarnedWorldLevel => assets.Progression.WorldLevelFor(TeamLevel, QuestGate);

    public uint WorldLevel => Math.Min(EarnedWorldLevel, _worldLevelSelection ?? EarnedWorldLevel);

    public uint TeamLevelCeiling => assets.Progression.TeamLevelCeiling(EarnedWorldLevel);

    public (int Result, uint WorldLevel) SelectWorldLevel(uint worldLevel)
    {
        if (worldLevel is < 1 || worldLevel > EarnedWorldLevel)
            return ((int)EnmTextCode.EnmTextWorldLevelNotEnough, WorldLevel);

        _worldLevelSelection = worldLevel;
        Dirty();
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
        IsDirty = false;
    }

    public void ClearDirty() => IsDirty = false;

    private void Dirty() => IsDirty = true;
}
