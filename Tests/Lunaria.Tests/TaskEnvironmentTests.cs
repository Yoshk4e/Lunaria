using System.Globalization;
using Lunaria.Game.Player;
using Lunaria.Game.Resources;
using Lunaria.Game.Resources.Tables;
using Lunaria.Game.Tasks;
using Msg;
using Xunit;

namespace Lunaria.Tests;

[Collection("bundled-gameplay")]
public sealed class TaskEnvironmentTests(BundledGameplayFixture fixture)
{
    private GameData Assets => fixture.Data;

    private void AtActions(Player player, params ulong[] actions) => player.Tasks.Load(actions.Select(action => {
        var step = Assets.Tasks.StepOfAction(TaskAssets.QuestMain, action);
        return (TaskAssets.QuestMain, Assets.Tasks.TaskOfStep(TaskAssets.QuestMain, step), step,
            Array.Empty<(ulong, uint, uint)>().AsEnumerable());
    }), []);

    [Fact]
    public void EveryReachableTimeSetting_AppliesAfterCompletionAndUsesClientMinutesAndWeather()
    {
        var count = 0;
        foreach (var row in fixture.Rows("P_TaskSteps_QuestMain"))
        {
            if (!row.TryGetProperty("pushGameTime", out var push)) continue;
            var step = row.GetProperty("id").GetUInt64();
            var task = Assets.Tasks.TaskOfStep(TaskAssets.QuestMain, step);
            if (task == 0) continue;
            var actionId = Assert.Single(row.GetProperty("actions").EnumerateArray()).GetUInt64();
            var action = Assets.Tasks.Action(TaskAssets.QuestMain, actionId)!;
            var player = new Player(1, Assets);
            AtActions(player, actionId);
            player.LoadGameTime(23 * 60);
            player.LoadWeather((uint)WeatherType.Rainy);
            var query = player.ReportTaskAction(TaskAssets.QuestMain, actionId, 0);
            Assert.Empty(query.Outcome!.AllNotifications);
            Assert.Equal(23 * 60u, player.GameTimeMinutes);
            Assert.Equal(WeatherType.Rainy, player.CurrentWeather);
            switch ((ServerTarget)action.ServerTargetType)
            {
                case ServerTarget.ArriveMap:
                    TargetParameter.TryId(action.ServerParam1, out var map);
                    Assert.Equal(0, player.Map.BeginEnter(map, 0).Code);
                    Assert.Equal(0, player.Map.FinishEnter());
                    break;
                case ServerTarget.ResetMonster:
                    Assert.Equal(0, player.Map.BeginEnter(100001001001, 0).Code);
                    Assert.Equal(0, player.Map.FinishEnter());
                    break;
                case ServerTarget.CompleteTask:
                    TargetParameter.TryCount(action.ServerParam1, out var required);
                    player.Tasks.Load([(TaskAssets.QuestMain, task, step, Array.Empty<(ulong, uint, uint)>().AsEnumerable())],
                        [(TaskAssets.QuestMain, required)]);
                    break;
                case ServerTarget.ReachGameTime:
                    TargetParameter.TryCount(action.ServerParam1, out var minute);
                    player.LoadGameTime(minute);
                    break;
            }
            var before = player.GameTimeMinutes;
            var result = player.ReportTaskAction(TaskAssets.QuestMain, actionId, 1);
            Assert.Equal(0, result.Code);
            Assert.True(result.Outcome!.Progress.StepAdvanced, $"Step {step} did not complete");
            Assert.Contains(step, result.Outcome.Progress.PassedSteps);
            var expectedMinute = (uint)(decimal.Parse(push[1].GetString()!, CultureInfo.InvariantCulture) * 60) % 1440;
            var expectedWeather = row.TryGetProperty("setGameWeather", out var weather)
                ? (WeatherType)weather.GetUInt32() : WeatherType.Rainy;
            Assert.Equal(expectedMinute, player.GameTimeMinutes);
            Assert.Equal(expectedWeather, player.CurrentWeather);
            var slips = result.Outcome.AllNotifications.OfType<SCGameTimeSlipNtf>().ToArray();
            if (expectedMinute != before || expectedWeather != WeatherType.Rainy)
            {
                var slip = Assert.Single(slips);
                Assert.Equal(expectedMinute, slip.SlipToTime);
                Assert.Equal((uint)expectedWeather, slip.CurWeather);
                Assert.Equal((expectedMinute + 1440 - before) % 1440, slip.PassTime);
            }
            else Assert.Empty(slips);
            Assert.Empty(player.ReportTaskAction(TaskAssets.QuestMain, actionId, 1).Outcome!.AllNotifications);
            count++;
        }
        Assert.Equal(14, count);
    }

    [Fact]
    public void WeatherOnlyStep_ChangesWeatherWithoutMovingTheClock()
    {
        var player = new Player(1, Assets);
        AtActions(player, 110010001);
        player.LoadGameTime(330);
        player.LoadWeather((uint)WeatherType.Foggy);
        var outcome = player.ReportTaskAction(TaskAssets.QuestMain, 110010001, 1).Outcome!;
        var slip = Assert.Single(outcome.AllNotifications.OfType<SCGameTimeSlipNtf>());
        Assert.Equal(330u, slip.SlipToTime);
        Assert.Equal(0u, slip.PassTime);
        Assert.Equal((uint)WeatherType.AfterRain, slip.CurWeather);
        Assert.Equal(WeatherType.AfterRain, player.CurrentWeather);
    }

    [Fact]
    public void FailedStepAndCleanup_DoNotApplySuccessfulCompletionEnvironment()
    {
        var player = new Player(1, Assets);
        AtActions(player, 110030401);
        player.LoadGameTime(330);
        player.LoadWeather((uint)WeatherType.Foggy);
        Assert.Empty(player.ReportTaskAction(TaskAssets.QuestMain, 110030402, 1).Outcome!.AllNotifications);
        var failure = player.ReportTaskAction(TaskAssets.QuestMain, 110030403, 1).Outcome!;
        Assert.True(failure.Progress.TaskFailed);
        Assert.Empty(failure.AllNotifications.OfType<SCGameTimeSlipNtf>());
        Assert.Equal(330u, player.GameTimeMinutes);
        Assert.Equal(WeatherType.Foggy, player.CurrentWeather);
    }

    [Fact]
    public void MapArrivalPolling_AppliesEnvironmentAndCrossesAnotherActiveTimeTarget()
    {
        var player = new Player(1, Assets);
        AtActions(player, 110012102, 310030111);
        player.LoadGameTime(23 * 60);
        player.LoadWeather((uint)WeatherType.Foggy);
        player.Map.BeginEnter(100001001001, 0);
        player.Map.FinishEnter();
        var outcomes = player.SettleServerTargets();
        var arrivalIndex = outcomes.ToList().FindIndex(o => o.Progress.TaskId == 11001);
        var timeIndex = outcomes.ToList().FindIndex(o => o.Progress.SettledActions.Any(a => a.ActionId == 310030111));
        Assert.True(arrivalIndex >= 0 && timeIndex > arrivalIndex);
        var slip = Assert.Single(outcomes.SelectMany(o => o.AllNotifications).OfType<SCGameTimeSlipNtf>());
        Assert.Equal(1020u, slip.SlipToTime);
        Assert.Equal(1080u, slip.PassTime);
        Assert.Equal((uint)WeatherType.AfterRain, slip.CurWeather);
        Assert.Empty(player.SettleServerTargets());
    }

    [Fact]
    public void SameTimeAndWeather_DoNotAddADayOrRepeatTheSlip()
    {
        var player = new Player(1, Assets);
        AtActions(player, 110030401);
        player.LoadGameTime(720);
        player.LoadWeather((uint)WeatherType.Sunny);
        var result = player.ReportTaskAction(TaskAssets.QuestMain, 110030401, 1);
        Assert.True(result.Outcome!.Progress.StepAdvanced);
        Assert.Empty(result.Outcome.AllNotifications.OfType<SCGameTimeSlipNtf>());
        Assert.Equal(720u, player.GameTimeMinutes);
    }

    [Fact]
    public void SeekingPastClientSteps_AppliesTheirEnvironmentInOrder()
    {
        var player = new Player(1, Assets);
        AtActions(player, 110030401);
        player.LoadGameTime(600);
        player.LoadWeather((uint)WeatherType.Foggy);
        var result = player.ReportTaskAction(TaskAssets.QuestMain, 1100304001, 1).Outcome!;
        Assert.Equal(new ulong[] { 1100304, 11003040 }, result.Progress.PassedSteps);
        var slip = Assert.Single(result.AllNotifications.OfType<SCGameTimeSlipNtf>());
        Assert.Equal(720u, slip.SlipToTime);
        Assert.Equal(120u, slip.PassTime);
        Assert.Equal((uint)WeatherType.Sunny, slip.CurWeather);
    }

    [Fact]
    public void InvalidWeather_RejectsTheWholeTimeRequestWithoutChangingState()
    {
        var player = new Player(1, Assets);
        player.LoadGameTime(600);
        player.LoadWeather((uint)WeatherType.Foggy);
        player.ClearSaveDirty();
        Assert.NotEqual(0, player.SetupGameTime(300, uint.MaxValue).Result);
        Assert.Equal(600u, player.GameTimeMinutes);
        Assert.Equal(WeatherType.Foggy, player.CurrentWeather);
        Assert.False(player.SaveDirty);
    }

    private sealed class FixedWeatherRoll(long value) : Random
    {
        public override long NextInt64(long maxValue)
        {
            Assert.InRange(value, 0, maxValue - 1);
            return value;
        }
    }

    [Theory]
    [InlineData(0, WeatherType.Sunny)]
    [InlineData(59, WeatherType.Sunny)]
    [InlineData(60, WeatherType.AfterRain)]
    [InlineData(99, WeatherType.AfterRain)]
    public void RandomWeather_UsesTheTableWeightsAcrossAllPeriodBoundaries(long roll, WeatherType expected)
    {
        foreach (var minute in new uint[] { 0, 359, 360, 599, 600, 1019, 1020, 1139, 1140, 1439 })
        {
            var player = new Player(1, Assets);
            var start = player.GameTimeMinutes;
            var reply = player.SetupGameTime(minute, 0, new FixedWeatherRoll(roll));
            Assert.Equal(0, reply.Result);
            Assert.Equal((uint)expected, reply.Weather);
            Assert.Equal(expected, player.CurrentWeather);
            // This is elapsed time added to the role's initial Tod, not an absolute clock.
            Assert.Equal((start + minute) % 1440, player.GameTimeMinutes);
        }
    }

    [Theory]
    [InlineData("1", "17")]
    [InlineData("2", "25")]
    [InlineData("2", "-1")]
    [InlineData("2", "NaN")]
    public void UnsupportedTimeSettings_FailResourceValidation(string mode, string value) =>
        Assert.Throws<ResourceException>(() => WorldTimeRules.ParseStep(new PTaskStepsQuestMain {
            Id = 1, PushGameTime = [mode, value]
        }));
}
