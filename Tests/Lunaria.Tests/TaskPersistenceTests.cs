using Lunaria.Game.Player;
using Lunaria.Game.Player.Persistence.Saves;
using Lunaria.Game.Resources;
using Lunaria.Game.Tasks;
using Msg;
using Xunit;

namespace Lunaria.Tests;

public sealed partial class RoleSessionTests
{
    private void LoadTaskAction(Player player, ulong actionId)
    {
        var step = _assets.Tasks.StepOfAction(TaskAssets.QuestMain, actionId);
        var task = _assets.Tasks.TaskOfStep(TaskAssets.QuestMain, step);
        Assert.NotEqual(0u, task);
        player.Tasks.Load([(TaskAssets.QuestMain, task, step, Array.Empty<(ulong, uint, uint)>().AsEnumerable())], []);
    }

    [Fact]
    public async Task TaskGrant_AppliedMarkerAndRewardsSurviveRoleSwitchAndRewind()
    {
        const ulong action = 110020101;
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        LoadTaskAction(ctx.Player, action);
        var before = ctx.Player.Wallet.Balance(1);
        var grant = ctx.Player.ReportTaskAction(TaskAssets.QuestMain, action, 1);
        Assert.True(grant.Outcome!.ActionDelivery.HasChanges);
        Assert.Equal(before + 2000, ctx.Player.Wallet.Balance(1));
        var held = ctx.Player.OwnedItemCount(11201003);
        Assert.True(held > 0);
        var effects = ctx.Player.Tasks.AppliedEffects.ToArray();
        LoadTaskAction(ctx.Player, action);
        ctx.Player.Tasks.LoadAppliedEffects(effects);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 2));
        Assert.False(ctx.Player.Tasks.HasAppliedEffect(TaskAssets.QuestMain, action));
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        Assert.True(ctx.Player.Tasks.HasAppliedEffect(TaskAssets.QuestMain, action));
        var replay = ctx.Player.ReportTaskAction(TaskAssets.QuestMain, action, 1);
        Assert.True(replay.Outcome!.Progress.StepAdvanced);
        Assert.False(replay.Outcome.ActionDelivery.HasChanges);
        Assert.Equal(before + 2000, ctx.Player.Wallet.Balance(1));
        Assert.Equal(held, ctx.Player.OwnedItemCount(11201003));
    }

    [Fact]
    public async Task TaskCost_PendingClientReportSurvivesReloadAndRetriesOnlyOnce()
    {
        const ulong action = 310152001;
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        LoadTaskAction(ctx.Player, action);
        Assert.True(TargetParameter.TryItems(_assets.Tasks.Action(TaskAssets.QuestMain, action)!.ServerParam1, out var costs));
        Assert.All(costs, c => Assert.Equal(0u, ctx.Player.Bag.CountOf(c.ItemId)));
        Assert.False(ctx.Player.ReportTaskAction(TaskAssets.QuestMain, action, 1).Outcome!.Progress.Recorded);
        Assert.Contains((TaskAssets.QuestMain, action), ctx.Player.Tasks.ReportedTargets);
        Assert.True(ctx.Player.SaveDirty);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 2));
        Assert.DoesNotContain((TaskAssets.QuestMain, action), ctx.Player.Tasks.ReportedTargets);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        Assert.Contains((TaskAssets.QuestMain, action), ctx.Player.Tasks.ReportedTargets);
        foreach (var cost in costs) ctx.Player.Bag.Add(cost.ItemId, cost.Count * 2);
        Assert.Single(ctx.Player.SettleServerTargets(), o => o.Progress.SettledActions.Any(a => a.ActionId == action));
        Assert.All(costs, c => Assert.Equal(c.Count, ctx.Player.Bag.CountOf(c.ItemId)));
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var replay = ctx.Player.ReportTaskAction(TaskAssets.QuestMain, action, uint.MaxValue);
        Assert.False(replay.Outcome!.Progress.Recorded);
        Assert.All(costs, c => Assert.Equal(c.Count, ctx.Player.Bag.CountOf(c.ItemId)));
    }

    [Fact]
    public async Task TaskFailure_RollbackAndClearedReportsSurviveReload()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        LoadTaskAction(ctx.Player, 1000100100201);
        var failure = _assets.Tasks.Action(TaskAssets.QuestMain, 1000100200102)!;
        var result = ctx.Player.ReportTaskAction(TaskAssets.QuestMain, failure.Id, 1);
        Assert.True(result.Outcome!.Progress.TaskFailed);
        Assert.Equal(10001001001ul, result.Outcome.Progress.UpdatedData!.CurrentStep.StepId);
        Assert.Empty(result.Outcome.Progress.PassedSteps);
        Assert.False(result.Outcome.Delivery.HasChanges);
        Assert.Contains(result.Outcome.AllNotifications.OfType<SCTaskProgressUpdateNtf>(),
            n => n.UpdateType == EnmTaskActionUpdateType.EtaskActionUpdateTypeFail);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        Assert.Equal(10001001001ul, ctx.Player.Tasks.TaskDataOf(TaskAssets.QuestMain, 10001004)!.CurrentStep.StepId);
        Assert.Empty(ctx.Player.Tasks.ReportedTargets);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SignIn_ClaimAndAttendanceSurviveSQLiteIncludingLegacySave(bool legacy)
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var now = DateTimeOffset.FromUnixTimeSeconds((long)_assets.SignIn.Activity(3)!.TimeOffsetStart).AddHours(1);
        if (legacy)
        {
            var document = RoleSaveMapper.Capture(ctx.Player) with {
                SignIn = new RoleSaveDocument.SignInSave {
                    SignedDays = [1], ClaimedDays = [1], LastSignInDay = now.ToUnixTimeSeconds() / 86400
                }
            };
            RoleSaveMapper.Apply(ctx.Player, document);
            Assert.NotEqual(0, ctx.Player.SignIn.Claim(3, 1).Result);
            now = now.AddDays(1);
        }
        ctx.Player.SignIn.Query(3, now);
        var day = legacy ? 2u : 1u;
        Assert.Equal(0, ctx.Player.ClaimSignInReward(3, day).Code);
        var calendar = ctx.Player.SignIn.Query(3, now).Data!;
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 2));
        Assert.False(ctx.Player.SignIn.HasClaimableDay(3));
        Assert.Empty(ctx.Player.SignIn.ClaimedDays);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        Assert.Equal(calendar, ctx.Player.SignIn.Query(3, now).Data);
        Assert.NotEqual(0, ctx.Player.ClaimSignInReward(3, day).Code);
        Assert.False(ctx.Player.SignIn.IsDirty);
    }
}
