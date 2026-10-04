using Lunaria.Game.Player.Gameplay;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    public SCCharacterUpdateTeam UpdateTeam(TeamData? proposed)
    {
        if (proposed is null || ActiveTemporaryTeam is not null || Battles.Current is not null)
            return TeamUpdateResult(proposed?.TeamId ?? 0, (int)EnmTextCode.EnmTextWrongParam);

        // Catalysts travel in each member's gem_slots; the budget follows the highest world level reached.
        var (code, gems) = Teams.CheckGems(
            proposed.MemberData.Select(m => (m.MemberSlotId, m.GemSlots.Select(g => (g.GemSlotId, g.GemItemid)))),
            gemId => Bag.CountOf(gemId) > 0,
            assets.Progression.MaxGemCost(Progress.EarnedWorldLevel));

        if (code == 0)
            code = Teams.SetMembers(proposed.TeamId, proposed.MemberData
                .Select(m => (m.MemberSlotId, m.InstId, m.CharacterId)).ToList(), Characters, gems);

        return TeamUpdateResult(proposed.TeamId, code);
    }

    public SCCharacterUpdateTeam RenameTeam(uint teamId, string name) =>
        TeamUpdateResult(teamId, Teams.Rename(teamId, name));

    private SCCharacterUpdateTeam TeamUpdateResult(uint teamId, int code)
    {
        var team = Teams.Get(teamId);
        return new SCCharacterUpdateTeam {
            Result = code,
            TeamData = team is null ? null : Teams.ToTeamData(team),
            UsingMemberSlot = ActiveTemporaryTeam?.Slot ?? Teams.UsingMemberSlot,
            AttribData = { team?.Members.Select(m => Characters.AttribData(m.InstId)) ?? [] }
        };
    }

    public SCCharacterSwitchTeam SwitchTeam(uint teamId) => new() {
        Result = ActiveTemporaryTeam is not null || Battles.Current is not null ? (int)EnmTextCode.EnmTextWrongParam : Teams.SetCurrent(teamId),
        TeamId = Teams.Current,
        UsingMemberSlot = ActiveTemporaryTeam?.Slot ?? Teams.UsingMemberSlot,
        AttribData = { Teams.CurrentMemberInstIds().Select(id => Characters.AttribData(id)) }
    };

    public SCCharacterSwitchMember SwitchTeamMember(uint slot) => new() {
        Result = SetCurrentTeamSlot(slot),
        UsingMemberSlot = ActiveTemporaryTeam?.Slot ?? Teams.UsingMemberSlot
    };

    public SCSwitchMainCharacter SwitchMainCharacter(TeamData? proposed, uint slot)
    {
        if (ActiveTemporaryTeam is not null || Battles.Current is not null || proposed is null || proposed.TeamId != Teams.Current
            || proposed.MemberData.All(m => m.MemberSlotId != slot))
            return new SCSwitchMainCharacter { Result = (int)EnmTextCode.EnmTextWrongParam };

        var result = UpdateTeam(proposed);
        if (result.Result == 0) Teams.SetUsingMemberSlot(slot);
        return new SCSwitchMainCharacter {
            Result = result.Result, TeamData = result.TeamData,
            UsingMemberSlot = ActiveTemporaryTeam?.Slot ?? Teams.UsingMemberSlot,
            AttribData = { Teams.CurrentMemberInstIds().Select(id => Characters.AttribData(id)) }
        };
    }
}
