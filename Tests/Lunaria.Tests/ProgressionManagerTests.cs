using Lunaria.Game.Player;
using Lunaria.Game.Player.Managers;
using Lunaria.Game.Resources;
using Xunit;

namespace Lunaria.Tests;

[Collection("bundled-gameplay")]
public sealed class ProgressionManagerTests(BundledGameplayFixture fixture)
{
    private GameData Assets => fixture.Data;
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void DailyMission_UnrelatedEventStillReportsTheDayReset()
    {
        var clock = new ManualClock(Now);
        var manager = new DailyMissionManager(Assets, clock);
        var mission = Assets.DailyMissions.Missions.First();
        Assert.True(manager.AddEventProgress(mission.Event, uint.MaxValue));
        Assert.Equal(0, manager.ClaimActivePoint(mission.Id).Result);
        clock.Now = Now.AddDays(1);
        Assert.True(manager.AddEventProgress(uint.MaxValue, 1));
        Assert.Empty(manager.Missions);
        Assert.Equal(0u, manager.ActivePoint);
        Assert.False(manager.AddEventProgress(uint.MaxValue, 1));
    }

    [Fact]
    public void DailyMission_ZeroProgressDoesNotInventMissionState()
    {
        var manager = new DailyMissionManager(Assets, new ManualClock(Now));
        manager.AdvanceTime(Now);
        manager.ClearDirty();
        Assert.False(manager.AddEventProgress(Assets.DailyMissions.Missions.First().Event, 0));
        Assert.Empty(manager.Missions);
        Assert.False(manager.IsDirty);
    }

    [Fact]
    public void DailyMission_PointClaimsAndRewardClaimsResetTogetherAndSurviveReload()
    {
        var clock = new ManualClock(Now);
        var manager = new DailyMissionManager(Assets, clock);
        uint expectedPoints = 0;
        foreach (var mission in Assets.DailyMissions.Missions)
        {
            Assert.NotEqual(0, manager.ClaimActivePoint(mission.Id).Result);
            manager.AddEventProgress(mission.Event, uint.MaxValue);
            Assert.Equal(0, manager.ClaimActivePoint(mission.Id).Result);
            expectedPoints += mission.ActivePoint;
            Assert.NotEqual(0, manager.ClaimActivePoint(mission.Id).Result);
        }
        Assert.Equal(expectedPoints, manager.ActivePoint);
        foreach (var reward in manager.EligibleRewards()) manager.MarkRewardClaimed(reward.Id);
        Assert.Empty(manager.EligibleRewards());
        var restored = new DailyMissionManager(Assets, clock);
        restored.Load(manager.DayAnchor.ToUnixTimeSeconds(), manager.Missions.Values.Select(m => (m.MissionId, m.Progress, m.Claimed)),
            manager.ActivePoint, manager.ClaimedRewards);
        Assert.Equal(manager.ToDailyMissionData(), restored.ToDailyMissionData());
        clock.Now = Now.AddDays(-1);
        Assert.False(restored.AdvanceTime(clock.Now));
        Assert.Equal(expectedPoints, restored.ActivePoint);
        clock.Now = Now.AddDays(1);
        Assert.True(restored.AdvanceTime(clock.Now));
        Assert.Empty(restored.Missions);
        Assert.Empty(restored.ClaimedRewards);
        Assert.Equal(0u, restored.ActivePoint);
        Assert.False(restored.AdvanceTime(clock.Now));
    }

    [Fact]
    public void DailyMission_LargeLoadedPointBalanceCannotWrapWhenClaiming()
    {
        var manager = new DailyMissionManager(Assets, new ManualClock(Now));
        var mission = Assets.DailyMissions.Missions.First();
        manager.Load(Now.ToUnixTimeSeconds(), [(mission.Id, uint.MaxValue, false)], uint.MaxValue, []);
        Assert.Equal(0, manager.ClaimActivePoint(mission.Id).Result);
        Assert.Equal(uint.MaxValue, manager.ActivePoint);
    }

    [Fact]
    public void BattlePass_MixedUnknownQueryIsRejectedBeforeSeedingAnyPass()
    {
        var manager = new BattlePassManager(Assets);
        var response = manager.ToBattlePassData([1001, uint.MaxValue]);
        Assert.NotEqual(0, response.Result);
        Assert.Empty(response.Datas);
        Assert.Empty(manager.Passes);
        Assert.False(manager.IsDirty);
    }

    [Fact]
    public void BattlePass_ZeroExperienceDoesNotCreateStateOrReportAChange()
    {
        var manager = new BattlePassManager(Assets);
        Assert.False(manager.AddExp(1001, 0).Changed);
        Assert.Empty(manager.Passes);
        Assert.False(manager.IsDirty);
    }

    [Fact]
    public void BattlePass_UnknownAndUnearnedClaimsCannotPoisonTheRewardPrefix()
    {
        var manager = new BattlePassManager(Assets);
        Assert.Empty(manager.ClaimableLevels(uint.MaxValue));
        manager.MarkAwardClaimed(uint.MaxValue, 1);
        Assert.Empty(manager.Passes);
        manager.AddExp(1001, 150);
        var before = manager.Passes[1001];
        manager.MarkAwardClaimed(1001, uint.MaxValue);
        Assert.Equal(before, manager.Passes[1001]);
        Assert.NotEmpty(manager.ClaimableLevels(1001));
    }

    [Fact]
    public void BattlePass_ClaimedRewardsRemainClaimedAfterReloadAndLaterExperience()
    {
        var player = new Player(1, Assets);
        player.BattlePasses.AddExp(1001, 150);
        var first = player.ClaimBattlePassAwards(1001);
        Assert.Equal(0, first.Code);
        var state = player.BattlePasses.Passes[1001];
        var restored = new Player(2, Assets);
        restored.BattlePasses.Load([(1001, state.Level, state.Exp, state.AwardLevel)]);
        Assert.NotEqual(0, restored.ClaimBattlePassAwards(1001).Code);
        restored.BattlePasses.AddExp(1001, 100);
        Assert.All(restored.BattlePasses.ClaimableLevels(1001), level => Assert.True(level > first.Level));
    }

    [Fact]
    public void SignIn_WindowAndBackwardClockCannotCreateExtraAttendance()
    {
        var manager = new SignInManager(Assets);
        var activity = Assets.SignIn.Activity(3)!;
        var start = DateTimeOffset.FromUnixTimeSeconds((long)activity.TimeOffsetStart);
        var stop = DateTimeOffset.FromUnixTimeSeconds((long)activity.TimeOffsetStop);
        Assert.Equal(0, manager.Query(3, start.AddSeconds(-1)).Result);
        Assert.Empty(manager.SignedDays);
        manager.Query(3, start);
        Assert.Single(manager.SignedDays);
        Assert.Equal(0, manager.Claim(3, 1).Result);
        manager.Query(3, start.AddDays(-1));
        manager.Query(3, stop.AddSeconds(1));
        Assert.Single(manager.SignedDays);
        Assert.False(manager.HasClaimableDay(3));
        Assert.NotEqual(0, manager.Claim(uint.MaxValue, 1).Result);
        Assert.NotEqual(0, manager.Claim(3, uint.MaxValue).Result);
    }

    [Fact]
    public void RegionProgress_LoadClearsPendingUnlocksFromThePreviousState()
    {
        var manager = new RegionProgressManager(Assets);
        var region = Assets.RegionProgress.TrackedSubRegions.First(id => Assets.RegionProgress.Sequences(id).Count > 0);
        Assert.True(manager.SetSequence(region, Assets.RegionProgress.Sequences(region)[0], 1));
        manager.Load([]);
        Assert.Empty(manager.DrainUnlocked());
        Assert.Empty(manager.Subregions);
    }

    [Fact]
    public void RegionProgress_AllConfiguredRewardsRequireProgressAndPayOnceAcrossReload()
    {
        var player = new Player(1, Assets);
        var region = Assets.RegionProgress.TrackedSubRegions.First(id => Assets.RegionProgress.Rewards(id).Count > 0
            && Assets.RegionProgress.Sequences(id).All(s => Assets.RegionProgress.SequenceRow(id, s)?.ParamNum > 0));
        Assert.NotEqual(0, player.ClaimRegionRewards(region).Code);
        foreach (var sequence in Assets.RegionProgress.Sequences(region)) player.RegionProgress.SetSequence(region, sequence, uint.MaxValue);
        Assert.Equal(100u, player.RegionProgress.CompletionPercent(region));
        Assert.Single(player.RegionProgress.DrainUnlocked());
        Assert.Empty(player.RegionProgress.DrainUnlocked());
        Assert.Equal(0, player.ClaimRegionRewards(region).Code);
        var saved = player.RegionProgress.Subregions[region];
        var restored = new Player(2, Assets);
        restored.RegionProgress.Load([(region, saved.Sequences.Select(s => (s.Key, s.Value)), saved.ClaimedValues)]);
        Assert.NotEqual(0, restored.ClaimRegionRewards(region).Code);
        Assert.False(restored.RegionProgress.SetSequence(region, Assets.RegionProgress.Sequences(region)[0], 0));
        Assert.Equal(100u, restored.RegionProgress.CompletionPercent(region));
    }

    [Fact]
    public void Case_PlacingAnAlreadyPlacedClueIsANoop()
    {
        var manager = new CaseManager(Assets);
        Assert.NotNull(manager.OpenCase(1002));
        Assert.True(manager.GiveClue(1002101));
        Assert.Equal(0, manager.PutClue(1002101).Result);
        var before = manager.ToCaseData();
        manager.ClearDirty();
        Assert.Equal(0, manager.PutClue(1002101).Result);
        Assert.Equal(before, manager.ToCaseData());
        Assert.False(manager.IsDirty);
    }

    [Fact]
    public void Case_LoadWithEveryStageCompleteCannotReopenACompletedInvestigation()
    {
        var manager = new CaseManager(Assets);
        var clues = Assets.Cases.Stages(1002).SelectMany(Assets.Cases.StageClues).Distinct().ToArray();
        Assert.NotEmpty(clues);
        manager.Load([(1002u, 0u, clues.AsEnumerable(), Array.Empty<ulong>().AsEnumerable())], []);
        Assert.Contains(1002u, manager.Finished);
        Assert.False(manager.IsProcessing(1002));
        Assert.Null(manager.OpenCase(1002));
        Assert.Equal(0, manager.PutClue(clues[0]).Result);
    }
}
