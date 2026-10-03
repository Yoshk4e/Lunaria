using Lunaria.Game.Player;
using Lunaria.Game.Resources;
using Lunaria.Game.Resources.Tables;
using Lunaria.Game.Tasks;
using Msg;
using Xunit;

namespace Lunaria.Tests;

[Collection("bundled-gameplay")]
public sealed class TaskServerTargetTests(BundledGameplayFixture fixture)
{
    private GameData Assets => fixture.Data;

    private IEnumerable<PTaskActionsTyped> Actions(ServerTarget target, string name = "QuestMain", uint type = TaskAssets.QuestMain)
    {
        var steps = fixture.Rows("P_TaskSteps_" + name).ToDictionary(s => s.GetProperty("id").GetUInt64());
        return fixture.Rows("P_TasksList_" + name)
            .SelectMany(t => t.GetProperty("steps").EnumerateArray().Select(s => s.GetUInt64()))
            .SelectMany(s => steps[s].GetProperty("actions").EnumerateArray().Select(a => a.GetUInt64()))
            .Distinct().Select(id => Assets.Tasks.Action(type, id)!)
            .Where(a => a.ServerTargetType == (int)target);
    }

    private void AtAction(Player player, PTaskActionsTyped action,
        params (uint Type, uint Task)[] finished)
    {
        var step = Assets.Tasks.StepOfAction(action.TaskType, action.Id);
        var task = Assets.Tasks.TaskOfStep(action.TaskType, step);
        Assert.NotEqual(0u, task);
        player.Tasks.Load([(action.TaskType, task, step, Array.Empty<(ulong, uint, uint)>().AsEnumerable())], finished);
    }

    private static void Completed(Player player, PTaskActionsTyped action)
    {
        var report = player.ReportTaskAction(action.TaskType, action.Id, uint.MaxValue);
        Assert.Equal(0, report.Code);
        Assert.True(report.Outcome!.Progress.Recorded, $"Action {action.Id} did not settle");
        Assert.Equal(player.Tasks.Maximum(action.TaskType, action.Id), report.Outcome.Progress.Progress);
    }

    [Theory]
    [InlineData(ServerTarget.ArriveMap, 14)]
    [InlineData(ServerTarget.TeamLevel, 3)]
    [InlineData(ServerTarget.OwnHouse, 1)]
    [InlineData(ServerTarget.CompleteCaseStage, 5)]
    [InlineData(ServerTarget.CompleteTask, 41)]
    [InlineData(ServerTarget.OwnItem, 2)]
    [InlineData(ServerTarget.CompleteDungeon, 3)]
    public void EveryWorldFactTarget_RejectsClientClaimsUntilNamedConditionHolds(ServerTarget target, int expected)
    {
        var actions = Actions(target).ToArray();
        Assert.Equal(expected, actions.Length);
        foreach (var action in actions)
        {
            var player = new Player(1, Assets);
            AtAction(player, action);
            Assert.True(TargetParameter.TryId(action.ServerParam1, out var id));
            var before = player.Tasks.ToPlayerTaskData();
            Assert.Equal(0, player.ReportTaskAction(action.TaskType, action.Id, uint.MaxValue).Code);
            Assert.Equal(before, player.Tasks.ToPlayerTaskData());
            switch (target)
            {
                case ServerTarget.ArriveMap:
                    Assert.Equal(0, player.Map.BeginEnter(id, 0).Code);
                    player.ReportTaskAction(action.TaskType, action.Id, 1);
                    Assert.Equal(before, player.Tasks.ToPlayerTaskData());
                    Assert.Equal(0, player.Map.FinishEnter());
                    break;
                case ServerTarget.TeamLevel:
                    player.Progress.Load((uint)id - 1, 0, 0, 0, DateTimeOffset.UtcNow);
                    player.ReportTaskAction(action.TaskType, action.Id, 1);
                    Assert.Equal(before, player.Tasks.ToPlayerTaskData());
                    player.Progress.Load((uint)id, 0, 0, 0, DateTimeOffset.UtcNow);
                    break;
                case ServerTarget.OwnHouse:
                    player.Houses.MarkBought(1, DateTimeOffset.UtcNow);
                    break;
                case ServerTarget.CompleteCaseStage:
                    var clues = Assets.Cases.StageClues(id);
                    Assert.NotEmpty(clues);
                    player.Cases.OpenCase(Assets.Cases.Clue(clues[0])!.CaseId);
                    foreach (var clue in clues) Assert.True(player.Cases.GiveClue(clue));
                    player.ReportTaskAction(action.TaskType, action.Id, 1);
                    Assert.Equal(before, player.Tasks.ToPlayerTaskData());
                    foreach (var clue in clues) Assert.Equal(0, player.Cases.PutClue(clue).Result);
                    break;
                case ServerTarget.CompleteTask:
                    var type = TargetParameter.TryCount(action.ServerParam2, out var configured) && configured != 0
                        ? configured : TaskAssets.QuestMain;
                    player.Tasks.StartTask(type, (uint)id);
                    Assert.False(player.ReportTaskAction(action.TaskType, action.Id, 1).Outcome!.Progress.Recorded);
                    AtAction(player, action, (type, (uint)id));
                    break;
                case ServerTarget.OwnItem:
                    player.GrantRewards([new ItemGrant((uint)id, 1)], EnmItemReason.EnmItemChangeNormal);
                    Assert.Equal(1ul, player.OwnedItemCount((uint)id));
                    break;
                case ServerTarget.CompleteDungeon:
                    player.Dungeons.Load([(id, 1u)], [], [], null, DateTimeOffset.UtcNow);
                    break;
            }
            Completed(player, action);
        }
    }

    [Theory]
    [InlineData(ServerTarget.GiveItems, 19)]
    [InlineData(ServerTarget.OpenCase, 4)]
    [InlineData(ServerTarget.GiveClue, 18)]
    [InlineData(ServerTarget.StartTask, 2)]
    [InlineData(ServerTarget.ResetMonster, 3)]
    public void EveryStoryCommand_WaitsForReportAndDoesNotReplayAfterRewind(ServerTarget target, int expected)
    {
        var actions = Actions(target).ToArray();
        Assert.Equal(expected, actions.Length);
        foreach (var action in actions)
        {
            var player = new Player(1, Assets);
            AtAction(player, action);
            if (target == ServerTarget.ResetMonster)
            {
                Assert.Equal(0, player.Map.BeginEnter(100001001001, 0).Code);
                Assert.Equal(0, player.Map.FinishEnter());
                player.Battles.StartPatrolCooldown(12020016, DateTimeOffset.MaxValue);
            }
            Assert.Empty(player.SettleServerTargets());
            Completed(player, action);
            Assert.True(player.Tasks.HasAppliedEffect(action.TaskType, action.Id));
            if (target == ServerTarget.OpenCase)
            {
                TargetParameter.TryId(action.ServerParam1, out var caseId);
                Assert.True(player.Cases.IsProcessing((uint)caseId));
            }
            if (target == ServerTarget.GiveClue)
            {
                TargetParameter.TryId(action.ServerParam1, out var clue);
                Assert.Contains(clue, player.Cases.Processing[Assets.Cases.Clue(clue)!.CaseId].OwnedClues);
            }
            if (target == ServerTarget.ResetMonster)
                Assert.DoesNotContain(12020016L, player.Battles.PatrolCooldown);

            var inventory = player.InventoryItems().ToArray();
            var wallet = player.Wallet.All().ToArray();
            var characters = player.Characters.All.ToArray();
            var cases = player.Cases.ToCaseData();
            var effects = player.Tasks.AppliedEffects.ToArray();
            AtAction(player, action);
            player.Tasks.LoadAppliedEffects(effects);
            if (target == ServerTarget.ResetMonster) player.Battles.StartPatrolCooldown(12020016, DateTimeOffset.MaxValue);
            var replay = player.ReportTaskAction(action.TaskType, action.Id, 1);
            Assert.Equal(0, replay.Code);
            Assert.True(replay.Outcome!.Progress.Recorded);
            Assert.False(replay.Outcome.ActionDelivery.HasChanges);
            Assert.Empty(replay.Outcome.ServerNotifications);
            Assert.Equal(inventory, player.InventoryItems());
            Assert.Equal(wallet, player.Wallet.All());
            Assert.Equal(characters, player.Characters.All);
            Assert.Equal(cases, player.Cases.ToCaseData());
            if (target == ServerTarget.ResetMonster)
                Assert.Contains(12020016L, player.Battles.PatrolCooldown);
        }
    }

    [Fact]
    public void EveryTakeItemCommand_RetriesMissingCostsAndChargesOnlyOnce()
    {
        var actions = Actions(ServerTarget.TakeItems).ToArray();
        Assert.Equal(3, actions.Length);
        foreach (var action in actions)
        {
            var player = new Player(1, Assets);
            AtAction(player, action);
            Assert.True(TargetParameter.TryItems(action.ServerParam1, out var costs));
            foreach (var cost in costs.Skip(1)) player.Bag.Add(cost.ItemId, cost.Count * 2);
            var before = player.InventoryItems().ToArray();
            Assert.False(player.ReportTaskAction(action.TaskType, action.Id, 1).Outcome!.Progress.Recorded);
            Assert.Equal(before, player.InventoryItems());
            Assert.False(player.Tasks.HasAppliedEffect(action.TaskType, action.Id));
            player.Bag.Add(costs[0].ItemId, costs[0].Count * 2);
            Assert.Contains(player.SettleServerTargets(), o => o.Progress.SettledActions.Any(a => a.ActionId == action.Id));
            Assert.All(costs, c => Assert.Equal(c.Count, player.Bag.CountOf(c.ItemId)));
            var effects = player.Tasks.AppliedEffects.ToArray();
            AtAction(player, action);
            player.Tasks.LoadAppliedEffects(effects);
            Completed(player, action);
            Assert.All(costs, c => Assert.Equal(c.Count, player.Bag.CountOf(c.ItemId)));
        }
    }

    [Theory]
    [InlineData(ServerTarget.BuyItem, 2)]
    [InlineData(ServerTarget.CompleteBattle, 1)]
    public void EveryEventTarget_OnlyCountsMatchingEventsWhileActive(ServerTarget target, int expected)
    {
        var actions = Actions(target).ToArray();
        Assert.Equal(expected, actions.Length);
        foreach (var action in actions)
        {
            var player = new Player(1, Assets);
            TargetParameter.TryId(action.ServerParam1, out var id);
            player.RecordTaskEvent(target, id);
            AtAction(player, action);
            Assert.Empty(player.SettleServerTargets());
            Assert.False(player.ReportTaskAction(action.TaskType, action.Id, uint.MaxValue).Outcome!.Progress.Recorded);
            player.RecordTaskEvent(target, id, 0);
            if (target == ServerTarget.BuyItem) player.RecordTaskEvent(target, id + 1);
            Assert.Empty(player.SettleServerTargets());
            player.RecordTaskEvent(target, id == 0 ? 123u : id, uint.MaxValue);
            var settled = player.SettleServerTargets();
            Assert.Contains(settled, o => o.Progress.SettledActions.Any(a => a.ActionId == action.Id && a.Progress == 1));
            player.RecordTaskEvent(target, id);
            Assert.Empty(player.SettleServerTargets());
        }
    }

    [Fact]
    public void EveryClockTarget_SettlesAtItsMinuteOrWhenCrossedIncludingMidnight()
    {
        var actions = Actions(ServerTarget.ReachGameTime).ToArray();
        Assert.Equal(4, actions.Length);
        foreach (var action in actions)
        {
            Assert.True(TargetParameter.TryCount(action.ServerParam1, out var minute));
            var player = new Player(1, Assets);
            player.LoadGameTime((minute + 1438) % 1440);
            AtAction(player, action);
            Assert.False(player.ReportTaskAction(action.TaskType, action.Id, uint.MaxValue).Outcome!.Progress.Recorded);
            player.AdvanceGameTime(0);
            player.AdvanceGameTime(1);
            Assert.Empty(player.SettleServerTargets());
            player.AdvanceGameTime(2);
            Assert.Contains(player.SettleServerTargets(), o => o.Progress.SettledActions.Any(a => a.ActionId == action.Id));
            // Step 3102908 also sets the clock to 17:00 after its objective completes.
            Assert.Equal(action.Id == 310290801 ? 1020u : (minute + 1) % 1440, player.GameTimeMinutes);
            Assert.Empty(player.SettleServerTargets());
            AtAction(player, action);
            player.LoadGameTime(minute);
            Completed(player, action);
        }
    }

    [Fact]
    public void EveryWantedEventTarget_RequiresThatEventInTheCurrentRun()
    {
        var actions = Actions(ServerTarget.WantedEvent, "Wanted", TaskAssets.Wanted).ToArray();
        Assert.Equal(53, actions.Length);
        foreach (var action in actions)
        {
            var player = new Player(1, Assets);
            AtAction(player, action);
            Assert.True(TargetParameter.TryCount(action.ServerParam1, out var eventId));
            Assert.False(player.ReportTaskAction(action.TaskType, action.Id, 1).Outcome!.Progress.Recorded);
            player.Wanted.Enter(10101);
            var run = player.Wanted.CaptureRun()!;
            player.Wanted.Load([], run with { History = [(run.Current.ProcessId, uint.MaxValue)] });
            Assert.False(player.ReportTaskAction(action.TaskType, action.Id, 1).Outcome!.Progress.Recorded);
            var completed = run.Current.EventId == eventId
                ? run with { Current = run.Current with { EventDone = true } }
                : run with { History = [(run.Current.ProcessId, eventId)] };
            player.Wanted.Load([], completed);
            Completed(player, action);
            player.Wanted.Leave();
            AtAction(player, action);
            Assert.False(player.ReportTaskAction(action.TaskType, action.Id, 1).Outcome!.Progress.Recorded);
        }
    }
}
