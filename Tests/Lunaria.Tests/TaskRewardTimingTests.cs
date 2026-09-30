using Lunaria.Game.Player;
using Lunaria.Game.Resources;
using Msg;
using Xunit;

namespace Lunaria.Tests;

[Collection("bundled-gameplay")]
public sealed class TaskRewardTimingTests(BundledGameplayFixture fixture)
{
    [Theory]
    [InlineData(91001u)]
    [InlineData(31090u)]
    [InlineData(31091u)]
    public void PreviewOnlyTask_DoesNotCreditInventoryExperienceOrRewardPopup(uint task)
    {
        var player = new Player(1, fixture.Data);
        var assets = fixture.Data.Tasks;
        Assert.Equal(0u, assets.RewardDrop(1, task));
        Assert.NotEmpty(assets.RewardItems(1, task));
        var last = assets.Steps(1, task)[^1];
        player.Tasks.Load([(1u, task, last, Array.Empty<(ulong, uint, uint)>().AsEnumerable())], []);
        player.Map.BeginEnter(100001001001, 0);
        player.Map.FinishEnter();
        var before = player.Progress.TeamExp;
        foreach (var id in assets.Actions(1, last))
        {
            var result = player.ReportTaskAction(1, id, 1);
            Assert.Equal(0, result.Code);
            Assert.False(result.Outcome!.Delivery.HasChanges);
            Assert.DoesNotContain(result.Outcome.AllNotifications, n => n is SCPreciousAwardShowNtf);
        }
        Assert.True(player.Tasks.IsFinished(1, task));
        Assert.Equal(before, player.Progress.TeamExp);
        Assert.Empty(player.InventoryItems());
        Assert.Empty(player.Wallet.All());
    }

    [Fact]
    public void MainQuest_PaysConfiguredRewardOnceAtCompletion()
    {
        var player = new Player(1, fixture.Data);
        player.Tasks.Load([(1u, 11002u, 1100251ul, Array.Empty<(ulong, uint, uint)>().AsEnumerable())], []);
        Assert.Equal(600002u, fixture.Data.Tasks.RewardDrop(1, 11002));
        Assert.Empty(player.SettleServerTargets());
        var result = player.ReportTaskAction(1, 110025101, 1);
        Assert.Equal(0, result.Code);
        Assert.True(result.Outcome!.Progress.TaskCompleted);
        Assert.True(result.Outcome.Delivery.HasChanges);
        Assert.Equal(EnmItemReason.EnmItemChangeTask, result.Outcome.Delivery.Reason);
        Assert.Equal(5u, player.Bag.CountOf(11201001));
        Assert.Equal(10000ul, player.OwnedItemCount(1));
        Assert.True(result.Outcome.Delivery.TeamExpAwarded >= 2000);
        Assert.Empty(player.ReportTaskAction(1, 110025101, 1).Outcome!.AllNotifications);
        Assert.Equal(5u, player.Bag.CountOf(11201001));
    }
}
