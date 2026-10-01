using Lunaria.Game.Player;
using Lunaria.Game.Resources;
using Lunaria.Game.Tasks;
using System.Text.Json;
using Msg;
using Xunit;

namespace Lunaria.Tests;

[Collection("bundled-gameplay")]
public sealed class TaskDataBehaviorTests(BundledGameplayFixture fixture)
{
    private GameData Assets => fixture.Data;

    private void AtStep(Player player, uint type, ulong step,
        params (ulong Action, uint Progress, uint Max)[] actions) =>
        player.Tasks.Load([(type, Assets.Tasks.TaskOfStep(type, step), step, actions.AsEnumerable())], []);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StoryCaseCommands_FollowTaskListWithoutEnteringOrphanStepsOrReplaying(bool reload)
    {
        var player = new Player(1, Assets);
        AtStep(player, TaskAssets.QuestMain, 1100370);
        foreach (var action in Assets.Tasks.Actions(TaskAssets.QuestMain, 1100370))
            Assert.Equal(0, player.ReportTaskAction(TaskAssets.QuestMain, action, 1).Code);
        Assert.Equal(1100371ul, player.Tasks.TaskDataOf(TaskAssets.QuestMain, 11003)!.CurrentStep.StepId);
        Assert.Equal(0u, Assets.Tasks.TaskOfStep(TaskAssets.QuestMain, 11003701));

        if (reload)
        {
            var effects = player.Tasks.AppliedEffects.ToArray();
            var reports = player.Tasks.ReportedTargets.ToArray();
            var state = player.Tasks.Processing.Values.Single();
            AtStep(player, state.Type, state.CurrentStep.StepId,
                state.CurrentStep.Actions.Select(a => (a.Key, a.Value.Progress, a.Value.MaxProgress)).ToArray());
            player.Tasks.LoadAppliedEffects(effects);
            player.Tasks.LoadReportedTargets(reports);
        }

        Assert.Empty(player.SettleServerTargets());
        foreach (var id in new ulong[] { 110037003, 110037004, 110037005, 110037006, 110037007 })
        {
            var report = player.ReportTaskAction(TaskAssets.QuestMain, id, uint.MaxValue);
            Assert.Equal(0, report.Code);
            Assert.False(report.Outcome!.Progress.Recorded);
            Assert.Empty(report.Outcome.AllNotifications);
        }
    }

    private static ulong[] Ids(JsonElement row, string field) => row.TryGetProperty(field, out var value)
        ? value.EnumerateArray().Select(v => v.GetUInt64()).ToArray() : [];

    [Theory]
    [InlineData("QuestMain", TaskAssets.QuestMain, 46)]
    [InlineData("POIQuest", TaskAssets.POIQuest, 16)]
    [InlineData("Wanted", TaskAssets.Wanted, 96)]
    public void EveryClientFailure_RollsBackWithoutCompletingOrRewarding(string name, uint type, int expected)
    {
        var tested = 0;
        foreach (var step in fixture.Rows("P_TaskSteps_" + name))
        {
            var stepId = step.GetProperty("id").GetUInt64();
            if (Assets.Tasks.TaskOfStep(type, stepId) == 0) continue;
            foreach (var actionId in Ids(step, "failAction"))
            {
                if (Assets.Tasks.Action(type, actionId)!.ServerTargetType != 0) continue;
                var player = new Player(1, Assets);
                AtStep(player, type, stepId);
                var before = player.Tasks.ToPlayerTaskData();
                Assert.Equal(0, player.ReportTaskAction(type, actionId, 0).Code);
                Assert.Equal(before, player.Tasks.ToPlayerTaskData());
                var result = player.ReportTaskAction(type, actionId, 1);
                Assert.Equal(0, result.Code);
                Assert.True(result.Outcome!.Progress.TaskFailed);
                Assert.False(result.Outcome.Progress.TaskCompleted);
                Assert.False(result.Outcome.Delivery.HasChanges);
                Assert.Empty(result.Outcome.Progress.PassedSteps);
                Assert.Empty(result.Outcome.Progress.StartedTasks);
                var rollback = step.TryGetProperty("rollbackStepWhenFail", out var configured) && configured.GetUInt64() != 0
                    ? configured.GetUInt64() : Assets.Tasks.RollbackStep(type, stepId);
                Assert.Equal(rollback, result.Outcome.Progress.UpdatedData!.CurrentStep.StepId);
                tested++;
            }
        }
        Assert.Equal(expected, tested);
    }

    [Theory]
    [InlineData(TaskAssets.POIQuest, 900702ul, 900700ul)]
    [InlineData(TaskAssets.POIQuest, 100203ul, 100202ul)]
    [InlineData(TaskAssets.QuestMain, 9998102ul, 9998101ul)]
    public void UnconfiguredPuzzleFailure_RearmsTheArrivalStepsBeforeIt(uint type, ulong failed, ulong expected)
    {
        var player = new Player(1, Assets);
        AtStep(player, type, failed);
        var failure = Assets.Tasks.FailureActions(type, failed).First();
        var result = player.ReportTaskAction(type, failure, 1);
        Assert.Equal(0, result.Code);
        Assert.True(result.Outcome!.Progress.TaskFailed);
        Assert.Equal(expected, result.Outcome.Progress.UpdatedData!.CurrentStep.StepId);
    }

    [Fact]
    public void Polling_HandlesMapFailureWithoutPassingOrRewardingTheFailedStep()
    {
        var player = new Player(1, Assets);
        AtStep(player, TaskAssets.QuestMain, 11009007);
        Assert.Equal(0, player.Map.BeginEnter(100001001001, 0).Code);
        Assert.Equal(0, player.Map.FinishEnter());
        var results = player.SettleServerTargets();
        Assert.NotEmpty(results);
        Assert.All(results, r => {
            Assert.True(r.Progress.TaskFailed);
            Assert.Empty(r.Progress.PassedSteps);
            Assert.False(r.Delivery.HasChanges);
        });
        Assert.Single(results);
        Assert.Equal(11009001ul, player.Tasks.TaskDataOf(TaskAssets.QuestMain, 11009)!.CurrentStep.StepId);
        Assert.Empty(player.SettleServerTargets());
    }

    [Fact]
    public void ScriptStartedTask_WaitsForItsStoryCommandInsteadOfSeedingAtLogin()
    {
        var player = new Player(1, Assets);
        player.Tasks.EnsureStarted();
        Assert.False(player.Tasks.IsProcessing(TaskAssets.QuestMain, 99970));
        Assert.Contains(player.Tasks.Processing.Keys, t => t.Type == TaskAssets.QuestMain);
        AtStep(player, TaskAssets.QuestMain, 11003221);
        var started = player.ReportTaskAction(TaskAssets.QuestMain, 1100322101, 1);
        Assert.Equal(0, started.Code);
        Assert.True(player.Tasks.IsProcessing(TaskAssets.QuestMain, 99970));
        Assert.Single(started.Outcome!.AllNotifications.OfType<SCTaskProgressUpdateNtf>(),
            n => n.UpdateType == EnmTaskActionUpdateType.EtaskActionUpdateTypeNew && n.UpdatedData.TaskId == 99970);
        Assert.Empty(player.ReportTaskAction(TaskAssets.QuestMain, 1100322101, 1).Outcome!.AllNotifications);
    }

    [Theory]
    [InlineData("QuestMain", TaskAssets.QuestMain)]
    [InlineData("Wanted", TaskAssets.Wanted)]
    public void EveryReachableMapTarget_RequiresLoadedMapAndHonorsPolarityAndRollback(string name, uint type)
    {
        var maps = fixture.Rows("P_MapDataTable").Select(r => r.GetProperty("id").GetUInt64())
            .Where(Assets.Maps.IsPlayable).ToArray();
        var actions = fixture.Rows("P_TaskActions_" + name).ToDictionary(r => r.GetProperty("id").GetUInt64());
        var tested = 0;
        foreach (var step in fixture.Rows("P_TaskSteps_" + name))
        {
            var stepId = step.GetProperty("id").GetUInt64();
            var task = Assets.Tasks.TaskOfStep(type, stepId);
            if (task == 0) continue;
            var failure = Ids(step, "failAction");
            foreach (var actionId in Ids(step, "actions").Concat(failure))
            {
                var action = actions[actionId];
                if (!action.TryGetProperty("serverTargetType", out var target) || target.GetInt32() != (int)ServerTarget.MapState) continue;
                var parameter = action.GetProperty("serverParam1").GetString()!;
                var mustLeave = action.TryGetProperty("serverParam2", out var param2) && TargetParameter.TryCount(param2.GetString()!, out var count) && count == 1;
                var matching = maps.First(m => TaskManager.MatchesMap(parameter, m));
                var other = maps.First(m => !TaskManager.MatchesMap(parameter, m));
                var accepted = mustLeave ? other : matching;
                var rejected = mustLeave ? matching : other;
                var player = new Player(1, Assets);
                AtStep(player, type, stepId);
                var before = player.Tasks.ToPlayerTaskData();
                player.ReportTaskAction(type, actionId, 1);
                Assert.Equal(before, player.Tasks.ToPlayerTaskData());
                Assert.Equal(0, player.Map.BeginEnter(rejected, 0).Code);
                Assert.Equal(0, player.Map.FinishEnter());
                player.ReportTaskAction(type, actionId, uint.MaxValue);
                Assert.Equal(before, player.Tasks.ToPlayerTaskData());
                Assert.Equal(0, player.Map.BeginEnter(accepted, 0).Code);
                player.ReportTaskAction(type, actionId, 1);
                Assert.Equal(before, player.Tasks.ToPlayerTaskData());
                Assert.Equal(0, player.Map.FinishEnter());
                var result = player.ReportTaskAction(type, actionId, 1);
                Assert.Equal(0, result.Code);
                Assert.True(result.Outcome!.Progress.Recorded);
                Assert.Equal(failure.Contains(actionId), result.Outcome.Progress.TaskFailed);
                if (failure.Contains(actionId))
                {
                    var rollback = step.TryGetProperty("rollbackStepWhenFail", out var configured) && configured.GetUInt64() != 0
                        ? configured.GetUInt64() : stepId;
                    Assert.Equal(rollback, result.Outcome.Progress.UpdatedData!.CurrentStep.StepId);
                    Assert.False(result.Outcome.Delivery.HasChanges);
                    Assert.Empty(result.Outcome.Progress.PassedSteps);
                }
                tested++;
            }
        }
        Assert.Equal(type == TaskAssets.QuestMain ? 133 : 96, tested);
    }

    [Theory]
    [InlineData("QuestMain", TaskAssets.QuestMain, 113)]
    [InlineData("POIQuest", TaskAssets.POIQuest, 44)]
    [InlineData("Wanted", TaskAssets.Wanted, 118)]
    [InlineData("DailyTask", TaskAssets.DailyTask, 28)]
    public void EveryConfiguredTask_WalksInClientOrderWithOneCompletion(string name, uint type, int count)
    {
        var tasks = fixture.Rows("P_TasksList_" + name);
        var steps = fixture.Rows("P_TaskSteps_" + name).ToDictionary(r => r.GetProperty("id").GetUInt64());
        Assert.Equal(count, tasks.Length);
        foreach (var task in tasks)
        {
            var manager = new TaskManager(Assets);
            var id = task.GetProperty("id").GetUInt32();
            Assert.NotNull(manager.StartTask(type, id));
            var chain = Ids(task, "steps");
            var transitions = new List<TaskProgressResult>();
            foreach (var step in chain)
            {
                Assert.Equal(step, manager.TaskDataOf(type, id)!.CurrentStep.StepId);
                foreach (var action in Ids(steps[step], "actions"))
                {
                    // This stub tests graph structure. Separate tests cover server target evaluation.
                    var (code, result) = manager.ReportAction(type, action, uint.MaxValue,
                        evaluateTarget: _ => manager.Maximum(type, action));
                    Assert.True(code == 0, $"{name} task {id}, step {step}, action {action}: {code}");
                    if (!result!.StepAdvanced) continue;
                    transitions.Add(result);
                    break;
                }
                Assert.Equal(step, transitions.LastOrDefault()?.PassedSteps.Single());
            }
            Assert.True(manager.IsFinished(type, id));
            Assert.Single(transitions, r => r.TaskCompleted);
            Assert.Equal(chain, transitions.SelectMany(r => r.PassedSteps));
            Assert.Equal(chain.Skip(1), transitions.Where(r => !r.TaskCompleted).Select(r => r.UpdatedData!.CurrentStep.StepId));
            var followups = Ids(task, "nextTasks").Where(t => Assets.Tasks.TaskExists(type, (uint)t)).Select(t => (uint)t);
            Assert.Equal(followups, transitions[^1].StartedTasks);
            manager.ClearDirty();
            var replay = manager.ReportAction(type, Ids(steps[chain[^1]], "actions")[0], uint.MaxValue);
            Assert.Equal(0, replay.Code);
            Assert.False(replay.Result!.Recorded);
            Assert.False(manager.IsDirty);
        }
    }

    [Theory]
    [InlineData("QuestMain", TaskAssets.QuestMain)]
    [InlineData("POIQuest", TaskAssets.POIQuest)]
    [InlineData("Wanted", TaskAssets.Wanted)]
    [InlineData("DailyTask", TaskAssets.DailyTask)]
    public void EveryReachableAction_ZeroReportAndCleanupCannotAdvance(string name, uint type)
    {
        var steps = fixture.Rows("P_TaskSteps_" + name).ToDictionary(r => r.GetProperty("id").GetUInt64());
        foreach (var task in fixture.Rows("P_TasksList_" + name))
        {
            var id = task.GetProperty("id").GetUInt32();
            var manager = new TaskManager(Assets);
            manager.StartTask(type, id);
            manager.ClearDirty();
            var before = manager.ToPlayerTaskData();
            foreach (var step in Ids(task, "steps"))
            foreach (var action in Ids(steps[step], "actions"))
            {
                Assert.Equal(0, manager.ReportAction(type, action, 0, evaluateTarget: _ => throw new InvalidOperationException()).Code);
                Assert.Equal(before, manager.ToPlayerTaskData());
                Assert.False(manager.IsDirty);
            }

            foreach (var step in Ids(task, "steps"))
            {
                manager.Load([(type, id, step, Array.Empty<(ulong, uint, uint)>().AsEnumerable())], []);
                before = manager.ToPlayerTaskData();
                foreach (var action in Ids(steps[step], "postId").Concat(Ids(steps[step], "postFailaction")))
                {
                    if (Assets.Tasks.Action(type, action) is null) continue;
                    var response = manager.ReportAction(type, action, 1);
                    Assert.Equal(0, response.Code);
                    Assert.False(response.Result!.Recorded);
                    Assert.Equal(before, manager.ToPlayerTaskData());
                    Assert.False(manager.IsDirty);
                }
            }
        }
    }
}
