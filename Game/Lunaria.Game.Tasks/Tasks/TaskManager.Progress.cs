using Lunaria.Game.Logging;
using Lunaria.Game.Resources.Tables;
using Msg;

namespace Lunaria.Game.Tasks;

public sealed record TaskProgressResult(
    uint TaskType,
    uint TaskId,
    bool Recorded,
    uint Progress,
    uint MaxProgress,
    bool StepAdvanced,
    bool TaskCompleted,
    IReadOnlyList<uint> StartedTasks,
    IReadOnlyList<ulong> PassedSteps
)
{
    public bool TaskFailed { get; init; }

    // Keep every step transition because Lua accepts only the immediate next step.
    public TaskData? UpdatedData { get; init; }
    public IReadOnlyList<TaskData> StartedTaskData { get; init; } = [];
    public IReadOnlyList<TaskAction> SettledActions { get; init; } = [];
}

public sealed partial class TaskManager
{
    public (int Code, TaskProgressResult? Result) ReportAction(
        uint taskType,
        ulong actionId,
        uint progress,
        ulong? currentMapId = null,
        Func<PTaskActionsTyped, uint?>? evaluateTarget = null
    )
    {
        var action = assets.Tasks.Action(taskType, actionId);

        if (action is null)
        {
            Log.Flag("task action report for {TaskType} {ActionId} has no such action", taskType, actionId);
            return ((int)EnmTextCode.EnmTextActionIdNotFound, null);
        }

        // Reused actions can have stale originStep values. Prefer their current occurrence.
        var stepId = _processing.Values.FirstOrDefault(s => s.Type == taskType
                                                            && assets.Tasks.Actions(taskType, s.CurrentStep.StepId).Contains(actionId))
                         ?.CurrentStep.StepId
                     ?? assets.Tasks.StepOfAction(taskType, actionId);
        var taskId = assets.Tasks.TaskOfStep(taskType, stepId);

        if (taskId == 0 || !assets.Tasks.TaskExists(taskType, taskId))
        {
            Log.Flag("task action {TaskType} {ActionId} maps to no task, step {StepId}", taskType, actionId, stepId);
            return ((int)EnmTextCode.EnmTextTaskIdNotFound, null);
        }

        if (_finished.Contains((taskType, taskId)))
        {
            Log.Event("acknowledged progress on already finished task {TaskType} {TaskId}", taskType, taskId);
            return (0, Acknowledge(taskType, taskId, Maximum(taskType, actionId), Maximum(taskType, actionId)));
        }

        if (!_processing.TryGetValue((taskType, taskId), out var state))
        {
            Log.Flag("task action report for {TaskType} {TaskId} has no processing state", taskType, taskId);
            return ((int)EnmTextCode.EnmTextTaskNotProcessing, null);
        }

        // Cleanup actions must not skip ahead or complete a task.
        if (assets.Tasks.IsPostAction(taskType, stepId, actionId))
        {
            if (IsStepAhead(taskType, taskId, state.CurrentStep.StepId, stepId))
                return ((int)EnmTextCode.EnmTextActionNotProcessing, null);

            return (0, Acknowledge(taskType, taskId, Math.Min(progress, val2: 1), max: 1));
        }

        var failure = assets.Tasks.IsFailureAction(taskType, stepId, actionId);

        if (IsStepAhead(taskType, taskId, stepId, state.CurrentStep.StepId))
            return failure ? ((int)EnmTextCode.EnmTextActionNotProcessing, null) : (0, Acknowledge(taskType, taskId, progress: 1, max: 1));

        // A progress query does not prove the client completed earlier steps.
        if (progress == 0)
        {
            var current = state.CurrentStep.StepId == stepId ? state.CurrentStep.Actions.GetValueOrDefault(actionId) : null;
            return (0, Acknowledge(taskType, taskId, current?.Progress ?? 0, Maximum(taskType, actionId)));
        }

        if (!failure && evaluateTarget is null && action.ServerTargetType == ArriveMapTarget
            && (currentMapId is not {} mapId || !MatchesArrival(action.ServerParam1, mapId)))
            return ((int)EnmTextCode.EnmTextActionNotProcessing, null);

        var advanced = false;
        var recorded = false;
        var passed = new List<ulong>();

        if (state.CurrentStep.StepId != stepId)
        {
            if (!IsStepAhead(taskType, taskId, state.CurrentStep.StepId, stepId))
                return ((int)EnmTextCode.EnmTextActionNotProcessing, null);

            while (state.CurrentStep.StepId != stepId)
            {
                if (!SavedGateHolds(taskType, state.CurrentStep.StepId, state.CurrentStep.Actions))
                    return ((int)EnmTextCode.EnmTextActionNotProcessing, null);

                var following = assets.Tasks.NextStep(taskType, taskId, state.CurrentStep.StepId);

                if (following == 0)
                    return ((int)EnmTextCode.EnmTextActionNotProcessing, null);

                passed.Add(state.CurrentStep.StepId);
                state = state with { CurrentStep = EnteredStep(taskType, following, state.CurrentStep) };
                advanced = true;
            }

            recorded = true;
        }

        // Commit only after every intervening gate passes. A blocked gate must leave state unchanged.
        if (failure)
        {
            if (action.ServerTargetType != 0 && evaluateTarget?.Invoke(action) is not > 0)
                return ((int)EnmTextCode.EnmTextActionNotProcessing, null);

            return (0, FailStep(state));
        }

        // Client reports activate server targets but cannot set their progress. Validate the full seek before running
        // commands.
        if (action.ServerTargetType != 0)
        {
            var current = state.CurrentStep.Actions.GetValueOrDefault(actionId);

            if (current?.IsComplete == true)
                return (0, Acknowledge(taskType, taskId, current.Progress, current.MaxProgress));

            if (IsCommand(action.ServerTargetType) && _reportedTargets.Add((taskType, actionId)))
                Dirty();

            var evaluated = evaluateTarget?.Invoke(action)
                            ?? (evaluateTarget is null && action.ServerTargetType == ArriveMapTarget ? 1u : null);

            if (evaluated is null || evaluated == 0)
            {
                if (advanced)
                {
                    _processing[(taskType, taskId)] = state;
                    Dirty();
                }

                return (0, Snapshot(new TaskProgressResult(taskType, taskId, advanced,
                    current?.Progress ?? 0, Maximum(taskType, actionId), advanced, TaskCompleted: false, [], passed)));
            }
            progress = evaluated.Value;
        }

        var actions = new SortedDictionary<ulong, TaskActionState>(state.CurrentStep.Actions);

        foreach (var tracked in assets.Tasks.ServerActions(taskType, stepId))
        {
            actions.TryAdd(tracked, new TaskActionState(Progress: 0, Maximum(taskType, tracked)));
        }

        var echoProgress = Math.Min(progress, val2: 1);
        uint echoMax = 1;

        if (assets.Tasks.IsServerSaved(taskType, actionId) || action.Necessary)
        {
            actions.TryGetValue(actionId, out var current);
            var max = Maximum(taskType, actionId);
            var next = Math.Min(Math.Max(current?.Progress ?? 0, progress), max);
            echoProgress = next;
            echoMax = max;

            if (current is null || next != current.Progress)
            {
                actions[actionId] = new TaskActionState(next, max);
                state = state with { CurrentStep = state.CurrentStep with { Actions = actions } };
                recorded = true;
            }
        }

        if (IsStepComplete(taskType, stepId, actions))
        {
            Log.Event("task {TaskType} {TaskId} step {StepId} complete, advancing", taskType, taskId, stepId);
            return (0, AdvancePast(taskType, taskId, state, stepId, echoProgress, echoMax, passed));
        }

        if (advanced || recorded)
        {
            _processing[(taskType, taskId)] = state;
            Dirty();
        }

        return (0, Snapshot(new TaskProgressResult(
            taskType, taskId, recorded, echoProgress, echoMax, advanced, TaskCompleted: false, [], passed)));
    }

    private TaskProgressResult AdvancePast(
        uint taskType,
        uint taskId,
        TaskState state,
        ulong stepId,
        uint echoProgress,
        uint echoMax,
        List<ulong> passed
    )
    {
        var following = assets.Tasks.NextStep(taskType, taskId, stepId);
        passed.Add(stepId);

        if (following == 0)
        {
            _processing.Remove((taskType, taskId));

            if (_finished.Count >= MaxFinished && _finished.Min is {} oldest)
                _finished.Remove(oldest);
            _finished.Add((taskType, taskId));
            Dirty();

            var started = new List<uint>();

            foreach (var followingTask in assets.Tasks.NextTasks(taskType, taskId))
            {
                if (TryStart(taskType, followingTask))
                    started.Add(followingTask);
            }

            Log.State("task {TaskType} {TaskId} completed, started {StartedCount} follow up tasks", taskType, taskId, started.Count);
            Log.Event("task {TaskType} {TaskId} completed at step {StepId}, started tasks {StartedTasks}",
                taskType, taskId, stepId, started);
            return Snapshot(new TaskProgressResult(
                taskType, taskId, Recorded: true, echoProgress, echoMax, StepAdvanced: true, TaskCompleted: true, started, passed));
        }

        _processing[(taskType, taskId)] = state with { CurrentStep = EnteredStep(taskType, following, state.CurrentStep) };
        Dirty();

        return Snapshot(new TaskProgressResult(
            taskType, taskId, Recorded: true, echoProgress, echoMax, StepAdvanced: true, TaskCompleted: false, [], passed));
    }

    private bool SavedGateHolds(uint type, ulong stepId, IReadOnlyDictionary<ulong, TaskActionState> actions)
    {
        foreach (var action in assets.Tasks.ServerActions(type, stepId))
        {
            if (!assets.Tasks.IsNecessary(type, action))
                continue;

            if (!actions.TryGetValue(action, out var state) || !state.IsComplete)
                return false;
        }

        return true;
    }

    private bool IsStepComplete(uint type, ulong stepId, IReadOnlyDictionary<ulong, TaskActionState> actions)
    {
        foreach (var action in assets.Tasks.Actions(type, stepId))
        {
            if (!assets.Tasks.IsNecessary(type, action))
                continue;

            if (!actions.TryGetValue(action, out var state) || !state.IsComplete)
                return false;
        }

        return true;
    }

    private bool IsStepAhead(uint type, uint taskId, ulong fromStep, ulong stepId)
    {
        var from = assets.Tasks.StepIndex(type, taskId, fromStep);
        return from >= 0 && assets.Tasks.StepIndex(type, taskId, stepId) > from;
    }

    private static TaskProgressResult Acknowledge(uint type, uint taskId, uint progress, uint max) =>
        new(type, taskId, Recorded: false, progress, max, StepAdvanced: false, TaskCompleted: false, [], []);

    private TaskProgressResult Snapshot(TaskProgressResult result) => result with {
        UpdatedData = TaskDataOf(result.TaskType, result.TaskId),
        StartedTaskData = result.StartedTasks
            .Select(id => TaskDataOf(result.TaskType, id)).OfType<TaskData>().ToList()
    };
}
