using Lunaria.Common.Tracking;
using Lunaria.Game.Logging;
using Lunaria.Game.Resources.Tables;
using Lunaria.Game.Resources;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.Game.Characters.Teams;

public sealed record TempTeamState(uint TeamSrc, IReadOnlyList<TeamMemberState> Members);

public sealed partial class TempTeamManager(GameData assets) : TrackedObject
{
    private static readonly ILogger Log = GameLog.Create("Lunaria.Game.Characters.TempTeams");

    private readonly TrackedDictionary<uint, TempTeamState> __tracked_teams = [];
    [Tracked]
    private partial TrackedDictionary<uint, TempTeamState> _teams { get; }
    private readonly TeamManager _teamData = new(assets);

    public IReadOnlyDictionary<uint, TempTeamState> Teams => _teams;

    public void Load(IEnumerable<(uint TeamSrc, IEnumerable<(uint Slot, uint CharacterId)> Members)> persisted)
        => Load(persisted.Select(row => (row.TeamSrc, row.Members.Select(m => new TeamMemberState {
            Slot = m.Slot, InstId = m.CharacterId, CharacterId = m.CharacterId
        }))));

    public void Load(IEnumerable<(uint TeamSrc, IEnumerable<TeamMemberState> Members)> persisted)
    {
        _teams.Clear();

        foreach (var row in persisted)
        {
            var table = assets.TmpTeams.GetBySrc(row.TeamSrc);

            if (table is null)
                continue;

            var valid = FieldableIds(table).ToHashSet();
            var members = new List<TeamMemberState>();

            foreach (var member in row.Members)
            {
                var (slot, characterId) = (member.Slot, member.CharacterId);
                if (valid.Contains(characterId) && slot is >= 1 and <= TeamManager.MaxMembers
                    && members.All(m => m.Slot != slot && m.CharacterId != characterId))
                    members.Add(member with { InstId = characterId,
                        Gems = member.Gems.Take(assets.Gems.MaxPerCharacter)
                            .Select(id => assets.Gems.Exists(id) ? id : 0).ToArray() });
            }

            if (members.Count > 0)
                _teams[row.TeamSrc] = new TempTeamState(row.TeamSrc, members);
        }

        AcceptLoadedState();
    }

    public TeamData? ToTeamData(int teamType, uint teamSrc)
    {
        var table = assets.TmpTeams.Get(teamType, teamSrc);

        if (table is null)
            return null;

        return _teamData.ToTeamData(teamSrc, TeamManager.DefaultTeamName, MembersOf(table));
    }

    public CurTeamData? ToCurTeamData(PTmpTeamTable table)
    {
        var members = MembersOf(table);

        if (members.Count == 0)
            return null;

        var rows = assets.TmpTeams.MembersOf(table);

        return new CurTeamData {
            TeamType = (int)EnmTmpTeamType.Task,
            TeamSrc = table.Id,
            UsingMemberSlot = members.Min(member => member.Slot),
            TeamData = _teamData.ToTeamData(table.Id, TeamManager.DefaultTeamName, members),
            TemporaryLiquid = InitialLiquid(table).ToProto(),
            AttribData = { members.Select(member => AttribDataOf(rows.First(r => r.CharacterId == member.CharacterId), member.InstId)) }
        };
    }

    public (int Result, TeamData? Team) Update(int teamType, uint teamSrc, TeamData? data,
        IReadOnlyDictionary<uint, IReadOnlyList<uint>>? gems = null)
    {
        var table = assets.TmpTeams.Get(teamType, teamSrc);

        if (table is null || data is null || data.TeamId != teamSrc)
            return ((int)EnmTextCode.EnmTextWrongParam, null);

        // Nonempty gem selections must pass the player's ownership and cost checks.
        if (gems is null && data.MemberData.Any(m => m.GemSlots.Count > 0))
            return ((int)EnmTextCode.EnmTextWrongParam, null);

        var maxMembers = TeamManager.MaxMembers;

        if (data.MemberData.Count == 0 || data.MemberData.Count > maxMembers)
            return ((int)EnmTextCode.EnmTextWrongParam, null);

        var fieldable = FieldableIds(table).ToHashSet();
        var seenCharacters = new HashSet<uint>();
        var seenSlots = new HashSet<uint>();
        var members = new List<TeamMemberState>();

        foreach (var member in data.MemberData)
        {
            if (!fieldable.Contains(member.CharacterId)
                || member.InstId != member.CharacterId
                || !seenCharacters.Add(member.CharacterId)
                || member.MemberSlotId < 1
                || member.MemberSlotId > maxMembers
                || !seenSlots.Add(member.MemberSlotId))
                return ((int)EnmTextCode.EnmTextWrongParam, null);

            members.Add(new TeamMemberState {
                Slot = member.MemberSlotId,
                InstId = member.CharacterId,
                CharacterId = member.CharacterId,
                Gems = gems?.GetValueOrDefault(member.MemberSlotId) ?? []
            });
        }

        _teams[teamSrc] = new TempTeamState(teamSrc, members.OrderBy(member => member.Slot).ToList());

        Log.Stage("story team {TeamSrc} selection updated with {MemberCount} members", teamSrc, members.Count);
        return (0, ToTeamData(teamType, teamSrc));
    }

    public PBCharacterAttribData AttribDataOf(PTmpCharacterTable row, ulong instId, int? hp = null, int? liquid = null)
    {
        var characterId = row.CharacterId;
        var develop = assets.Characters.DevelopAttributeId(characterId, row.Level);
        var attribs = assets.Attribs.ForCharacter(
            characterId, develop,
            hp ?? Ratio(assets.Attribs.MaxHp(characterId, develop), row.CharacterHpRatio),
            liquid ?? Ratio(assets.Attribs.PermanentLiquidMax(assets.Characters.FixedAttributeId(characterId)), row.CharacterPermanentLiquidRatio),
            assets.Characters.FixedAttributeId(characterId));

        return new PBCharacterAttribData {
            InstId = instId,
            AttribData = {
                attribs.Select(pair => new PBAttribDataElem {
                    AttribType = pair.Id,
                    BaseValue = pair.Value,
                    FinalValue = pair.Value
                })
            }
        };
    }

    public static int Ratio(int maximum, uint ratio) => (int)((long)maximum * Math.Min(ratio, 10_000u) / 10_000);

    public static TeamLiquid InitialLiquid(PTmpTeamTable row)
    {
        var liquid = TeamLiquid.Empty;
        for (var element = 1; element <= 7; element++) liquid = liquid.Add(element, (int)Math.Min(row.TemporaryLiquidRatio, 10_000u));
        return liquid;
    }

    private IEnumerable<uint> FieldableIds(PTmpTeamTable table) =>
        assets.TmpTeams.MembersOf(table).Select(member => member.CharacterId);

    private IReadOnlyList<TeamMemberState> MembersOf(PTmpTeamTable table)
    {
        if (_teams.TryGetValue(table.Id, out var state))
            return state.Members;

        return assets.TmpTeams.MembersOf(table)
            .Select((member, index) => new TeamMemberState {
                Slot = (uint)(index + 1),
                InstId = member.CharacterId,
                CharacterId = member.CharacterId
            })
            .ToList();
    }

}
