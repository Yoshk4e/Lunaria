using Lunaria.Common.Tracking;
using Lunaria.Game.Resources.Tables;
using Msg;

namespace Lunaria.Game.Tasks;

public sealed partial class TaskManager : TrackedObject
{
    public const int ArriveMapTarget = (int)ServerTarget.ArriveMap;

    /// <summary>Client target 13 (EmptyAction) completes as soon as its step becomes current.</summary>
    public const int EmptyActionTarget = 13;

    // Do not repeat grant commands when a failed step rolls back.
    private readonly TrackedSet<(uint Type, ulong Action)> __tracked_appliedEffects = [];
    [Tracked]
    private partial TrackedSet<(uint Type, ulong Action)> _appliedEffects { get; }
    private readonly TrackedSet<(uint Type, ulong Action)> __tracked_reportedTargets = [];
    [Tracked]
    private partial TrackedSet<(uint Type, ulong Action)> _reportedTargets { get; }
    public IReadOnlyCollection<(uint Type, ulong Action)> AppliedEffects => _appliedEffects;
    public IReadOnlyCollection<(uint Type, ulong Action)> ReportedTargets => _reportedTargets;
    public bool HasAppliedEffect(uint type, ulong action) => _appliedEffects.Contains((type, action));

    public void MarkEffectApplied(uint type, ulong action)
    {
        _appliedEffects.Add((type, action));
    }

    public void LoadAppliedEffects(IEnumerable<(uint Type, ulong Action)> effects)
    {
        _appliedEffects.Clear();

        foreach (var effect in effects)
        {
            if (assets.Tasks.Action(effect.Type, effect.Action) is { ServerTargetType: not 0 })
                _appliedEffects.Add(effect);
        }
    }

    public void LoadReportedTargets(IEnumerable<(uint Type, ulong Action)> reported)
    {
        _reportedTargets.Clear();

        foreach (var entry in reported)
        {
            if (assets.Tasks.Action(entry.Type, entry.Action) is { ServerTargetType: not 0 })
                _reportedTargets.Add(entry);
        }
    }

    public TaskData? StartTask(uint type, uint taskId) =>
        TryStart(type, taskId) ? TaskDataOf(type, taskId) : null;

    public void ResetNamespace(uint type)
    {
        foreach (var key in _processing.Keys.Where(key => key.Type == type).ToArray())
        {
            _processing.Remove(key);
        }
        _finished.RemoveWhere(key => key.Type == type);
        _appliedEffects.RemoveWhere(key => key.Type == type);
        _reportedTargets.RemoveWhere(key => key.Type == type);

    }

    public uint Maximum(uint type, ulong actionId)
    {
        var action = assets.Tasks.Action(type, actionId);
        if (action is null) return 1;

        var parameter = (ServerTarget)action.ServerTargetType switch {
            ServerTarget.OwnHouse => action.ServerParam1,
            ServerTarget.BuyItem or ServerTarget.OwnItem or ServerTarget.CompleteDungeon
                or ServerTarget.CompleteBattle => action.ServerParam2,
            _ => "1"
        };
        return TargetParameter.TryCount(parameter, out var count) && count > 0 ? count : 1;
    }

    /// <summary>
    /// Yield each transition so the next sees its rewards and case changes.
    /// Client commands still need an activation report.
    /// </summary>
    public IEnumerable<TaskProgressResult> EvaluateServerTargets(Func<PTaskActionsTyped, uint?> evaluate)
    {
        var budget = assets.Tasks.ActionCount + assets.Tasks.TaskCount;
        var failed = new HashSet<(uint Type, uint TaskId, ulong StepId)>();

        while (budget-- > 0)
        {
            TaskProgressResult? changed = null;

            foreach (var state in _processing.Values.ToArray())
            {
                // A successful exit also triggers the map failure guard. Check subquest completion first.
                var normal = assets.Tasks.Actions(state.Type, state.CurrentStep.StepId);
                foreach (var id in normal)
                {
                    var action = assets.Tasks.Action(state.Type, id)!;
                    if (action.ServerTargetType != (int)ServerTarget.CompleteTask
                        || state.CurrentStep.Actions.GetValueOrDefault(id)?.IsComplete == true
                        || normal.Any(other => other != id && assets.Tasks.IsNecessary(state.Type, other)
                            && state.CurrentStep.Actions.GetValueOrDefault(other)?.IsComplete != true))
                        continue;

                    if (evaluate(action) is > 0)
                        changed = SettleServerAction(state.Type, state.TaskId, id, Maximum(state.Type, id));
                    if (changed is not null) break;
                }
                if (changed is not null) break;

                foreach (var id in assets.Tasks.FailureActions(state.Type, state.CurrentStep.StepId))
                {
                    var action = assets.Tasks.Action(state.Type, id);

                    if (action is not { ServerTargetType: (int)ServerTarget.MapState }
                        || failed.Contains((state.Type, state.TaskId, state.CurrentStep.StepId))
                        || evaluate(action) is not > 0) continue;

                    failed.Add((state.Type, state.TaskId, state.CurrentStep.StepId));
                    changed = FailStep(state);
                    break;
                }
                if (changed is not null) break;

                foreach (var id in assets.Tasks.Actions(state.Type, state.CurrentStep.StepId))
                {
                    var action = assets.Tasks.Action(state.Type, id)!;

                    if (action.ServerTargetType == 0
                        || state.CurrentStep.Actions.GetValueOrDefault(id)?.IsComplete == true
                        || IsCommand(action.ServerTargetType) && action.TargetType != 0
                                                              && !_reportedTargets.Contains((state.Type, id)))
                        continue;

                    if (evaluate(action) is {} progress)
                        changed = SettleServerAction(state.Type, state.TaskId, id, progress);
                    if (changed is not null) break;
                }
                if (changed is not null) break;
            }
            if (changed is null) yield break;

            yield return changed;
        }
    }

    private TaskProgressResult FailStep(TaskState state)
    {
        var rollback = assets.Tasks.RollbackStep(state.Type, state.CurrentStep.StepId);
        _processing[(state.Type, state.TaskId)] = state with { CurrentStep = FreshStep(state.Type, rollback) };

        foreach (var step in assets.Tasks.Steps(state.Type, state.TaskId))
        foreach (var action in assets.Tasks.Actions(state.Type, step))
        {
            _reportedTargets.Remove((state.Type, action));
        }

        return Snapshot(new TaskProgressResult(state.Type, state.TaskId, Recorded: true, Progress: 1, MaxProgress: 1, StepAdvanced: false,
                TaskCompleted: false, [], [])
            { TaskFailed = true });
    }

    public static bool IsCommand(int type) => (ServerTarget)type is
        ServerTarget.GiveItems or ServerTarget.TakeItems or ServerTarget.OpenCase
        or ServerTarget.GiveClue or ServerTarget.GiveEvidence or ServerTarget.DecryptEvidence
        or ServerTarget.StartTask or ServerTarget.ResetMonster;

    /// <summary>
    /// Settle markers and arrivals before FinEnterMap. Later period changes remove the NPC crowd without respawning it.
    /// </summary>
    public IReadOnlyList<TaskProgressResult> OnMapEntered(ulong mapId)
    {
        var results = new List<TaskProgressResult>();

        if (mapId != 0)
        {
            List<TaskProgressResult> batch;

            do
            {
                batch = [];

                foreach (var (type, actionId) in InstantMarkersFor(mapId))
                {
                    // Repeating a completed marker only sends another acknowledgement.
                    // Yield only new progress so the batch can finish while other actions are pending.
                    if (ReportAction(type, actionId, progress: 1) is (0, { Recorded: true } or { StepAdvanced: true }) and (_, { } result))
                    {
                        results.Add(result);
                        batch.Add(result);
                    }
                }
            }
            while (batch.Count > 0);
        }

        results.AddRange(EvaluateServerTargets(action => action.ServerTargetType == ArriveMapTarget
                                                        && MatchesArrival(action.ServerParam1, mapId) ? 1u :
            null));
        return results;
    }

    private IEnumerable<(uint Type, ulong Action)> InstantMarkersFor(ulong mapId)
    {
        foreach (var key in _processing.Keys.ToArray())
        {
            var stepId = _processing[key].CurrentStep.StepId;

            foreach (var actionId in assets.Tasks.Actions(key.Type, stepId))
            {
                if (assets.Tasks.Action(key.Type, actionId) is not { TargetType: EmptyActionTarget, ServerTargetType: 0 } action)
                    continue;

                if (MatchesMap(action.MapId, mapId))
                    yield return (key.Type, actionId);
            }
        }
    }

    private static bool MatchesArrival(string parameter, ulong mapId) =>
        mapId != 0 && TargetParameter.TryId(parameter, out var target) && target == mapId;

    public static bool MatchesMap(string parameter, ulong mapId) =>
        TargetParameter.TryId(parameter, out var target) && MatchesMap(target, mapId);

    /// <summary>MapID can name either the map or its world, such as 211001001001 or 211.</summary>
    public static bool MatchesMap(ulong target, ulong mapId) =>
        mapId != 0 && target != 0 && (target == mapId || target == mapId / 1_000_000_000);

    public IReadOnlyList<TaskProgressResult> OnServerEvent(ServerTarget target, ulong id, uint count = 1)
    {
        if (count == 0) return [];

        var candidates = _processing.Values.SelectMany(state =>
                assets.Tasks.Actions(state.Type, state.CurrentStep.StepId)
                    .Select(actionId => (state.Type, state.TaskId, state.CurrentStep.StepId, Action: actionId)))
            .ToArray();
        var results = new List<TaskProgressResult>();

        foreach (var candidate in candidates)
        {
            if (!_processing.TryGetValue((candidate.Type, candidate.TaskId), out var state)
                || state.CurrentStep.StepId != candidate.StepId)
                continue;

            var action = assets.Tasks.Action(candidate.Type, candidate.Action)!;
            if (action.ServerTargetType != (int)target) continue;

            var matches = TargetParameter.TryId(action.ServerParam1, out var expected)
                          && (expected == id || target == ServerTarget.CompleteBattle && expected == 0);
            if (!matches) continue;

            var previous = state.CurrentStep.Actions.GetValueOrDefault(candidate.Action)?.Progress ?? 0;
            var next = (uint)Math.Min((ulong)previous + count, Maximum(candidate.Type, candidate.Action));

            if (SettleServerAction(candidate.Type, candidate.TaskId, candidate.Action, next) is {} result)
                results.Add(result);
        }
        return results;
    }

    public IReadOnlyList<TaskProgressResult> OnGameTimeAdvanced(uint previousMinute, uint elapsedMinutes)
    {
        if (elapsedMinutes == 0) return [];

        var candidates = _processing.Values.SelectMany(state => assets.Tasks.Actions(state.Type, state.CurrentStep.StepId)
            .Select(id => (state.Type, state.TaskId, Action: assets.Tasks.Action(state.Type, id)!))).ToArray();
        var results = new List<TaskProgressResult>();

        foreach (var (type, task, action) in candidates)
        {
            if (action.ServerTargetType != (int)ServerTarget.ReachGameTime
                || !TargetParameter.TryCount(action.ServerParam1, out var minute) || minute > 1440) continue;

            var distance = (minute % 1440 + 1440 - previousMinute % 1440) % 1440;

            if (elapsedMinutes >= distance && SettleServerAction(type, task, action.Id, progress: 1) is {} result)
                results.Add(result);
        }
        return results;
    }

    private TaskProgressResult? SettleServerAction(uint type, uint task, ulong action, uint progress)
    {
        if (!_processing.TryGetValue((type, task), out var state)
            || !assets.Tasks.Actions(type, state.CurrentStep.StepId).Contains(action))
            return null;

        var current = state.CurrentStep.Actions.GetValueOrDefault(action);
        var max = Maximum(type, action);
        var next = Math.Min(progress, max);
        if (next <= (current?.Progress ?? 0)) return null;

        var actions = new SortedDictionary<ulong, TaskActionState>(state.CurrentStep.Actions.ToDictionary(p => p.Key, p => p.Value)) {
            [action] = new(next, max)
        };
        state = state with { CurrentStep = state.CurrentStep with { Actions = actions } };
        _processing[(type, task)] = state;

        var result = IsStepComplete(type, state.CurrentStep.StepId, actions) ?
            AdvancePast(type, task, state, state.CurrentStep.StepId, next, max, []) :
            Snapshot(new TaskProgressResult(type, task, Recorded: true, next, max, StepAdvanced: false, TaskCompleted: false, [], []));

        return result with {
            SettledActions = [new TaskAction { ActionId = action, Progress = next, MaxProgress = max }]
        };
    }
}
