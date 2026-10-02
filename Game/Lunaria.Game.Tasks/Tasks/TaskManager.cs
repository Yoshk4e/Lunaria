using Lunaria.Game.Logging;
using Lunaria.Game.Resources;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.Game.Tasks;

public sealed partial class TaskManager(GameData assets)
{
    private static readonly ILogger Log = GameLog.Create("Lunaria.Game.Tasks");

    /// <summary>Open wanted tasks with their run and daily tasks with the daily reset, not at role creation.</summary>
    private static readonly uint[] SeededNamespaces = [TaskAssets.QuestMain, TaskAssets.POIQuest];
    private readonly SortedSet<(uint Type, uint Id)> _finished = [];

    private readonly SortedDictionary<(uint Type, uint Id), TaskState> _processing = [];
    public static int MaxProcessing => (int)EnmSizeLimit.MaxProcessingTasksLen;

    public static int MaxFinished => (int)EnmSizeLimit.MaxCompletedTasksLen;

    public bool IsDirty { get; private set; }

    public IReadOnlyDictionary<(uint Type, uint Id), TaskState> Processing => _processing;

    public IReadOnlyCollection<(uint Type, uint Id)> Finished => _finished;

    public int ProcessingCount => _processing.Count;

    public int FinishedCount => _finished.Count;

    public bool IsEmpty => _processing.Count == 0 && _finished.Count == 0;

    public void Load(
        IEnumerable<(uint Type, uint Task, ulong Step, IEnumerable<(ulong Action, uint Progress, uint Max)> Actions)> persisted,
        IEnumerable<(uint Type, uint Task)> finished
    )
    {
        _processing.Clear();
        _finished.Clear();
        _appliedEffects.Clear();
        _reportedTargets.Clear();

        foreach (var row in persisted)
        {
            if (!assets.Tasks.TaskExists(row.Type, row.Task))
                continue;

            if (_processing.ContainsKey((row.Type, row.Task)))
                continue;

            if (_processing.Count >= MaxProcessing)
                break;

            var step = row.Step != 0
                       && assets.Tasks.StepExists(row.Type, row.Step)
                       && assets.Tasks.TaskOfStep(row.Type, row.Step) == row.Task ?
                row.Step :
                assets.Tasks.FirstStep(row.Type, row.Task);

            if (step == 0)
                continue;

            _processing[(row.Type, row.Task)] = new TaskState(row.Type, row.Task, AdoptStep(row.Type, step, row.Actions));
            Log.Event("restored task {TaskType} {TaskId} at step {StepId}", row.Type, row.Task, step);
        }

        foreach (var (type, task) in finished)
        {
            if (!assets.Tasks.TaskExists(type, task))
                continue;

            if (_processing.ContainsKey((type, task)))
                continue;

            if (_finished.Count >= MaxFinished)
                break;

            _finished.Add((type, task));
        }

        IsDirty = false;
    }

    private TaskStepState AdoptStep(uint type, ulong step, IEnumerable<(ulong Action, uint Progress, uint Max)> actions)
    {
        var adopted = new SortedDictionary<ulong, TaskActionState>();

        foreach (var (action, progress, max) in actions)
        {
            if (!assets.Tasks.IsServerSaved(type, action))
                continue;

            if (!assets.Tasks.Actions(type, step).Contains(action))
                continue;

            var floor = Maximum(type, action);
            adopted[action] = new TaskActionState(Math.Min(progress, floor), floor);
        }

        foreach (var tracked in assets.Tasks.ServerActions(type, step))
        {
            adopted.TryAdd(tracked, new TaskActionState(Progress: 0, Maximum(type, tracked)));
        }

        return new TaskStepState(step, adopted);
    }

    public IReadOnlyList<(uint Type, uint Id)> EnsureStarted()
    {
        if (_processing.Count > 0 || _finished.Count > 0)
            return [];

        var started = new List<(uint Type, uint Id)>();
        // StartTask destinations must wait for that command, even when no nextTasks link points to them.
        var scriptStarted = new HashSet<(uint Type, uint Id)>();
        foreach (var action in assets.Tasks.ServerTargetActions((int)ServerTarget.StartTask))
        {
            if (!TargetParameter.TryId(action.ServerParam1, out var id) || id is 0 or > uint.MaxValue) continue;
            var type = TargetParameter.TryCount(action.ServerParam2, out var configured) && configured != 0
                ? configured : TaskAssets.QuestMain;
            scriptStarted.Add((type, (uint)id));
        }

        foreach (var type in SeededNamespaces)
        foreach (var task in assets.Tasks.StartingTasksOf(type))
        {
            if (scriptStarted.Contains((type, task))) continue;
            if (_processing.Count >= MaxProcessing)
                return started;

            if (TryStart(type, task))
                started.Add((type, task));
        }

        return started;
    }

    public bool IsProcessing(uint type, uint taskId) => _processing.ContainsKey((type, taskId));

    public bool IsFinished(uint type, uint taskId) => _finished.Contains((type, taskId));

    public void ClearDirty() => IsDirty = false;

    public TaskData? TaskDataOf(uint type, uint taskId)
    {
        if (!assets.Tasks.TaskExists(type, taskId))
            return null;

        if (_processing.TryGetValue((type, taskId), out var state))
            return ToTaskData(state);

        if (_finished.Contains((type, taskId)))
            return ToFinishedTaskData(type, taskId);

        return null;
    }

    public PlayerTaskData ToPlayerTaskData()
    {
        var data = new PlayerTaskData();
        data.ProcessingTasks.AddRange(_processing.Values.Select(ToTaskData));
        data.FinishedTasks.AddRange(_finished.Select(row => ToFinishedTaskData(row.Type, row.Id)));
        return data;
    }

    /// <summary>
    /// Send completed tasks first, then open tasks in a second list. Open tasks in the first reply block client
    /// initialization.
    /// </summary>
    public PlayerTaskData ToBootstrapPlayerTaskData()
    {
        var data = new PlayerTaskData();
        data.FinishedTasks.AddRange(_finished.Select(row => ToFinishedTaskData(row.Type, row.Id)));
        return data;
    }

    private bool TryStart(uint type, uint taskId)
    {
        if (!assets.Tasks.TaskExists(type, taskId))
            return false;

        if (_processing.ContainsKey((type, taskId)) || _finished.Contains((type, taskId)))
            return false;

        if (_processing.Count >= MaxProcessing)
        {
            Log.Flag("task {TaskType} {TaskId} refused because the processing list is full at {Count}", type, taskId, _processing.Count);
            return false;
        }

        var first = assets.Tasks.FirstStep(type, taskId);

        if (first == 0)
            return false;

        _processing[(type, taskId)] = new TaskState(type, taskId, FreshStep(type, first));
        Log.Event("started task {TaskType} {TaskId} at step {StepId}", type, taskId, first);
        Dirty();
        return true;
    }

    private TaskStepState FreshStep(uint type, ulong stepId)
    {
        var actions = new SortedDictionary<ulong, TaskActionState>();

        foreach (var action in assets.Tasks.ServerActions(type, stepId))
        {
            actions[action] = new TaskActionState(Progress: 0, Maximum(type, action));
        }
        return new TaskStepState(stepId, actions);
    }

    private TaskStepState EnteredStep(uint type, ulong stepId, TaskStepState previous)
    {
        var actions = new SortedDictionary<ulong, TaskActionState>();

        foreach (var action in assets.Tasks.ServerActions(type, stepId))
        {
            actions[action] = previous.Actions.GetValueOrDefault(action) is {} carried ?
                carried :
                new TaskActionState(Progress: 0, Maximum(type, action));
        }
        return new TaskStepState(stepId, actions);
    }

    private TaskData ToTaskData(TaskState state)
    {
        var data = new TaskData {
            TaskId = state.TaskId,
            TaskType = state.Type
        };
        var step = new TaskStep { StepId = state.CurrentStep.StepId };

        foreach (var (actionId, progress) in state.CurrentStep.Actions)
        {
            step.Actions.Add(new TaskAction {
                ActionId = actionId,
                Progress = progress.Progress,
                MaxProgress = progress.MaxProgress
            });
        }
        data.CurrentStep = step;
        return data;
    }

    private TaskData ToFinishedTaskData(uint type, uint taskId)
    {
        var data = new TaskData {
            TaskId = taskId,
            TaskType = type
        };
        var steps = assets.Tasks.Steps(type, taskId);

        if (steps.Count == 0)
            return data;

        var last = steps[steps.Count - 1];
        var step = new TaskStep { StepId = last };

        foreach (var action in assets.Tasks.ServerActions(type, last))
        {
            step.Actions.Add(new TaskAction
                { ActionId = action, Progress = Maximum(type, action), MaxProgress = Maximum(type, action) });
        }
        data.CurrentStep = step;
        return data;
    }

    private void Dirty() => IsDirty = true;
}
