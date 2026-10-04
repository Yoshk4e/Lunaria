using Google.Protobuf;
using Lunaria.Game.Player.Gameplay;
using Lunaria.Game.Player.Managers;
using Lunaria.Game.Resources;
using Lunaria.Game.Tasks;
using Lunaria.Game.World;
using Msg;

namespace Lunaria.Game.Player;

public sealed record TaskActionOutcome(
    TaskProgressResult Progress,
    RewardDelivery Delivery,
    IReadOnlyList<SCCaseReceiveNtf> CaseNotifications
)
{
    public RewardDelivery ActionDelivery { get; init; } = RewardDelivery.Empty;
    public IReadOnlyList<IMessage> ServerNotifications { get; init; } = [];
    public IReadOnlyList<IMessage> Notifications { get; init; } = [];

    // Send effects before the task-action reply and progress after it.
    public IEnumerable<IMessage> EffectNotifications => ServerNotifications.Concat(ActionDelivery.Presentation);
    public IEnumerable<IMessage> AllNotifications => EffectNotifications.Concat(Notifications);
}

public sealed partial class Player
{
    /// <summary>
    /// The client expects case 1002 after the opening quest. No table defines this transition from case 1001.
    /// </summary>
    private const uint OpeningFollowUpCase = 1002;

    public (int Code, TaskActionOutcome? Outcome) ReportTaskAction(uint taskType, ulong actionId, uint progress)
    {
        using var operationTime = BeginOperation();
        EnsureLevelBaseline();
        var effects = new TargetEffects();

        var (code, result) = Tasks.ReportAction(taskType, actionId, progress,
            Map.Phase == MapPhase.Loaded ? Map.MapId : null,
            action => EvaluateTaskTarget(action, effects));

        if (code != 0 || result is null)
            return (code, null);

        return (0, WithEffects(Settle(result), effects));
    }

    /// <param name="settleMarkers">
    /// False after a client action report: the EmptyAction delay of the step that just became current is the client's
    /// to run, and completing it here moved the player before the scenario covered the screen.
    /// </param>
    public IReadOnlyList<TaskActionOutcome> SettleMapArrival(ulong mapId, bool settleMarkers = true)
    {
        using var operationTime = BeginOperation();
        EnsureLevelBaseline();
        var outcomes = Tasks.OnMapEntered(mapId, settleMarkers).Select(Settle).ToList();
        // Resolve dependent quests before FinEnterMap. Lua uses its TaskData snapshot to resume the story.
        outcomes.AddRange(SettleServerTargets());
        SynchronizeLevelData();
        return outcomes;
    }

    private TaskActionOutcome Settle(TaskProgressResult result)
    {
        var cases = result.TaskCompleted
                    && result.TaskType == TaskAssets.QuestMain
                    && result.TaskId == TaskAssets.OpeningTaskId
                    && Cases.OpenCase(OpeningFollowUpCase) is { Opened: true } opened ?
            new List<SCCaseReceiveNtf>
                { CaseManager.ToReceiveNotification(OpeningFollowUpCase, opened.ClueIds, opened.EvidenceIds) } :
            [];

        var grants = new List<ItemGrant>();
        var notifications = new List<IMessage>();
        ApplyTaskEnvironment(result, notifications);

        var unlockedGuides = UnlockPassedGuides(result.PassedSteps);

        if (result.TaskType == TaskAssets.Wanted)
        {
            var before = Wanted.ToResource();
            var (completed, step, drop) = Wanted.OnTaskStepsPassed(result.PassedSteps);

            if (completed)
            {
                Gameplay.Publish(new WantedResourcesChanged(before));
                grants.AddRange(drop);
                if (step is not null) notifications.Add(step);
            }
        }

        var rewardId = Assets.Tasks.RewardDrop(result.TaskType, result.TaskId);
        var paysCompletion = result.TaskCompleted && result.Recorded && rewardId != 0;
        if (paysCompletion)
        {
            // UI previews include tasks with no payout. Require a reward ID before using the preview as a fallback.
            grants.AddRange(Assets.Drops.Exists(rewardId) ? Assets.Drops.Bundle(rewardId)
                : Assets.Tasks.RewardItems(result.TaskType, result.TaskId));
        }

        var delivery = GrantRewards(grants, paysCompletion ? EnmItemReason.EnmItemChangeTask
            : result.TaskType == TaskAssets.Wanted && grants.Count > 0 ? EnmItemReason.EnmItemChangeWantedStep : default)
            with { UnlockedGuides = unlockedGuides };
        return CreateTaskOutcome(result, delivery, cases) with { ServerNotifications = notifications };
    }

    private IReadOnlyList<uint> UnlockPassedGuides(IEnumerable<ulong> passedSteps)
    {
        var opened = Guides.UnlockMany(passedSteps.SelectMany(Assets.Guides.GuidesForStep), UtcNow.ToUnixTimeSeconds());
        if (opened.Count > 0) Gameplay.Publish(new GuidesChanged(opened));
        return opened;
    }

    public void UnlockWalkedGuides()
    {
        using var operationTime = BeginOperation();
        var passed = new List<ulong>();

        foreach (var (type, taskId) in Tasks.Finished)
            passed.AddRange(Assets.Tasks.Steps(type, taskId));

        foreach (var ((type, taskId), state) in Tasks.Processing)
        {
            var walk = Assets.Tasks.StepIndex(type, taskId, state.CurrentStep.StepId);

            foreach (var step in Assets.Tasks.Steps(type, taskId))
            {
                if (Assets.Tasks.StepIndex(type, taskId, step) < walk)
                    passed.Add(step);
            }
        }

        UnlockPassedGuides(passed);
    }

    public void InstallQuestGates()
    {
        using var operationTime = BeginOperation();
        Progress.QuestGate = taskId => Tasks.IsFinished(TaskAssets.QuestMain, taskId);
        Wanted.IsConditionMet = id => assets.Unlocks.IsConditionMet(id, Achievements.IsEventFinished);
    }

    public bool IsQuestMainFinished(uint taskId) => Tasks.IsFinished(TaskAssets.QuestMain, taskId);
}
