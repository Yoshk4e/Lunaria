using Google.Protobuf;
using Lunaria.Game.Resources;
using Lunaria.Game.Resources.Tables;
using Msg;

namespace Lunaria.Game.Characters.Teams;

public sealed record TempTeamState(uint TeamSrc, IReadOnlyList<TeamMemberState> Members);

public sealed class TempTeamManager(GameData assets)
{
    private readonly Dictionary<uint, TempTeamState> _teams = [];

    public bool IsDirty { get; private set; }

    public IReadOnlyDictionary<uint, TempTeamState> Teams => _teams;

    public void Load(IEnumerable<(uint TeamSrc, IEnumerable<(uint Slot, uint CharacterId)> Members)> persisted)
    {
        _teams.Clear();

        foreach (var row in persisted)
        {
            var table = assets.TmpTeams.GetBySrc(row.TeamSrc);

            if (table is null)
                continue;

            var valid = FieldableIds(table).ToHashSet();
            var members = new List<TeamMemberState>();

            foreach (var (slot, characterId) in row.Members)
            {
                if (valid.Contains(characterId) && slot is >= 1 and <= TeamManager.MaxMembers
                    && members.All(m => m.Slot != slot && m.CharacterId != characterId))
                    members.Add(new TeamMemberState { Slot = slot, InstId = characterId, CharacterId = characterId });
            }

            if (members.Count > 0)
                _teams[row.TeamSrc] = new TempTeamState(row.TeamSrc, members);
        }

        IsDirty = false;
    }

    public void ClearDirty() => IsDirty = false;

    public TeamData? ToTeamData(int teamType, uint teamSrc)
    {
        var table = assets.TmpTeams.Get(teamType, teamSrc);

        if (table is null)
            return null;

        return new TeamData {
            TeamId = teamSrc,
            Name = ByteString.CopyFromUtf8(TeamManager.DefaultTeamName),
            MemberData = { MembersOf(table).Select(ToMemberData) }
        };
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
            TeamData = new TeamData {
                TeamId = table.Id,
                Name = ByteString.CopyFromUtf8(TeamManager.DefaultTeamName),
                MemberData = { members.Select(ToMemberData) }
            },
            TemporaryLiquid = InitialLiquid(table).ToProto(),
            AttribData = { members.Select(member => AttribDataOf(rows.First(r => r.CharacterId == member.CharacterId), member.InstId)) }
        };
    }

    public (int Result, TeamData? Team) Update(int teamType, uint teamSrc, TeamData? data)
    {
        var table = assets.TmpTeams.Get(teamType, teamSrc);

        if (table is null || data is null || data.TeamId != teamSrc)
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
                CharacterId = member.CharacterId
            });
        }

        _teams[teamSrc] = new TempTeamState(teamSrc, members.OrderBy(member => member.Slot).ToList());
        Dirty();
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

    private static TeamMemberData ToMemberData(TeamMemberState member) => new() {
        MemberSlotId = member.Slot,
        InstId = member.InstId,
        CharacterId = member.CharacterId
    };

    private void Dirty() => IsDirty = true;
}
