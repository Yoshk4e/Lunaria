using Lunaria.Common.Tracking;
using Lunaria.Game.Characters.Teams;
using Lunaria.Game.Characters;
using Lunaria.Game.Logging;
using Lunaria.Game.Resources.Tables;
using Lunaria.Game.Tasks;
using Msg;

namespace Lunaria.Game.Player;

// Editing a future lineup must not change the current fight or permanent team.
public sealed record TemporaryTeamSelection(EnmTmpTeamType Type, uint Source, IReadOnlyList<TeamMemberState> Members);
public sealed record TrialVitals(ulong InstanceId, uint TemplateId, int Hp, int Liquid);
/// <summary>HP and permanent liquid an owned character uses during a dungeon run, kept apart from its own.</summary>
public sealed record RunVitals(ulong InstanceId, int Hp, int Liquid);

public sealed record ActiveTemporaryTeam(
    EnmTmpTeamType Type, uint Source, uint Slot, IReadOnlyList<TeamMemberState> Members,
    IReadOnlyList<TrialVitals> Trials, TeamLiquid Liquid, TeamLiquid LiquidLv2, IReadOnlyList<RunVitals>? Runs = null);

public sealed partial class Player : TrackedObject
{
    private readonly TrackedDictionary<(EnmTmpTeamType, uint), TemporaryTeamSelection> __tracked_temporarySelections = [];
    [Tracked]
    private partial TrackedDictionary<(EnmTmpTeamType, uint), TemporaryTeamSelection> _temporarySelections { get; }
    public IReadOnlyCollection<TemporaryTeamSelection> TemporarySelections => (IReadOnlyCollection<TemporaryTeamSelection>)_temporarySelections.Values;
    private ActiveTemporaryTeam? __trackedActiveTemporaryTeam = default!;
    [Tracked]
    public partial ActiveTemporaryTeam? ActiveTemporaryTeam { get; private set; }
    private ActiveTemporaryTeam? __trackedSuspendedStoryTeam = default!;
    [Tracked]
    public partial ActiveTemporaryTeam? SuspendedStoryTeam { get; private set; }
    public bool TemporaryTeamDirty => Changes.IsChanged(nameof(_temporarySelections))
        || Changes.IsChanged(nameof(ActiveTemporaryTeam)) || Changes.IsChanged(nameof(SuspendedStoryTeam));

    private bool ValidTemporarySource(EnmTmpTeamType type, uint source) => type switch {
        EnmTmpTeamType.Task => assets.TmpTeams.GetBySrc(source) is {} row && assets.TmpTeams.MembersOf(row).Count > 0,
        // The CBT1 client keys a dungeon team by its dungeon type (P_DungeonsTypeTable), shared by every stage of that type.
        EnmTmpTeamType.Dungeon => assets.Dungeons.Type(source) is not null,
        EnmTmpTeamType.Wanted => assets.Wanted.Entry(source) is not null,
        _ => false
    };

    private uint DungeonTeamSource(ulong dungeonId) =>
        dungeonId <= uint.MaxValue ? assets.Dungeons.Dungeon((uint)dungeonId)?.DungeonType ?? 0 : 0;

    public TeamData? QueryTemporaryTeam(int type, uint source)
    {
        var kind = (EnmTmpTeamType)type;
        if (!ValidTemporarySource(kind, source))
        {
            Log.Flag("temporary team query refused: unknown source {TeamSource} for type {TeamType}", source, kind);
            return null;
        }
        if (ActiveTemporaryTeam is {} active && active.Type == kind && active.Source == source)
            return TemporaryTeamData(source, active.Members);
        if (kind == EnmTmpTeamType.Task) return TempTeams.ToTeamData(type, source);
        var members = _temporarySelections.GetValueOrDefault((kind, source))?.Members ?? Teams.CurrentTeam()?.Members ?? [];
        return TemporaryTeamData(source, members);
    }

    public (int Result, TeamData? Team) UpdateTemporaryTeam(int type, uint source, TeamData? data)
    {
        var kind = (EnmTmpTeamType)type;
        if (!ValidTemporarySource(kind, source) || data is null || data.TeamId != source
            || Battles.Current is not null
            || ActiveTemporaryTeam is {} active && active.Type == kind && active.Source == source)
            return ((int)EnmTextCode.EnmTextWrongParam, null);
        if (kind != EnmTmpTeamType.Task && !ValidOwnedMembers(data.MemberData))
            return ((int)EnmTextCode.EnmTextWrongParam, null);
        var (code, gems) = Teams.CheckGems(
            data.MemberData.Select(m => (m.MemberSlotId, m.GemSlots.Select(g => (g.GemSlotId, g.GemItemid)))),
            gemId => Bag.CountOf(gemId) > 0,
            assets.Progression.MaxGemCost(Progress.EarnedWorldLevel));
        if (code != 0)
        {
            Log.Stage("temporary team gem selection refused for type {TeamType} source {TeamSource} with code {Code}", kind, source, code);
            return (code, null);
        }
        if (kind == EnmTmpTeamType.Task) return TempTeams.Update(type, source, data, gems);
        _temporarySelections[(kind, source)] = new(kind, source, data.MemberData.OrderBy(m => m.MemberSlotId)
            .Select(m => new TeamMemberState { Slot = m.MemberSlotId, InstId = m.InstId, CharacterId = m.CharacterId,
                Gems = gems.GetValueOrDefault(m.MemberSlotId) ?? [] }).ToArray());

        Log.Stage("temporary team selection updated for type {TeamType} source {TeamSource} with {MemberCount} members", kind, source, data.MemberData.Count);
        return (0, QueryTemporaryTeam(type, source));
    }

    private bool ValidOwnedMembers(IEnumerable<TeamMemberData> proposed)
    {
        var members = proposed.ToArray();
        return members.Length is > 0 and <= TeamManager.MaxMembers
            && members.All(m => m.MemberSlotId is >= 1 and <= TeamManager.MaxMembers
                && Characters.Get(m.InstId)?.CharacterId == m.CharacterId)
            && members.Select(m => m.MemberSlotId).Distinct().Count() == members.Length
            && members.Select(m => m.InstId).Distinct().Count() == members.Length
            && members.Select(m => m.CharacterId).Distinct().Count() == members.Length;
    }

    private TeamData TemporaryTeamData(uint source, IReadOnlyList<TeamMemberState> members) =>
        Teams.ToTeamData(source, TeamManager.DefaultTeamName, members);

    public bool ReconcileTemporaryTeam(bool notify = true)
    {
        // Keep fight participants until settlement even if task reports advance the story.
        if (Battles.Current is not null) return false;
        var storyTeams = Tasks.Processing.Values.Where(t => StepPlaysInLiveMap(t.Type, t.CurrentStep.StepId))
            .SelectMany(t => assets.TmpTeams.TeamsOfStep(t.Type, t.CurrentStep.StepId))
            .Where(t => assets.TmpTeams.MembersOf(t).Count > 0).DistinctBy(t => t.Id).OrderBy(t => t.Id).ToArray();
        var story = storyTeams.FirstOrDefault(t => ActiveTemporaryTeam is { Type: EnmTmpTeamType.Task } active && active.Source == t.Id)
            ?? storyTeams.FirstOrDefault();
        var type = InWantedRun ? EnmTmpTeamType.Wanted : Dungeons.Current is not null ? EnmTmpTeamType.Dungeon
            : story is not null ? EnmTmpTeamType.Task : EnmTmpTeamType.None;
        var source = type switch {
            EnmTmpTeamType.Wanted => Wanted.CurrentEntryId,
            EnmTmpTeamType.Dungeon => DungeonTeamSource(Dungeons.Current!.Value.DungeonId),
            EnmTmpTeamType.Task => story!.Id,
            _ => 0u
        };
        if (ActiveTemporaryTeam is {} current && current.Type == type && current.Source == source) return false;
        if (ActiveTemporaryTeam is null && type == EnmTmpTeamType.None) return false;

        if (ActiveTemporaryTeam is { Type: EnmTmpTeamType.Task } previous && type is EnmTmpTeamType.Wanted or EnmTmpTeamType.Dungeon)
            SuspendedStoryTeam = previous;
        ActiveTemporaryTeam = null;
        if (type == EnmTmpTeamType.Task && SuspendedStoryTeam is {} suspended && suspended.Source == source)
        {
            ActiveTemporaryTeam = suspended;
            SuspendedStoryTeam = null;
        }
        if (type is EnmTmpTeamType.Task or EnmTmpTeamType.None) SuspendedStoryTeam = null;
        if (ActiveTemporaryTeam is null && type != EnmTmpTeamType.None && QueryTemporaryTeam((int)type, source) is {} team && team.MemberData.Count > 0)
        {
            var trials = new List<TrialVitals>();
            var members = team.MemberData.Select(m => {
                var id = m.InstId;
                if (type == EnmTmpTeamType.Task)
                {
                    var template = assets.TmpTeams.MembersOf(story!).First(r => r.CharacterId == m.CharacterId);
                    id = Guid.Next();
                    trials.Add(new(id, template.Id,
                        TempTeamManager.Ratio(assets.Attribs.MaxHp(template.CharacterId, assets.Characters.DevelopAttributeId(template.CharacterId, template.Level)), template.CharacterHpRatio),
                        TempTeamManager.Ratio(assets.Attribs.PermanentLiquidMax(assets.Characters.FixedAttributeId(template.CharacterId)), template.CharacterPermanentLiquidRatio)));
                }
                var gems = new uint[assets.Gems.MaxPerCharacter];
                foreach (var gem in m.GemSlots) gems[gem.GemSlotId - 1] = gem.GemItemid;
                return new TeamMemberState { Slot = m.MemberSlotId, InstId = id, CharacterId = m.CharacterId,
                    Gems = gems.Any(g => g != 0) ? gems : [] };
            }).ToArray();
            ActiveTemporaryTeam = new(type, source, members.Min(m => m.Slot), members, trials,
                story is not null && type == EnmTmpTeamType.Task ? TempTeamManager.InitialLiquid(story) : TeamLiquid.Empty, TeamLiquid.Empty,
                type == EnmTmpTeamType.Dungeon ? DungeonRunVitals(source, members) : null);
        }

        Log.State("active temporary team changed to type {TeamType} source {TeamSource} with {MemberCount} members, suspended story source {SuspendedSource}",
            type, source, ActiveTemporaryTeam?.Members.Count ?? 0, SuspendedStoryTeam?.Source);
        if (notify && CurrentTeamData() is {} data) _changes.Add(new SCCharacterTempTeamNtf { CurTeam = data });
        return true;
    }

    /// <summary>Apply a story team only on the step's map. Actions without MapID can run anywhere.</summary>
    private bool StepPlaysInLiveMap(uint type, ulong stepId)
    {
        var maps = assets.Tasks.Actions(type, stepId)
            .Select(action => assets.Tasks.Action(type, action))
            .Where(row => row is { MapId: > 0 })
            .Select(row => row!.MapId)
            .ToArray();
        return maps.Length == 0 || maps.Any(map => TaskManager.MatchesMap(map, Map.MapId));
    }

    /// <summary>
    /// A dungeon run starts every participant at its dungeon type's HP and liquid ratios (P_DungeonsTypeTable) and
    /// discards those values when the run ends, so the owned characters keep the vitals they entered with.
    /// </summary>
    private IReadOnlyList<RunVitals> DungeonRunVitals(uint dungeonType, IEnumerable<TeamMemberState> members) =>
        assets.Dungeons.Type(dungeonType) is not {} row ? [] : members
            .Where(m => Characters.Owns(m.InstId))
            .Select(m => new RunVitals(m.InstId,
                TempTeamManager.Ratio(Characters.MaxHp(m.InstId), row.CharacterHpRatio),
                TempTeamManager.Ratio(Characters.PermanentLiquidMax(m.InstId), row.CharacterPermanentLiquidRatio)))
            .ToArray();

    private RunVitals? Run(ulong id) => ActiveTemporaryTeam?.Runs?.FirstOrDefault(r => r.InstanceId == id);

    public CurTeamData? CurrentTeamData() => ActiveTemporaryTeam is {} active ? new CurTeamData {
        TeamType = (int)active.Type, TeamSrc = active.Source, UsingMemberSlot = active.Slot,
        TeamData = TemporaryTeamData(active.Source, active.Members), TemporaryLiquid = active.Liquid.ToProto(),
        AttribData = { active.Members.Select(m => OutsideAttributes(m.InstId)) }
    } : Teams.CurTeamData(Characters);

    public IReadOnlyList<ulong> CurrentTeamMembers() => ActiveTemporaryTeam?.Members.Select(m => m.InstId).ToArray() ?? Teams.CurrentMemberInstIds();
    private TrialVitals? Trial(ulong id) => ActiveTemporaryTeam?.Trials.FirstOrDefault(t => t.InstanceId == id);
    private PTmpCharacterTable TrialTemplate(TrialVitals trial) => assets.TmpTeams.Member(trial.TemplateId)!;
    public PBCharacterAttribData OutsideAttributes(ulong id) => Trial(id) is {} trial
        ? TempTeams.AttribDataOf(TrialTemplate(trial), id, trial.Hp, trial.Liquid)
        : Characters.AttribData(id, Run(id)?.Hp, Run(id)?.Liquid);
    public int TeamCharacterHp(ulong id) => Trial(id)?.Hp ?? Run(id)?.Hp ?? Characters.Hp(id);
    public int TeamCharacterLiquid(ulong id) => Trial(id)?.Liquid ?? Run(id)?.Liquid ?? Characters.PermanentLiquid(id);
    public int TeamCharacterMaxHp(ulong id) => Trial(id) is {} trial
        ? assets.Attribs.MaxHp(TrialTemplate(trial).CharacterId, assets.Characters.DevelopAttributeId(TrialTemplate(trial).CharacterId, TrialTemplate(trial).Level)) : Characters.MaxHp(id);
    public int TeamCharacterMaxLiquid(ulong id) => Trial(id) is {} trial
        ? assets.Attribs.PermanentLiquidMax(assets.Characters.FixedAttributeId(TrialTemplate(trial).CharacterId)) : Characters.PermanentLiquidMax(id);

    public void SetTeamCharacterVitals(ulong id, int hp = -1, int liquid = -1)
    {
        if (Trial(id) is {} trial)
        {
            var next = trial with { Hp = hp < 0 ? trial.Hp : Math.Clamp(hp, 0, TeamCharacterMaxHp(id)),
                Liquid = liquid < 0 ? trial.Liquid : Math.Clamp(liquid, 0, TeamCharacterMaxLiquid(id)) };
            if (next == trial) return;
            ActiveTemporaryTeam = ActiveTemporaryTeam! with { Trials = ActiveTemporaryTeam.Trials.Select(t => t.InstanceId == id ? next : t).ToArray() };

        }
        else if (Run(id) is {} run)
        {
            var next = run with { Hp = hp < 0 ? run.Hp : Math.Clamp(hp, 0, TeamCharacterMaxHp(id)),
                Liquid = liquid < 0 ? run.Liquid : Math.Clamp(liquid, 0, TeamCharacterMaxLiquid(id)) };
            if (next != run)
                ActiveTemporaryTeam = ActiveTemporaryTeam! with { Runs = ActiveTemporaryTeam.Runs!.Select(r => r.InstanceId == id ? next : r).ToArray() };
        }
        else
        {
            if (hp >= 0) Characters.SetHp(id, hp);
            if (liquid >= 0) Characters.SetPermanentLiquid(id, liquid);
        }
    }

    public int SetCurrentTeamSlot(uint slot)
    {
        if (ActiveTemporaryTeam is not {} active) return Teams.SetUsingMemberSlot(slot);
        if (active.Members.All(m => m.Slot != slot)) return (int)EnmTextCode.EnmTextWrongParam;
        if (active.Slot != slot) { ActiveTemporaryTeam = active with { Slot = slot };  }
        return 0;
    }

    public TeamLiquid CurrentTeamLiquid => ActiveTemporaryTeam?.Liquid ?? Teams.CurrentTeam()?.TemporaryLiquid ?? TeamLiquid.Empty;
    private bool ApplyCurrentTeamLiquid(ElementParamMap? first, ElementParamMap? second)
    {
        if (ActiveTemporaryTeam is not {} active) return Teams.ApplyBattleLiquid(first, second);
        var next = active with { Liquid = first is null ? active.Liquid : TeamLiquid.FromProto(first), LiquidLv2 = second is null ? active.LiquidLv2 : TeamLiquid.FromProto(second) };
        if (next == active) return false;
        ActiveTemporaryTeam = next;

        return true;
    }
    private bool AddCurrentTeamLiquid(int element, int amount) => ActiveTemporaryTeam is {} active
        ? ApplyCurrentTeamLiquid(active.Liquid.Add(element, amount).ToProto(), null) : Teams.AddTemporaryLiquid(element, amount);
    public SCTemporaryLiquidPoolNtf CurrentLiquidNotification() => ActiveTemporaryTeam is {} active
        ? new() { TeamType = (int)active.Type, TeamSrc = active.Source, TemporaryLiquid = active.Liquid.ToProto() }
        : Teams.TemporaryLiquidNotification();

    public void LoadTemporaryTeams(IEnumerable<TemporaryTeamSelection> selections, ActiveTemporaryTeam? active, ActiveTemporaryTeam? suspended = null)
    {
        IReadOnlyList<TeamMemberState> NormalizeMembers(IReadOnlyList<TeamMemberState> members) => members
            .Select(m => m with { Gems = m.Gems.Take(assets.Gems.MaxPerCharacter)
                .Select(id => assets.Gems.Exists(id) ? id : 0).ToArray() }).ToArray();

        _temporarySelections.Clear();
        ActiveTemporaryTeam = null;
        foreach (var selection in selections)
            if (selection.Type is EnmTmpTeamType.Dungeon or EnmTmpTeamType.Wanted && ValidTemporarySource(selection.Type, selection.Source)
                && ValidOwnedMembers(TemporaryTeamData(selection.Source, selection.Members).MemberData))
                _temporarySelections[(selection.Type, selection.Source)] = selection with { Members = NormalizeMembers(selection.Members) };
        SuspendedStoryTeam = null;
        foreach (var candidate in new[] { suspended, active })
        {
            ActiveTemporaryTeam = null;
            if (candidate is null || !ValidTemporarySource(candidate.Type, candidate.Source)) continue;
            var wire = TemporaryTeamData(candidate.Source, candidate.Members);
            var valid = candidate.Type == EnmTmpTeamType.Task
                ? candidate.Members.Count is > 0 and <= TeamManager.MaxMembers
                    && candidate.Members.All(m => m.Slot is >= 1 and <= TeamManager.MaxMembers && m.InstId != 0 && !Characters.Owns(m.InstId)
                        && candidate.Trials.Count(t => t.InstanceId == m.InstId && assets.TmpTeams.MembersOf(assets.TmpTeams.GetBySrc(candidate.Source)!).Any(r => r.Id == t.TemplateId && r.CharacterId == m.CharacterId)) == 1)
                    && candidate.Members.Select(m => m.InstId).Distinct().Count() == candidate.Members.Count
                    && candidate.Members.Select(m => m.Slot).Distinct().Count() == candidate.Members.Count
                    && candidate.Members.Select(m => m.CharacterId).Distinct().Count() == candidate.Members.Count
                    && candidate.Trials.Count == candidate.Members.Count
                : candidate.Trials.Count == 0 && ValidOwnedMembers(wire.MemberData);
            if (valid)
            {
                ActiveTemporaryTeam = candidate with { Members = NormalizeMembers(candidate.Members),
                    Slot = candidate.Members.Any(m => m.Slot == candidate.Slot) ? candidate.Slot : candidate.Members.Min(m => m.Slot),
                    Liquid = candidate.Liquid.Normalized(), LiquidLv2 = candidate.LiquidLv2.Normalized(),
                    Runs = candidate.Type == EnmTmpTeamType.Dungeon
                        ? candidate.Runs?.Where(r => candidate.Members.Any(m => m.InstId == r.InstanceId) && Characters.Owns(r.InstanceId))
                            .Select(r => r with { Hp = Math.Clamp(r.Hp, 0, Characters.MaxHp(r.InstanceId)),
                                Liquid = Math.Clamp(r.Liquid, 0, Characters.PermanentLiquidMax(r.InstanceId)) }).ToArray()
                        : null };
                foreach (var trial in candidate.Trials) SetTeamCharacterVitals(trial.InstanceId, Math.Max(0, trial.Hp), Math.Max(0, trial.Liquid));
                foreach (var trial in candidate.Trials) Guid.Adopt(trial.InstanceId);
                if (ReferenceEquals(candidate, suspended) && candidate.Type == EnmTmpTeamType.Task) SuspendedStoryTeam = ActiveTemporaryTeam;
            }
        }

    }
}
