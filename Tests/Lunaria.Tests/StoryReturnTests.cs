using System.Text.Json;
using Lunaria.Game.Player;
using Lunaria.Game.Player.Persistence.Saves;
using Lunaria.Game.Resources;
using Lunaria.Game.World;
using Msg;
using Xunit;

namespace Lunaria.Tests;

[Collection("bundled-gameplay")]
public sealed class StoryReturnTests(BundledGameplayFixture fixture)
{
    private const ulong CafeMap = 100001001001;
    private const ulong PalaceMap = 212001001001;
    private static readonly (int X, int Y, int Z) CafePosition = (153083, 40326, 32111);

    private Player AtCafe()
    {
        var player = new Player(1, fixture.Data);
        player.Tasks.Load([
            (1u, 11002u, 1100206ul, Array.Empty<(ulong, uint, uint)>().AsEnumerable()),
            (1u, 91001u, 1100299ul, Array.Empty<(ulong, uint, uint)>().AsEnumerable())
        ], []);
        Enter(player, CafeMap);
        Assert.True(player.Map.SyncPosition(CafePosition));
        return player;
    }

    private static void Enter(Player player, ulong map)
    {
        Assert.True(player.Map.BeginEnter(map, 0).Ok);
        Assert.Equal(0, player.Map.FinishEnter());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MindPalace_CompletedVisitResumesCafeSceneAndPosition(bool reconnect)
    {
        var player = AtCafe();
        Assert.True(player.Map.BeginEnter(PalaceMap, 0).Ok);
        Assert.False(player.ReportTaskAction(1, 110020601, 1).Outcome!.Progress.Recorded);
        Assert.Equal(0, player.Map.FinishEnter());
        Assert.DoesNotContain(player.SettleMapArrival(PalaceMap), o => o.Progress.TaskFailed);
        Assert.Equal(11002061ul, player.Tasks.TaskDataOf(1, 11002)!.CurrentStep.StepId);
        Assert.True(player.Map.SyncPosition((567, -2093, 7)));

        // Observed client reports, preserving their order.
        foreach (var id in new ulong[] { 110020701, 1100207201, 110020801, 1100208301,
                     1100208102, 1100208101, 1100208103, 1100208201, 110020901, 110020902 })
        {
            Assert.Equal(0, player.ReportTaskAction(1, id, 1).Code);
            Assert.DoesNotContain(player.SettleServerTargets(), o => o.Progress.TaskFailed);
        }

        if (reconnect)
        {
            var json = JsonSerializer.Serialize(RoleSaveMapper.Capture(player), SaveJson.Options);
            player = new Player(2, fixture.Data);
            RoleSaveMapper.Apply(player, JsonSerializer.Deserialize<RoleSaveDocument>(json, SaveJson.Options)!);
            Enter(player, PalaceMap);
        }

        Assert.True(player.Map.BeginEnter(CafeMap, 0).Ok);
        Assert.Equal(CafePosition, player.Map.Position);
        Assert.Equal(EnmBornPosType.EnmBornPosition, player.Map.BornPosType);
        Assert.False(player.ReportTaskAction(1, 110021002, 1).Outcome!.Progress.Recorded);
        Assert.Equal(0, player.Map.FinishEnter());
        var outcomes = player.SettleMapArrival(CafeMap);
        Assert.DoesNotContain(outcomes, o => o.Progress.TaskFailed);
        Assert.All(outcomes, o => Assert.False(o.Delivery.HasChanges));
        Assert.True(player.Tasks.IsFinished(TaskAssets.QuestMain, 91001));
        Assert.Equal(1100211ul, player.Tasks.TaskDataOf(1, 11002)!.CurrentStep.StepId);
        Assert.Equal(1100211ul, player.Tasks.ToPlayerTaskData().ProcessingTasks
            .Single(t => t.TaskId == 11002).CurrentStep.StepId);
        var scene = fixture.Data.Tasks.Action(1, 110021101)!;
        Assert.Equal(11, scene.TargetType);
        Assert.Equal("30011.0", scene.ClientParam1);
        Assert.Equal(0u, player.Tasks.TaskDataOf(1, 11002)!.CurrentStep.Actions.Single().Progress);
        Assert.Null(player.Map.ReturnPoint);
        Assert.Empty(player.SettleServerTargets());
        Assert.Empty(player.ReportTaskAction(1, 110021002, 1).Outcome!.AllNotifications);
    }

    [Fact]
    public void MindPalace_AbandonedVisitStillRollsBackWithoutRewards()
    {
        var player = AtCafe();
        Enter(player, PalaceMap);
        player.SettleMapArrival(PalaceMap);
        Enter(player, CafeMap);
        var outcomes = player.SettleMapArrival(CafeMap);
        Assert.Contains(outcomes, o => o.Progress.TaskId == 11002 && o.Progress.TaskFailed);
        Assert.All(outcomes, o => Assert.False(o.Delivery.HasChanges));
        Assert.False(player.Tasks.IsFinished(1, 91001));
        Assert.Equal(1100203ul, player.Tasks.TaskDataOf(1, 11002)!.CurrentStep.StepId);
        Assert.Equal(CafePosition, player.Map.Position);
    }

    [Fact]
    public void ReturnPoint_SurvivesStoryMapHops()
    {
        var player = AtCafe();
        Enter(player, PalaceMap);
        Enter(player, 207001001001);
        Assert.Equal(CafeMap, player.Map.ReturnPoint!.MapId);
        Enter(player, CafeMap);
        Assert.Equal(CafePosition, player.Map.Position);
        Assert.Null(player.Map.ReturnPoint);
    }

    [Theory]
    [InlineData(true, 1100211ul, true)]
    [InlineData(false, 1100211ul, false)]
    [InlineData(true, 11002061ul, false)]
    [InlineData(true, 1100280ul, false)]
    public void LegacySave_OnlyRecoversTheCompletedStrandedContinuation(bool finished, ulong step, bool recovered)
    {
        var player = new Player(1, fixture.Data);
        var snapshot = RoleSaveMapper.Capture(player) with {
            Map = new RoleSaveDocument.MapSave { MapId = PalaceMap, X = 567, Y = -2093, Z = 7 },
            Quests = new RoleSaveDocument.QuestsSave {
                Processing = [new() { TaskType = 1, Task = 11002, Step = step }],
                Finished = finished ? [new() { TaskType = 1, Task = 91001 }] : []
            }
        };
        RoleSaveMapper.Apply(player, snapshot);
        Assert.Equal(recovered ? CafeMap : PalaceMap, player.Map.MapId);
        Assert.Equal(step, player.Tasks.TaskDataOf(1, 11002)!.CurrentStep.StepId);
        Assert.Equal(finished, player.Tasks.IsFinished(1, 91001));
        Assert.Equal(MapPhase.Idle, player.Map.Phase);
        if (recovered)
        {
            Assert.Equal((149498, 49874, 32099), player.Map.Position);
            Assert.Equal(EnmBornPosType.EnmBornPosition, player.Map.BornPosType);
            Assert.True(player.Map.IsDirty);
            var repaired = RoleSaveMapper.Capture(player);
            RoleSaveMapper.Apply(player, repaired);
            Assert.Equal(JsonSerializer.Serialize(repaired.Map), JsonSerializer.Serialize(RoleSaveMapper.Capture(player).Map));
        }
    }
}
