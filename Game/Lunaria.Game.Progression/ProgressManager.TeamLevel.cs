using Lunaria.Game.Logging;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.Game.Progression;

public sealed partial class ProgressManager
{
    private static readonly ILogger Log = GameLog.Create("Lunaria.Game.Progression");

    public TeamExpGrant AddTeamExp(uint exp)
    {
        if (exp == 0)
            return new TeamExpGrant(Code: 0, TeamLevel, TeamExp, LevelsGained: 0, WorldLevel, Dropped: 0);

        var pool = (ulong)TeamExp + exp;
        var level = TeamLevel;
        uint gained = 0;

        while (level < assets.Progression.TeamLevelCeiling(assets.Progression.WorldLevelFor(level, QuestGate))
               && assets.Progression.TeamExpToAdvance(level) is {} need
               && pool >= need)
        {
            pool -= need;
            level++;
            gained++;
        }

        var cap = assets.Progression.TeamExpCap(level);
        var dropped = pool > cap ? (uint)(pool - cap) : 0;

        TeamLevel = level;
        TeamExp = (uint)Math.Min(pool, cap);
        Dirty();

        if (gained > 0)
            Log.State("team level advanced to {Level} with {LevelsGained} levels gained from {Exp} exp", level, gained, exp);

        // Report full only for discarded XP. Reaching the cap exactly still succeeds.
        var code = dropped > 0 && level >= TeamLevelCeiling ? (int)EnmTextCode.EnmTextTeamExpFull : 0;

        if (dropped > 0)
            Log.Flag("team exp overflow dropped {Dropped} exp at level {Level}", dropped, level);

        return new TeamExpGrant(code, TeamLevel, TeamExp, gained, WorldLevel, dropped);
    }

    public uint? TeamExpToNextLevel() =>
        TeamLevel < TeamLevelCeiling && assets.Progression.TeamExpToAdvance(TeamLevel) is {} need ? need - Math.Min(TeamExp, need) : null;
}
