using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

public sealed class TmpTeamAssets
{
    private readonly HashSet<uint> _droppedMembers = [];
    private readonly Dictionary<uint, PTmpCharacterTable> _members = [];
    private readonly Dictionary<uint, PTmpTeamTable> _teams = [];
    private readonly Dictionary<(uint TaskType, ulong Step), List<PTmpTeamTable>> _teamsOfStep = [];

    public TmpTeamAssets(
        IReadOnlyDictionary<string, PTmpTeamTable> teams,
        IReadOnlyDictionary<string, PTmpCharacterTable> members,
        CharacterAssets characters
    )
    {
        foreach (var row in teams.Values)
        {
            _teams[row.Id] = row;

            foreach (var stepId in row.StepId)
            {
                var key = (row.TaskType, stepId);
                (_teamsOfStep.TryGetValue(key, out var list) ? list : _teamsOfStep[key] = []).Add(row);
            }
        }

        foreach (var row in members.Values)
        {
            _members[row.Id] = row;
        }

        if (_teams.Count == 0)
            throw new ResourceException("P_TmpTeamTable.json", "p_tmpteamtable has no rows");

        if (_members.Count == 0)
            throw new ResourceException("P_TmpCharacterTable.json", "p_tmpcharactertable has no rows");

        foreach (var row in _teams.Values)
        {
            foreach (var memberId in row.TmpCharacters)
            {
                if (!_members.ContainsKey(memberId))
                    throw new ResourceException(
                        "P_TmpTeamTable.json", $"tmp team {row.Id} references missing tmp character {memberId}");
            }
        }

        foreach (var member in _members.Values)
        {
            if (!characters.Exists(member.CharacterId))
                _droppedMembers.Add(member.Id);
        }

        if (_teams.Values.All(team => MembersOf(team).Count == 0))
            throw new ResourceException(
                "P_TmpCharacterTable.json", "every tmp team member references a missing character");
    }

    public PTmpTeamTable? Get(int teamType, uint teamSrc) =>
        teamType == (int)Msg.EnmTmpTeamType.Task ? _teams.GetValueOrDefault(teamSrc) : null;

    /// <summary>Saved teams use src alone because row IDs are unique across types.</summary>
    public PTmpTeamTable? GetBySrc(uint teamSrc) => _teams.GetValueOrDefault(teamSrc);

    public IReadOnlyList<PTmpTeamTable> TeamsOfStep(uint taskType, ulong stepId) =>
        _teamsOfStep.GetValueOrDefault((taskType, stepId)) ?? [];

    public PTmpCharacterTable? Member(uint memberId) =>
        _members.GetValueOrDefault(memberId) is {} member && !_droppedMembers.Contains(memberId) ? member : null;

    public IReadOnlyList<PTmpCharacterTable> MembersOf(PTmpTeamTable team) =>
        team.TmpCharacters
            .Select(Member)
            .Where(member => member is not null)
            .Select(member => member!)
            .ToList();
}
