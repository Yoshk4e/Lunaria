using Lunaria.Game.Resources;
using Lunaria.Game.Tasks;
using Xunit;

namespace Lunaria.Tests;

[Collection("bundled-gameplay")]
[Trait("Category", "Audit")]
public sealed class AuditQuestTests(BundledGameplayFixture fixture)
{
    [Fact]
    public void MapArrival_WithEmptyMarkersAndPendingDialogue_TerminatesWithoutCompletingDialogue()
    {
        const ulong stepId = 1100510;
        var assets = fixture.Data;
        var taskId = assets.Tasks.TaskOfStep(TaskAssets.QuestMain, stepId);
        Assert.NotEqual(0u, taskId);
        var actions = assets.Tasks.Actions(TaskAssets.QuestMain, stepId).Select(id => assets.Tasks.Action(TaskAssets.QuestMain, id)!).ToArray();
        var markers = actions.Where(a => a.TargetType == TaskManager.EmptyActionTarget).ToArray();
        Assert.Equal(2, markers.Length);
        var dialogue = Assert.Single(actions, a => a.TargetType != TaskManager.EmptyActionTarget);
        Assert.True(dialogue.Necessary);
        var map = fixture.Rows("P_MapDataTable").Select(r => r.GetProperty("id").GetUInt64())
            .First(id => assets.Maps.MapExists(id) && TaskManager.MatchesMap(markers[0].MapId, id));
        var manager = new TaskManager(assets);
        manager.Load([(TaskAssets.QuestMain, taskId, stepId, Array.Empty<(ulong, uint, uint)>())], []);

        var settled = manager.OnMapEntered(map);

        Assert.Equal(2, settled.Count);
        Assert.All(settled, result => Assert.True(result.Recorded));
        Assert.Equal(stepId, manager.TaskDataOf(TaskAssets.QuestMain, taskId)!.CurrentStep.StepId);
        Assert.False(manager.IsFinished(TaskAssets.QuestMain, taskId));
        Assert.Empty(manager.OnMapEntered(map));
        var report = manager.ReportAction(TaskAssets.QuestMain, dialogue.Id, 1, map);
        Assert.Equal(0, report.Code);
        Assert.True(report.Result!.StepAdvanced);
    }
}
