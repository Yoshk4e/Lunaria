using Google.Protobuf;
using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Characters;

public sealed partial class TeamManager(GameData assets)
{
    public const int MaxMembers = (int)EnmSizeLimit.MaxCharacterTeamMembers;

    public const int MaxTeams = (int)EnmSizeLimit.MaxCharacterTeams;

    public const uint BigWorldTeamId = 1;

    /// <summary>The client reads this field as GBK. The default name uses ASCII.</summary>
    public const string DefaultTeamName = "TheBigBang";

    private readonly List<TeamState> _teams = [];

    public bool IsDirty { get; private set; }

    public uint Current { get; private set; } = BigWorldTeamId;

    public uint UsingMemberSlot { get; private set; } = 1;

    public IReadOnlyList<TeamState> All => _teams;
    public bool IsEmpty => _teams.Count == 0;

    public bool GrantStarter(CharacterManager characters)
    {
        if (_teams.Any(t => t.Members.Count > 0))
            return false;

        var members = assets.Starter.Team
            .Select(characters.InstanceOf)
            .OfType<CharacterState>()
            .Take(MaxMembers)
            .Select((character, index) => new TeamMemberState {
                Slot = (uint)(index + 1),
                InstId = character.InstId,
                CharacterId = character.CharacterId
            })
            .ToList();

        if (members.Count == 0)
        {
            if (characters.All.FirstOrDefault() is not {} owned) return false;
            members.Add(new TeamMemberState { Slot = 1, InstId = owned.InstId, CharacterId = owned.CharacterId });
        }

        var starter = new TeamState {
            TeamId = BigWorldTeamId,
            Name = DefaultTeamName,
            Members = members,
            TemporaryLiquid = TeamLiquid.Empty,
            TemporaryLiquidLv2 = TeamLiquid.Empty
        };
        var index = _teams.FindIndex(t => t.TeamId == BigWorldTeamId);
        if (index >= 0) _teams[index] = starter;
        else _teams.Add(starter);
        Current = BigWorldTeamId;
        UsingMemberSlot = members[0].Slot;
        IsDirty = true;
        return true;
    }

    public void Load(IEnumerable<TeamState> persisted, uint current, uint usingMemberSlot, CharacterManager characters)
    {
        _teams.Clear();

        foreach (var team in persisted.Where(t => t.TeamId >= 1 && t.TeamId <= MaxTeams)
                     .DistinctBy(t => t.TeamId).Take(MaxTeams))
        {
            var members = team.Members
                .Where(m => m.Slot >= 1 && m.Slot <= MaxMembers)
                .Select(m => (m.Slot, Character: characters.Get(m.InstId)))
                .Where(pair => pair.Character is not null)
                .DistinctBy(pair => pair.Slot)
                .DistinctBy(pair => pair.Character!.InstId)
                .OrderBy(pair => pair.Slot)
                .Select(pair => new TeamMemberState {
                    Slot = pair.Slot,
                    InstId = pair.Character!.InstId,
                    CharacterId = pair.Character.CharacterId
                })
                .ToList();

            _teams.Add(team with {
                Members = members,
                TemporaryLiquid = team.TemporaryLiquid.Normalized(),
                TemporaryLiquidLv2 = team.TemporaryLiquidLv2.Normalized()
            });
        }

        Current = _teams.FirstOrDefault(t => t.TeamId == current && t.Members.Count > 0)?.TeamId
                  ?? _teams.FirstOrDefault(t => t.TeamId == BigWorldTeamId && t.Members.Count > 0)?.TeamId
                  ?? _teams.FirstOrDefault(t => t.Members.Count > 0)?.TeamId
                  ?? BigWorldTeamId;
        UsingMemberSlot = SlotOrLowestOccupied(usingMemberSlot);
        IsDirty = false;
    }

    public TeamState? CurrentTeam() => _teams.FirstOrDefault(t => t.TeamId == Current);

    public TeamState? Get(uint teamId) => _teams.FirstOrDefault(t => t.TeamId == teamId);

    public IReadOnlyList<ulong> CurrentMemberInstIds() =>
        CurrentTeam()?.Members.Select(m => m.InstId).ToList() ?? [];

    public void ClearDirty() => IsDirty = false;

    private uint SlotOrLowestOccupied(uint slot)
    {
        var members = CurrentTeam()?.Members;

        if (members is null || members.Count == 0)
            return 1;

        return members.Any(m => m.Slot == slot) ? slot : members.Min(m => m.Slot);
    }

    public IReadOnlyList<TeamData> TeamsData() => _teams.Select(ToTeamData).ToList();

    public TeamData ToTeamData(TeamState team) => new() {
        TeamId = team.TeamId,
        Name = ByteString.CopyFromUtf8(team.Name),
        MemberData = { team.Members.Select(ToTeamMemberData) }
    };

    public TeamMemberData ToTeamMemberData(TeamMemberState member) => new() {
        MemberSlotId = member.Slot,
        InstId = member.InstId,
        CharacterId = member.CharacterId
    };

    /// <summary>Loading requires team_data and numeric team_src and team_type values.</summary>
    public CurTeamData? CurTeamData(CharacterManager characters)
    {
        if (CurrentTeam() is not {} team)
            return null;

        return new CurTeamData {
            TeamType = 0,
            TeamSrc = team.TeamId,
            UsingMemberSlot = UsingMemberSlot,
            TeamData = ToTeamData(team),
            TemporaryLiquid = team.TemporaryLiquid.ToProto(),
            AttribData = { team.Members.Select(m => characters.AttribData(m.InstId)) }
        };
    }
}
