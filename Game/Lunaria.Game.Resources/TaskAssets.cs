using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

/// <summary>Look up tasks by (type, id). Roots have no incoming links in their namespace.</summary>
public sealed class TaskAssets
{
    public const uint QuestMain = 1;
    public const uint POIQuest = 2;
    public const uint Wanted = 3;
    public const uint DailyTask = 4;

    public const uint OpeningTaskId = 101004;

    private readonly Dictionary<uint, Namespace> _namespaces = [];
    private readonly Dictionary<(uint Type, ulong Step), TaskStepEnvironment> _environments = [];

    private readonly Dictionary<(uint Type, uint Id), List<ItemGrant>> _rewardItems = [];

    public TaskAssets(
        IReadOnlyDictionary<string, PTasksListQuestMain> questMainTasks,
        IReadOnlyDictionary<string, PTaskStepsQuestMain> questMainSteps,
        IReadOnlyDictionary<string, PTaskActionsQuestMain> questMainActions,
        IReadOnlyDictionary<string, CTaskTaskUITable> questMainUi,
        IReadOnlyDictionary<string, PTasksListPOIQuest> poiQuestTasks,
        IReadOnlyDictionary<string, PTaskStepsPOIQuest> poiQuestSteps,
        IReadOnlyDictionary<string, PTaskActionsPOIQuest> poiQuestActions,
        IReadOnlyDictionary<string, CTaskTaskUITablePOI> poiQuestUi,
        IReadOnlyDictionary<string, PTasksListWanted> wantedTasks,
        IReadOnlyDictionary<string, PTaskStepsWanted> wantedSteps,
        IReadOnlyDictionary<string, PTaskActionsWanted> wantedActions,
        IReadOnlyDictionary<string, PTasksListDailyTask> dailyTaskTasks,
        IReadOnlyDictionary<string, PTaskStepsDailyTask> dailyTaskSteps,
        IReadOnlyDictionary<string, PTaskActionsDailyTask> dailyTaskActions,
        ItemAssets items
    )
    {
        Add(QuestMain, "QuestMain",
            questMainTasks.Select(row => (PTasksListTyped)row.Value),
            questMainSteps.Select(row => (PTaskStepsTyped)row.Value),
            questMainActions.Select(row => (PTaskActionsTyped)row.Value));

        Add(POIQuest, "POIQuest",
            poiQuestTasks.Select(row => (PTasksListTyped)row.Value),
            poiQuestSteps.Select(row => (PTaskStepsTyped)row.Value),
            poiQuestActions.Select(row => (PTaskActionsTyped)row.Value));

        Add(Wanted, "Wanted",
            wantedTasks.Select(row => (PTasksListTyped)row.Value),
            wantedSteps.Select(row => (PTaskStepsTyped)row.Value),
            wantedActions.Select(row => (PTaskActionsTyped)row.Value));

        Add(DailyTask, "DailyTask",
            dailyTaskTasks.Select(row => (PTasksListTyped)row.Value),
            dailyTaskSteps.Select(row => (PTaskStepsTyped)row.Value),
            dailyTaskActions.Select(row => (PTaskActionsTyped)row.Value));

        if (_namespaces.Count == 0)
            throw new ResourceException("P_TasksList_QuestMain.json", "no task namespace loaded");

        foreach (var (type, ns) in _namespaces)
        foreach (var step in ns.Steps.Values)
            _environments[(type, step.Id)] = WorldTimeRules.ParseStep(step);

        AdoptRewards(QuestMain, questMainUi.Select(row => (CTaskTaskUIBase)row.Value), items);
        AdoptRewards(POIQuest, poiQuestUi.Select(row => (CTaskTaskUIBase)row.Value), items);
    }

    public IReadOnlyCollection<uint> Types => _namespaces.Keys;

    public int TaskCount => _namespaces.Values.Sum(ns => ns.Tasks.Count);
    public int StepCount => _namespaces.Values.Sum(ns => ns.Steps.Count);
    public int ActionCount => _namespaces.Values.Sum(ns => ns.Actions.Count);

    public TaskStepEnvironment EnvironmentAfterStep(uint type, ulong step) => _environments.GetValueOrDefault((type, step));

    private void AdoptRewards(uint type, IEnumerable<CTaskTaskUIBase> rows, ItemAssets items)
    {
        foreach (var row in rows)
        {
            if (row.RewardItems.Count == 0)
                continue;

            var grants = new List<ItemGrant>();

            foreach (var pair in row.RewardItems)
            {
                var parts = pair.Split(separator: ':', count: 2);

                if (parts.Length == 2
                    && uint.TryParse(parts[0].Trim(), out var itemId)
                    && uint.TryParse(parts[1].Trim(), out var count)
                    && items.Exists(itemId))
                    grants.Add(new ItemGrant(itemId, count));
            }

            if (grants.Count > 0)
                _rewardItems[(type, row.Id)] = grants;
        }
    }

    private void Add(
        uint type,
        string name,
        IEnumerable<PTasksListTyped> tasks,
        IEnumerable<PTaskStepsTyped> steps,
        IEnumerable<PTaskActionsTyped> actions
    )
    {
        var ns = new Namespace {
            Tasks = tasks.ToDictionary(row => row.Id),
            Steps = steps.ToDictionary(row => row.Id),
            Actions = actions.ToDictionary(row => row.Id),
            ServerActions = [],
            NextTasks = [],
            Roots = []
        };

        if (ns.Tasks.Count == 0)
            throw new ResourceException($"P_TasksList_{name}.json", $"the {name} task sheet has no rows");

        if (ns.Steps.Count == 0)
            throw new ResourceException($"P_TaskSteps_{name}.json", $"the {name} step sheet has no rows");

        foreach (var task in ns.Tasks.Values)
        foreach (var stepId in task.Steps)
        {
            if (!ns.Steps.ContainsKey(stepId))
                throw new ResourceException(
                    $"P_TaskSteps_{name}.json", $"task {task.Id} references missing step {stepId}");
        }

        foreach (var step in ns.Steps.Values)
        foreach (var actionId in step.Actions)
        {
            if (!ns.Actions.ContainsKey(actionId))
                throw new ResourceException(
                    $"P_TaskActions_{name}.json", $"step {step.Id} references missing action {actionId}");
        }

        foreach (var step in ns.Steps.Values)
        {
            ns.ServerActions[step.Id] = step.Actions
                .Where(id => IsSaved(ns.Actions[id]))
                .ToArray();
        }

        foreach (var task in ns.Tasks.Values)
        {
            for (var index = 0; index < task.Steps.Count; index++)
            {
                var stepId = task.Steps[index];
                ns.StepTasks.Add(stepId, task.Id);
                ns.StepIndices.Add(stepId, index);
                var step = ns.Steps[stepId];

                foreach (var actionId in step.Actions.Concat(step.FailAction).Concat(step.PostId).Concat(step.PostFailaction))
                {
                    if (!ns.Actions.TryGetValue(actionId, out var action))
                        // Some old cleanup actions are missing, such as POI action 20302. They cannot be reported.
                        continue;

                    if (!ns.ActionSteps.ContainsKey(actionId) || action.OriginStep == stepId)
                        ns.ActionSteps[actionId] = stepId;
                }
            }
        }

        foreach (var task in ns.Tasks.Values)
        {
            ns.NextTasks[task.Id] = [.. task.NextTasks.Where(id => ns.Tasks.ContainsKey(id))];
        }

        var chained = ns.Tasks.Values.SelectMany(t => t.NextTasks).ToHashSet();
        ns.Roots = ns.Tasks.Keys.Where(id => !chained.Contains(id)).Order().ToList();

        _namespaces[type] = ns;
    }

    private Namespace? Of(uint type) => _namespaces.GetValueOrDefault(type);

    public IReadOnlyList<uint> StartingTasksOf(uint type) => Of(type)?.Roots ?? [];

    public IEnumerable<PTaskActionsTyped> ServerTargetActions(int target) => _namespaces.Values
        .SelectMany(ns => ns.StepTasks.Keys.SelectMany(step => ns.Steps[step].Actions)
            .Distinct().Select(id => ns.Actions[id]))
        .Where(action => action.ServerTargetType == target);

    public bool TaskExists(uint type, uint taskId) => Of(type)?.Tasks.ContainsKey(taskId) ?? false;

    public IReadOnlyList<ulong> Steps(uint type, uint taskId) => Of(type)?.Tasks.GetValueOrDefault(taskId)?.Steps ?? [];

    public ulong FirstStep(uint type, uint taskId) => Steps(type, taskId) is [var first, ..] ? first : 0;

    public ulong NextStep(uint type, uint taskId, ulong stepId)
    {
        var steps = Steps(type, taskId);
        var index = StepIndex(type, taskId, stepId);
        return index >= 0 && index + 1 < steps.Count ? steps[index + 1] : 0;
    }

    public int StepIndex(uint type, uint taskId, ulong stepId) =>
        TaskOfStep(type, stepId) == taskId ? Of(type)?.StepIndices.GetValueOrDefault(stepId, defaultValue: -1) ?? -1 : -1;

    public IReadOnlyList<uint> NextTasks(uint type, uint taskId) => Of(type)?.NextTasks.GetValueOrDefault(taskId) ?? [];

    public uint RewardDrop(uint type, uint taskId) => Of(type)?.Tasks.GetValueOrDefault(taskId)?.RewardId ?? 0;

    /// <summary>Use the UI preview only when RewardDrop is nonzero and its bundle is missing.</summary>
    public IReadOnlyList<ItemGrant> RewardItems(uint type, uint taskId) =>
        _rewardItems.GetValueOrDefault((type, taskId)) ?? [];

    public (int X, int Y, int Z)? PositionAfterStep(uint type, ulong stepId)
    {
        var move = (Of(type)?.Steps.GetValueOrDefault(stepId)?.PostId ?? [])
            .Select(id => Action(type, id)).FirstOrDefault(action => action?.TargetType == 10);
        return move is null ? null : FVectorText.Parse(move.ClientParam1, "P_TaskActions");
    }

    public bool StepExists(uint type, ulong stepId) => Of(type)?.Steps.ContainsKey(stepId) ?? false;

    public uint TaskOfStep(uint type, ulong stepId) => Of(type)?.StepTasks.GetValueOrDefault(stepId) ?? 0;

    public IReadOnlyList<ulong> Actions(uint type, ulong stepId) => Of(type)?.Steps.GetValueOrDefault(stepId)?.Actions ?? [];

    public IReadOnlyList<ulong> ServerActions(uint type, ulong stepId) => Of(type)?.ServerActions.GetValueOrDefault(stepId) ?? [];

    public PTaskActionsTyped? Action(uint type, ulong actionId) => Of(type)?.Actions.GetValueOrDefault(actionId);

    public bool IsServerSaved(uint type, ulong actionId) => Action(type, actionId) is {} action && IsSaved(action);

    // Lua CheckActionIsSave includes server targets even when server_save is false.
    private static bool IsSaved(PTaskActionsTyped action) => action.ServerSave || action.ServerTargetType != 0;

    public bool IsNecessary(uint type, ulong actionId) => Of(type)?.Actions.GetValueOrDefault(actionId)?.Necessary ?? false;

    public ulong StepOfAction(uint type, ulong actionId) => Of(type)?.ActionSteps.GetValueOrDefault(actionId) ?? 0;

    public bool IsFailureAction(uint type, ulong stepId, ulong actionId) =>
        Of(type)?.Steps.GetValueOrDefault(stepId)?.FailAction.Contains(actionId) ?? false;

    public IReadOnlyList<ulong> FailureActions(uint type, ulong stepId) =>
        Of(type)?.Steps.GetValueOrDefault(stepId)?.FailAction ?? [];

    public bool IsPostAction(uint type, ulong stepId, ulong actionId) =>
        Of(type)?.Steps.GetValueOrDefault(stepId) is {} step
        && (step.PostId.Contains(actionId) || step.PostFailaction.Contains(actionId));

    public ulong RollbackStep(uint type, ulong stepId)
    {
        var rollback = Of(type)?.Steps.GetValueOrDefault(stepId)?.RollbackStepWhenFail ?? 0;
        var taskId = TaskOfStep(type, stepId);

        // Unconfigured puzzle failures re-arm the arrival steps just before them, otherwise the puzzle restarts
        // around a player who already left its boundary and fails again.
        if (rollback == 0)
            return ApproachStart(type, taskId, stepId);

        var index = StepIndex(type, taskId, rollback);
        // A rollback must stay in the same task, but its destination can come after the failed step.
        return index >= 0 ? rollback : stepId;
    }

    private const int ArrivePositionTarget = 1;
    private const int PuzzleStateTarget = 33;

    private ulong ApproachStart(uint type, uint taskId, ulong stepId)
    {
        if (!FailureActions(type, stepId).Any(id => Action(type, id)?.TargetType == PuzzleStateTarget))
            return stepId;

        var steps = Steps(type, taskId);
        var index = StepIndex(type, taskId, stepId);

        while (index > 0 && Actions(type, steps[index - 1]) is [_, ..] actions
               && actions.All(id => Action(type, id)?.TargetType == ArrivePositionTarget))
            index--;

        return index >= 0 ? steps[index] : stepId;
    }

    private sealed class Namespace
    {
        public required Dictionary<uint, PTasksListTyped> Tasks { get; init; }
        public required Dictionary<ulong, PTaskStepsTyped> Steps { get; init; }
        public required Dictionary<ulong, PTaskActionsTyped> Actions { get; init; }

        public required Dictionary<ulong, ulong[]> ServerActions { get; init; }

        // Ignore orphan steps. The dump contains obsolete actions with stale originStep values.
        public Dictionary<ulong, uint> StepTasks { get; } = [];
        public Dictionary<ulong, ulong> ActionSteps { get; } = [];
        public Dictionary<ulong, int> StepIndices { get; } = [];

        public required Dictionary<uint, uint[]> NextTasks { get; init; }

        public required IReadOnlyList<uint> Roots { get; set; }
    }
}
