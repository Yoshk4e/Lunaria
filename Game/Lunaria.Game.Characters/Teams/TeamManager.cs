using Lunaria.Common.Tracking;
using Google.Protobuf;
using Lunaria.Game.Logging;
using Lunaria.Game.Resources;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.Game.Characters;

public sealed partial class TeamManager(GameData assets) : TrackedObject
{
    private static readonly ILogger Log = GameLog.Create("Lunaria.Game.Characters.Teams");

    public const int MaxMembers = (int)EnmSizeLimit.MaxCharacterTeamMembers;

    public const int MaxTeams = (int)EnmSizeLimit.MaxCharacterTeams;

    public const uint BigWorldTeamId = 1;

    /// <summary>The client reads this field as GBK. The default name uses ASCII.</summary>
    public const string DefaultTeamName = "TheBigBang";

    private readonly TrackedList<TeamState> __tracked_teams = [];
    [Tracked]
    private partial TrackedList<TeamState> _teams { get; }

    private uint __trackedCurrent = BigWorldTeamId;
    [Tracked]
    public partial uint Current { get; private set; }

    private uint __trackedUsingMemberSlot = 1;
    [Tracked]
    public partial uint UsingMemberSlot { get; private set; }

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
                .Select(m => (m.Slot, m.Gems, Character: characters.Get(m.InstId)))
                .Where(pair => pair.Character is not null)
                .DistinctBy(pair => pair.Slot)
                .DistinctBy(pair => pair.Character!.InstId)
                .OrderBy(pair => pair.Slot)
                .Select(pair => new TeamMemberState {
                    Slot = pair.Slot,
                    InstId = pair.Character!.InstId,
                    CharacterId = pair.Character.CharacterId,
                    Gems = pair.Gems.Take(assets.Gems.MaxPerCharacter)
                        .Select(id => assets.Gems.Exists(id) ? id : 0).ToArray()
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
        AcceptLoadedState();
    }

    public TeamState? CurrentTeam() => _teams.FirstOrDefault(t => t.TeamId == Current);

    public TeamState? Get(uint teamId) => _teams.FirstOrDefault(t => t.TeamId == teamId);

    public IReadOnlyList<ulong> CurrentMemberInstIds() =>
        CurrentTeam()?.Members.Select(m => m.InstId).ToList() ?? [];

    private uint SlotOrLowestOccupied(uint slot)
    {
        var members = CurrentTeam()?.Members;

        if (members is null || members.Count == 0)
            return 1;

        return members.Any(m => m.Slot == slot) ? slot : members.Min(m => m.Slot);
    }

    public IReadOnlyList<TeamData> TeamsData() => _teams.Select(ToTeamData).ToList();

    public TeamData ToTeamData(TeamState team) => ToTeamData(team.TeamId, team.Name, team.Members);

    public TeamData ToTeamData(uint teamId, string name, IReadOnlyList<TeamMemberState> members)
    {
        var elements = members.Select(member => assets.Characters.ElementOf(member.CharacterId))
            .CountBy(element => element).ToDictionary();
        return new TeamData {
            TeamId = teamId,
            Name = ByteString.CopyFromUtf8(name),
            MemberData = { members.Select(member => ToTeamMemberData(member, elements)) }
        };
    }

    public TeamMemberData ToTeamMemberData(TeamMemberState member, IReadOnlyDictionary<uint, int> teamElements) => new() {
        MemberSlotId = member.Slot,
        InstId = member.InstId,
        CharacterId = member.CharacterId,
        GemSlots = {
            member.Gems
                .Select((gemId, index) => (gemId, index))
                .Where(pair => pair.gemId != 0)
                .Select(pair => new GemSlotData {
                    GemSlotId = (uint)pair.index + 1,
                    GemItemid = pair.gemId,
                    GemState = assets.Gems.MeetsElementRequirements(pair.gemId, teamElements)
                        ? EnmGemStatus.Valid : EnmGemStatus.Invalid
                })
        }
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
