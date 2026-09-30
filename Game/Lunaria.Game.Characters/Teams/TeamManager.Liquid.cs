using Msg;

namespace Lunaria.Game.Characters;

public sealed partial class TeamManager
{
    /// <summary>Save both liquid levels even though the team packet only carries level 1.</summary>
    public bool ApplyBattleLiquid(ElementParamMap? temporaryLiquid, ElementParamMap? temporaryLiquidLv2)
    {
        if (CurrentTeam() is not {} team)
            return false;

        var level1 = TeamLiquid.FromProto(temporaryLiquid);
        var level2 = TeamLiquid.FromProto(temporaryLiquidLv2);

        if (level1 == team.TemporaryLiquid && level2 == team.TemporaryLiquidLv2)
            return false;

        Replace(team with {
            TemporaryLiquid = level1,
            TemporaryLiquidLv2 = level2
        });
        return true;
    }

    public bool AddTemporaryLiquid(int element, int basisPoints)
    {
        if (CurrentTeam() is not {} team || element is < 1 or > 7 || basisPoints <= 0)
            return false;

        var next = team.TemporaryLiquid.Add(element, basisPoints);

        if (next == team.TemporaryLiquid)
            return false;

        Replace(team with { TemporaryLiquid = next });
        return true;
    }

    /// <summary>The client applies this to the active team and ignores the team selectors.</summary>
    public SCTemporaryLiquidPoolNtf TemporaryLiquidNotification()
    {
        var team = CurrentTeam();

        return new SCTemporaryLiquidPoolNtf {
            TeamType = 0,
            TeamSrc = team?.TeamId ?? Current,
            TemporaryLiquid = (team?.TemporaryLiquid ?? TeamLiquid.Empty).ToProto()
        };
    }
}
