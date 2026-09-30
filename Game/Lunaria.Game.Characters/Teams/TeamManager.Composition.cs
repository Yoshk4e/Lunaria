using System.Text;
using Msg;

namespace Lunaria.Game.Characters;

public sealed partial class TeamManager
{
    public int SetMembers(
        uint teamId,
        IReadOnlyList<(uint Slot, ulong InstId, uint CharacterId)> members,
        CharacterManager characters
    )
    {
        if (Get(teamId) is not {} team)
            return (int)EnmTextCode.EnmTextCharacterInvalidTeamid;

        if (members.Count == 0)
            return teamId == BigWorldTeamId ?
                (int)EnmTextCode.EnmTextCharacterCannotClearMembersInMainTeam :
                (int)EnmTextCode.EnmTextCharacterNoMemberInTargetTeam;

        if (members.Count > MaxMembers)
            return (int)EnmTextCode.EnmTextCharacterInvalidTeamMemberIndex;

        if (members.Any(m => m.Slot < 1 || m.Slot > MaxMembers))
            return (int)EnmTextCode.EnmTextCharacterInvalidTeamMemberIndex;

        if (members.Select(m => m.Slot).Distinct().Count() != members.Count)
            return (int)EnmTextCode.EnmTextCharacterInvalidTeamMemberIndex;

        if (members.Select(m => m.InstId).Distinct().Count() != members.Count)
            return (int)EnmTextCode.EnmTextCharacterTeamMemberDuplicate;

        var resolved = new List<TeamMemberState>(members.Count);

        foreach (var (slot, instId, characterId) in members.OrderBy(m => m.Slot))
        {
            if (characters.Get(instId) is not {} character)
                return (int)EnmTextCode.EnmTextCharacterNotExist;

            if (characterId != character.CharacterId)
                return (int)EnmTextCode.EnmTextWrongParam;

            resolved.Add(new TeamMemberState {
                Slot = slot,
                InstId = instId,
                CharacterId = character.CharacterId
            });
        }

        Replace(team with { Members = resolved });

        // Keep the controlled slot occupied or the client cannot spawn a character.
        if (teamId == Current)
            UsingMemberSlot = SlotOrLowestOccupied(UsingMemberSlot);

        return 0;
    }

    /// <summary>MAX_TEAM_NAME_LEN limits bytes, not characters.</summary>
    public int Rename(uint teamId, string name)
    {
        if (Get(teamId) is not {} team)
            return (int)EnmTextCode.EnmTextCharacterInvalidTeamid;

        if (string.IsNullOrWhiteSpace(name))
            return (int)EnmTextCode.EnmTextWrongParam;

        if (Encoding.UTF8.GetByteCount(name) > (int)EnmSizeLimit.MaxTeamNameLen)
            return (int)EnmTextCode.EnmTextCharacterTeamNameTooLong;

        if (name == team.Name)
            return 0;

        Replace(team with { Name = name });
        return 0;
    }

    public int SetCurrent(uint teamId)
    {
        if (Get(teamId) is not {} team)
            return (int)EnmTextCode.EnmTextCharacterInvalidTeamid;

        if (team.Members.Count == 0)
            return (int)EnmTextCode.EnmTextCharacterNoMemberInTargetTeam;

        if (teamId == Current)
            return 0;

        Current = teamId;
        UsingMemberSlot = SlotOrLowestOccupied(UsingMemberSlot);
        IsDirty = true;
        return 0;
    }

    public int SetUsingMemberSlot(uint slot)
    {
        if (CurrentTeam() is not {} team)
            return (int)EnmTextCode.EnmTextCharacterInvalidTeamid;

        if (!team.Members.Any(m => m.Slot == slot))
            return (int)EnmTextCode.EnmTextCharacterNobodySitAtIndex;

        if (slot == UsingMemberSlot)
            return 0;

        UsingMemberSlot = slot;
        IsDirty = true;
        return 0;
    }

    public void Withdraw(ulong instId)
    {
        for (var index = 0; index < _teams.Count; index++)
        {
            var team = _teams[index];

            if (!team.Has(instId))
                continue;

            _teams[index] = team with { Members = team.Members.Where(m => m.InstId != instId).ToList() };
            IsDirty = true;
        }

        UsingMemberSlot = SlotOrLowestOccupied(UsingMemberSlot);
    }

    private void Replace(TeamState team)
    {
        var index = _teams.FindIndex(t => t.TeamId == team.TeamId);

        if (index < 0)
            return;

        _teams[index] = team;
        IsDirty = true;
    }
}
