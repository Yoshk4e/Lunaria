using System.Collections.Frozen;
using System.Globalization;
using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

public enum GlobalEventSub : uint
{
    RoleLvUp = 1,
    WorldLv = 2,
    PlayerLv = 3,
    Task = 4,
    TaskStep = 5,
    AddItemType = 6,
    AddMotiveType = 7,
    AddMotiveRare = 8,
    AddHouse = 9,
    DungeonCount = 10,
    PassDay = 11,
    StaminaCost = 22,
    DungeonTypeCount = 23,
    CollectItemType = 25,
    UseItemType = 26,
    TeleportPoint = 27,
    WantedFinish = 28,
    AddHouseAny = 29,
    SubRegionProgress = 30,
    PlayerLogin = 21
}

public enum UnlockCombine : uint
{
    And = 1,
    Or = 2
}

public sealed class UnlockAssets
{
    private readonly FrozenDictionary<(uint SubType, ulong Argument), uint[]> _byArgument;
    private readonly FrozenDictionary<uint, uint[]> _bySubType;
    private readonly FrozenDictionary<uint, PUnlockConditionTable> _conditions;
    private readonly FrozenDictionary<uint, uint[]> _conditionsBySystem;
    private readonly FrozenDictionary<uint, uint> _defaultUnlock;
    private readonly FrozenDictionary<uint, PGlobalEventFinishTable> _events;
    private readonly FrozenDictionary<uint, uint[]> _withoutArguments;

    public UnlockAssets(
        IReadOnlyDictionary<string, PUnlockConditionTable> conditions,
        IReadOnlyDictionary<string, PUnlockFeatureTable> features,
        IReadOnlyDictionary<string, CSystemIDTable> systems,
        IReadOnlyDictionary<string, PGlobalEventFinishTable> events
    )
    {
        var byId = new Dictionary<uint, PUnlockConditionTable>();

        foreach (var row in conditions.Values)
        {
            byId[row.Id] = row;
        }

        if (byId.Count == 0)
            throw new ResourceException("P_UnlockConditionTable.json", "p_unlockconditiontable has no rows");

        var eventById = new Dictionary<uint, PGlobalEventFinishTable>();

        foreach (var row in events.Values)
        {
            eventById[row.Id] = row;
        }

        foreach (var row in byId.Values)
        foreach (var eventId in row.FinishEvents)
        {
            if (!eventById.ContainsKey(eventId))
                throw new ResourceException(
                    "P_GlobalEventFinishTable.json",
                    $"p_unlockconditiontable {row.Id} references invalid event {eventId}");
        }

        var conditionsBySystem = new Dictionary<uint, List<uint>>();

        foreach (var row in features.Values)
        {
            if (!byId.ContainsKey(row.UnlockConditionId))
                throw new ResourceException(
                    "P_UnlockConditionTable.json",
                    $"p_unlockfeaturetable {row.Id} references invalid condition {row.UnlockConditionId}");

            if (!conditionsBySystem.TryGetValue(row.ClientSystemType, out var gated))
            {
                gated = [];
                conditionsBySystem[row.ClientSystemType] = gated;
            }

            gated.Add(row.UnlockConditionId);
        }

        var defaultUnlock = new Dictionary<uint, uint>();

        foreach (var row in systems.Values)
        {
            defaultUnlock[row.SystemFunctionType] = row.DefaultUnlock;
        }

        if (defaultUnlock.Count == 0)
            throw new ResourceException("C_SystemIDTable.json", "c_systemidtable has no rows");

        _conditions = byId.ToFrozenDictionary();
        _events = eventById.ToFrozenDictionary();

        _bySubType = eventById.Values.GroupBy(row => row.SubType)
            .ToFrozenDictionary(group => group.Key, group => group.Select(row => row.Id).Order().ToArray());

        _withoutArguments = eventById.Values.Where(row => row.Args.Count == 0).GroupBy(row => row.SubType)
            .ToFrozenDictionary(group => group.Key, group => group.Select(row => row.Id).Order().ToArray());

        _byArgument = eventById.Values.SelectMany(row => row.Args
                .Select(ParseArgument).OfType<ulong>().Distinct()
                .Select(arg => (Key: (row.SubType, arg), row.Id)))
            .GroupBy(entry => entry.Key)
            .ToFrozenDictionary(group => group.Key, group => group.Select(entry => entry.Id).Order().ToArray());

        _conditionsBySystem = conditionsBySystem.ToFrozenDictionary(
            kvp => kvp.Key, kvp => kvp.Value.OrderBy(id => id).ToArray());
        _defaultUnlock = defaultUnlock.ToFrozenDictionary();
    }

    public bool IsUnlocked(uint systemType, Func<uint, bool> isFinished)
    {
        if (!_conditionsBySystem.TryGetValue(systemType, out var conditionIds) || conditionIds.Length == 0)
            return _defaultUnlock.TryGetValue(systemType, out var fallback) && fallback == 1;

        return conditionIds.All(id => ConditionMet(_conditions[id], isFinished));
    }

    public IReadOnlyList<uint> UnlockedSystems(Func<uint, bool> isFinished) =>
        _defaultUnlock.Keys
            .Concat(_conditionsBySystem.Keys)
            .Distinct()
            .Where(system => IsUnlocked(system, isFinished))
            .OrderBy(system => system)
            .ToList();

    public IReadOnlyList<uint> EventsOfSubType(GlobalEventSub subType) =>
        _bySubType.GetValueOrDefault((uint)subType) ?? [];

    public IReadOnlyList<uint> StepEvents(ulong stepId) => ArgEvents(GlobalEventSub.TaskStep, stepId);
    public IReadOnlyList<uint> TaskEvents(uint taskId) => ArgEvents(GlobalEventSub.Task, taskId);
    public IReadOnlyList<uint> LoginEvents() => EventsOfSubType(GlobalEventSub.PlayerLogin);

    public IReadOnlyList<uint> ArgEvents(GlobalEventSub subType, ulong id) =>
        _byArgument.GetValueOrDefault(((uint)subType, id)) ?? [];

    public IReadOnlyList<uint> CountEvents(GlobalEventSub subType) =>
        _withoutArguments.GetValueOrDefault((uint)subType) ?? [];

    public IReadOnlyList<uint> MotiveTypeEvents(uint identity) => ArgEvents(GlobalEventSub.AddMotiveType, identity);
    public IReadOnlyList<uint> MotiveRareEvents(uint rarity) => ArgEvents(GlobalEventSub.AddMotiveRare, rarity);

    public ulong? FirstNumericArg(uint eventId) =>
        _events.GetValueOrDefault(eventId)?.Args.Select(ParseArgument).FirstOrDefault(value => value is > 0);

    public bool IsClientReportable(uint eventId) =>
        _events.GetValueOrDefault(eventId)?.SubType is 12 or 15 or 16;

    private static ulong? ParseArgument(string argument) =>
        decimal.TryParse(argument, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
        && number >= 0 && number <= ulong.MaxValue && number == decimal.Truncate(number) ?
            (ulong)number :
            null;

    public bool IsConditionMet(uint id, Func<uint, bool> isFinished) =>
        id == 0 || _conditions.TryGetValue(id, out var condition) && ConditionMet(condition, isFinished);

    private static bool ConditionMet(PUnlockConditionTable condition, Func<uint, bool> isFinished) =>
        condition.UnlockType == (uint)UnlockCombine.Or ?
            condition.FinishEvents.Any(isFinished) :
            condition.FinishEvents.All(isFinished);
}
