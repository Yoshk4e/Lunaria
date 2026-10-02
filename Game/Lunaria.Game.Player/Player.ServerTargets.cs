using Google.Protobuf;
using Lunaria.Game.Player.Gameplay;
using Lunaria.Game.Player.Managers;
using Lunaria.Game.Resources;
using Lunaria.Game.Resources.Tables;
using Lunaria.Game.Tasks;
using Lunaria.Game.World;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    private readonly Queue<TaskActionOutcome> _pendingTaskOutcomes = new();
    private readonly Queue<TaskProgressResult> _pendingTimeTargets = new();
    private bool _gameTimeDirty;
    private uint _wantedTasksStep;

    public uint GameTimeMinutes { get; private set; }

    public void LoadGameTime(uint minute)
    {
        GameTimeMinutes = minute % 1440;
        _gameTimeDirty = false;
    }

    /// <summary>Older saves used a clock of 0. Use the table's start time for those saves.</summary>
    public void RestoreGameTime(uint minute) =>
        LoadGameTime(minute == 0 ? assets.Starter.GameTime : minute);

    public void RestoreWantedTaskStep() => _wantedTasksStep = Wanted.CurrentStep;

    public void AdvanceGameTime(uint elapsedMinutes)
    {
        using var operationTime = BeginOperation();
        EnsureLevelBaseline();
        var previous = GameTimeMinutes;
        GameTimeMinutes = (uint)(((ulong)previous + elapsedMinutes) % 1440);
        _gameTimeDirty |= GameTimeMinutes != previous;

        foreach (var result in Tasks.OnGameTimeAdvanced(previous, elapsedMinutes))
        {
            _pendingTaskOutcomes.Enqueue(Settle(result));
        }
    }

    public void ClearTaskEvents()
    {
        _pendingTaskOutcomes.Clear();
        _pendingTimeTargets.Clear();
    }

    internal void RecordTaskEvent(ServerTarget target, ulong id, uint count = 1)
    {
        EnsureLevelBaseline();
        foreach (var result in Tasks.OnServerEvent(target, id, count))
        {
            _pendingTaskOutcomes.Enqueue(Settle(result));
        }
    }

    public IReadOnlyList<TaskActionOutcome> SettleServerTargets()
    {
        using var operationTime = BeginOperation();
        EnsureLevelBaseline();
        var outcomes = new List<TaskActionOutcome>();

        if (Assets.Wanted.Event(Wanted.CurrentEventId) is {} wantedEvent)
        {
            if (_wantedTasksStep != Wanted.CurrentStep)
            {
                Tasks.ResetNamespace(TaskAssets.Wanted);
                _wantedTasksStep = Wanted.CurrentStep;
            }

            var run = Wanted.CaptureRun()!;
            var completedEvent = run.Current.EventDone ? wantedEvent
                : run.History.Count > 0 ? Assets.Wanted.Event(run.History[^1].EventId) : null;
            var tasks = wantedEvent.EventStartAddTask.Concat(completedEvent?.EventFinishAddTask ?? []);
            foreach (var taskId in tasks.Distinct())
            {
                if (Tasks.StartTask(TaskAssets.Wanted, taskId) is not {} data) continue;

                outcomes.Add(CreateTaskOutcome(new TaskProgressResult(TaskAssets.Wanted, taskId, Recorded: true, Progress: 0,
                        MaxProgress: 1, StepAdvanced: false, TaskCompleted: false, [taskId], [])
                    { StartedTaskData = [data] }, RewardDelivery.Empty, []));
            }
        }

        while (_pendingTaskOutcomes.TryDequeue(out var outcome))
            outcomes.Add(outcome);

        DrainTimeTargets(outcomes);

        var effects = new TargetEffects();

        foreach (var progress in Tasks.EvaluateServerTargets(action => EvaluateTaskTarget(action, effects)))
        {
            outcomes.Add(WithEffects(Settle(progress), effects));

            DrainTimeTargets(outcomes);

            effects = new TargetEffects();
        }
        SynchronizeLevelData();
        return outcomes;
    }

    private void DrainTimeTargets(List<TaskActionOutcome> outcomes)
    {
        // Finishing a time objective can change time again. Send that change after the step that caused it.
        while (_pendingTimeTargets.TryDequeue(out var progress))
            outcomes.Add(Settle(progress));
    }

    private static TaskActionOutcome WithEffects(TaskActionOutcome outcome, TargetEffects effects) => outcome with {
        ActionDelivery = effects.Delivery,
        ServerNotifications = outcome.ServerNotifications.Concat(effects.Notifications).ToArray()
    };

    private uint? EvaluateTaskTarget(PTaskActionsTyped action, TargetEffects effects)
    {
        var target = (ServerTarget)action.ServerTargetType;
        var command = TaskManager.IsCommand(action.ServerTargetType);
        if (command && Tasks.HasAppliedEffect(action.TaskType, action.Id)) return 1;

        var hasId = TargetParameter.TryId(action.ServerParam1, out var id);
        var hasCount = TargetParameter.TryCount(action.ServerParam2, out var count);
        uint? result = null;

        switch (target)
        {
            case ServerTarget.GiveItems:
                if (!TargetParameter.TryItems(action.ServerParam1, out var grants)
                    || grants.Any(g => !Assets.Items.Exists(g.ItemId))) break;

                effects.Delivery = GrantRewards(grants, EnmItemReason.EnmItemChangeTaskActionAdd);
                result = 1;
                break;
            case ServerTarget.TakeItems:
                if (!TargetParameter.TryItems(action.ServerParam1, out var costs)
                    || costs.Any(g => !Assets.Items.Exists(g.ItemId))) break;

                var bagCosts = costs.Where(g => Assets.Items.MoneyTypeOf(g.ItemId) is null).ToArray();

                var prices = costs.Where(g => Assets.Items.MoneyTypeOf(g.ItemId) is not null)
                    .Select(g => (Assets.Items.MoneyTypeOf(g.ItemId)!.Value, (long)g.Count)).ToArray();
                if (Purchase(bagCosts, prices, () => 0, EnmItemReason.EnmItemChangeTaskActionCost) != 0) break;

                result = 1;
                break;
            case ServerTarget.ArriveMap:
                if (hasId && Map.Phase == MapPhase.Loaded && Map.MapId == id) result = 1;
                break;
            case ServerTarget.TeamLevel:
                if (hasId && id > 0 && Progress.TeamLevel >= id) result = 1;
                break;
            case ServerTarget.OpenCase:
                if (!hasId || id > uint.MaxValue || !Assets.Cases.CaseExists((uint)id)) break;

                OpenTargetCase((uint)id, effects);
                result = 1;
                break;
            case ServerTarget.GiveClue:
                if (!hasId || Assets.Cases.Clue(id) is not {} clue) break;

                OpenTargetCase(clue.CaseId, effects);
                if (!Cases.GiveClue(id)) break;

                effects.Notifications.Add(new SCCaseNewClueNtf { ClueId = id });
                result = 1;
                break;
            case ServerTarget.GiveEvidence:
                if (!hasId || Assets.Cases.Evidence(id) is not {} evidence) break;

                OpenTargetCase(evidence.CaseId, effects);
                if (!Cases.GiveEvidence(id)) break;

                effects.Notifications.Add(new SCCaseNewEvidenceNtf { EvidenceId = id });
                result = 1;
                break;
            case ServerTarget.DecryptEvidence:
                if (!hasId || Cases.DecryptEvidence(id) != 0) break;

                effects.Notifications.Add(new SCCaseEvidenceDecrypted { EvidenceId = id });
                result = 1;
                break;
            case ServerTarget.StartTask:
                if (!hasId || id == 0 || id > uint.MaxValue) break;

                var taskType = hasCount && count != 0 ? count : TaskAssets.QuestMain;
                if (!Assets.Tasks.TaskExists(taskType, (uint)id)) break;

                if (Tasks.StartTask(taskType, (uint)id) is {} started)
                    effects.Notifications.Add(new SCTaskProgressUpdateNtf
                        { UpdatedData = started, UpdateType = EnmTaskActionUpdateType.EtaskActionUpdateTypeNew });
                if (Tasks.IsProcessing(taskType, (uint)id) || Tasks.IsFinished(taskType, (uint)id)) result = 1;
                break;
            case ServerTarget.CompleteCaseStage:
                if (!hasId) break;

                var clues = Assets.Cases.StageClues(id);
                if (clues.Count == 0 || Assets.Cases.Clue(clues[0]) is not {} stageClue) break;

                if (Cases.Finished.Contains(stageClue.CaseId)
                    || Cases.Processing.TryGetValue(stageClue.CaseId, out var current)
                    && clues.All(current.OnSlotClues.Contains)) result = 1;
                break;
            case ServerTarget.CompleteTask:
                if (hasId && id <= uint.MaxValue
                          && Tasks.IsFinished(hasCount && count != 0 ? count : TaskAssets.QuestMain, (uint)id)) result = 1;
                break;
            case ServerTarget.WantedEvent:
                if (hasId && id <= uint.MaxValue && Wanted.IsEventComplete((uint)id)) result = 1;
                break;
            case ServerTarget.OwnHouse:
                result = (uint)Houses.Houses.Count;
                break;
            case ServerTarget.MapState:
                if (hasId && id > 0 && Map.Phase == MapPhase.Loaded
                    && TaskManager.MatchesMap(action.ServerParam1, Map.MapId) != (hasCount && count == 1)) result = 1;
                break;
            case ServerTarget.OwnItem:
                if (!hasId || id == 0 || id > uint.MaxValue || !Assets.Items.Exists((uint)id)) break;

                var held = OwnedItemCount((uint)id);
                result = (uint)Math.Min(held, Tasks.Maximum(action.TaskType, action.Id));
                break;
            case ServerTarget.CompleteDungeon:
                if (hasId && id > 0) result = Dungeons.Finishes.GetValueOrDefault(id);
                break;
            case ServerTarget.ReachGameTime:
                if (hasId && id <= 1440 && GameTimeMinutes == id % 1440) result = 1;
                break;
            case ServerTarget.ResetMonster:
                if (!TargetParameter.TryId(action.ServerParam2, out var monster) || monster == 0
                                                                                 || monster > uint.MaxValue || Map.Phase != MapPhase.Loaded
                                                                                 || hasId && id != 0 &&
                                                                                 !TaskManager.MatchesMap(action.ServerParam1, Map.MapId))
                    break;

                Battles.ResetMonster((long)monster);
                var patrol = new SCPatrolMonsterRes();
                patrol.CdMonsters.AddRange(Battles.PatrolCooldown.Select(m => (uint)m));
                effects.Notifications.Add(patrol);
                result = 1;
                break;
            // Only accepted gameplay events advance counters. Task reports and historical totals do not.
            case ServerTarget.UseItem:
            case ServerTarget.CompleteBattle:
                break;
        }
        if (command && result is > 0) Tasks.MarkEffectApplied(action.TaskType, action.Id);
        return result;
    }

    private void OpenTargetCase(uint caseId, TargetEffects effects)
    {
        if (Cases.OpenCase(caseId) is { Opened: true } opened)
            effects.Notifications.Add(CaseManager.ToReceiveNotification(caseId, opened.ClueIds, opened.EvidenceIds));
    }

    private sealed class TargetEffects
    {
        public RewardDelivery Delivery { get; set; } = RewardDelivery.Empty;
        public List<IMessage> Notifications { get; } = [];
    }
}
