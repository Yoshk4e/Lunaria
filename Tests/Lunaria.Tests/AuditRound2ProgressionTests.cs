using Lunaria.Game.Player;
using Lunaria.Game.Player.Persistence.Saves;
using System.Text.Json;
using Lunaria.Game.Resources;
using Msg;
using Xunit;

namespace Lunaria.Tests;

[Collection("bundled-gameplay")]
[Trait("Category", "AuditRound2")]
public sealed class AuditRound2ProgressionTests(BundledGameplayFixture fixture)
{
    [Fact]
    public void SixtyDailySignIns_CompleteSixtyDayAttendanceAchievement()
    {
        var assets = fixture.Data;
        var player = new Player(1, assets);
        const uint activityId = 3;
        const uint achievementId = 701001;
        var achievement = assets.Achievements.Get(achievementId)!;
        Assert.Contains(achievement.FinishId, assets.Unlocks.EventsOfSubType(GlobalEventSub.PassDay));
        var requiredDays = assets.Achievements.NeedCount(achievement.FinishId);
        Assert.Equal(60u, requiredDays);
        Assert.Equal(5, assets.SignIn.Days(activityId).Count);
        var activity = assets.SignIn.Activity(activityId)!;
        var start = DateTimeOffset.FromUnixTimeSeconds((long)activity.TimeOffsetStart).AddHours(1);
        Assert.True(start.AddDays(requiredDays - 1).ToUnixTimeSeconds() <= (long)activity.TimeOffsetStop);

        for (var day = 0; day < requiredDays; day++)
        {
            Assert.Equal(0, player.SignIn.Query(activityId, start.AddDays(day)).Result);
            // Login uses the same attendance reconciliation as Player.QuerySignIn.
            player.CompleteRoleLogin();
        }

        Assert.Equal((ulong)requiredDays, player.Achievements.ProgressOf(achievement.FinishId));
        Assert.Equal(0, player.ClaimAchievementRewards([achievementId]).Code);
    }

    [Fact]
    public void RepeatedLoginAndSignInOnSameDay_CountAttendanceOnce()
    {
        var assets = fixture.Data;
        var player = new Player(1, assets);
        var activity = assets.SignIn.Activity(3)!;
        var day = DateTimeOffset.FromUnixTimeSeconds((long)activity.TimeOffsetStart).AddHours(1);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            Assert.Equal(0, player.SignIn.Query(3, day).Result);
            player.CompleteRoleLogin();
        }

        Assert.Equal(1ul, player.Achievements.ProgressOf(701001));
        Assert.Single(player.SignIn.SignedDays);
        Assert.NotEqual(0, player.ClaimAchievementRewards([701001]).Code);
    }

    [Fact]
    public void FourMatchingCreatures_CompleteDayfairFourCreatureObjectiveAndFinalReward()
    {
        var assets = fixture.Data;
        var player = new Player(1, assets);
        const ulong subRegionId = 100001004002;
        const uint sequenceId = 1142079601;
        var sequence = assets.RegionProgress.SequenceRow(subRegionId, sequenceId)!;
        Assert.Equal(6u, sequence.Type);
        Assert.Equal(4u, sequence.ParamNum);
        Assert.Equal(new uint[] { 81001, 81002, 81003 }, sequence.ParamId);

        // Set up the preceding exploration: every other objective is already complete.
        foreach (var other in assets.RegionProgress.Sequences(subRegionId).Where(id => id != sequenceId))
            player.RegionProgress.SetSequence(subRegionId, other,
                assets.RegionProgress.SequenceRow(subRegionId, other)!.ParamNum);
        Assert.Equal(0, player.ClaimRegionRewards(subRegionId).Code);
        Assert.Contains(100u, assets.RegionProgress.Rewards(subRegionId).Select(reward => reward.PrograssValue));

        // These bundled item IDs collect four creatures across all three permitted growth IDs.
        player.GrantRewards([new ItemGrant(29900001, 2), new ItemGrant(29900006, 1), new ItemGrant(29900011, 1)],
            EnmItemReason.EnmItemChangeNormal);
        Assert.Equal(4, player.SilverCreatures.Creatures.Count);
        Assert.Equal(4, sequence.ParamId.Sum(player.SilverCreatures.CountOfGrowth));
        player.RecalculateRegionProgress();

        Assert.Equal(100u, player.RegionProgress.CompletionPercent(subRegionId));
        var claim = player.ClaimRegionRewards(subRegionId);
        Assert.Equal(0, claim.Code);
        Assert.Equal(new uint[] { 100 }, claim.Values);
    }

    [Fact]
    public void AttendanceAfterRewardCalendarEnds_SurvivesSaveWithoutCountingTheSameDayTwice()
    {
        var player = new Player(1, fixture.Data);
        var start = DateTimeOffset.FromUnixTimeSeconds((long)fixture.Data.SignIn.Activity(3)!.TimeOffsetStart).AddHours(1);
        for (var day = 0; day < 8; day++) player.SignIn.Query(3, start.AddDays(day));
        player.CompleteRoleLogin();
        Assert.Equal(8u, player.SignIn.AttendanceDays);
        var saved = JsonSerializer.Deserialize<RoleSaveDocument>(JsonSerializer.Serialize(RoleSaveMapper.Capture(player)))!;
        var restored = new Player(2, fixture.Data);
        RoleSaveMapper.Apply(restored, saved);
        restored.SignIn.Query(3, start.AddDays(7));
        restored.SignIn.Query(3, start.AddDays(6));
        Assert.Equal(8u, restored.SignIn.AttendanceDays);
        restored.SignIn.Query(3, start.AddDays(10));
        restored.CompleteRoleLogin();
        Assert.Equal(9u, restored.SignIn.AttendanceDays);
        Assert.Equal(9ul, restored.Achievements.ProgressOf(701001));
        Assert.Equal(5, restored.SignIn.SignedDays.Count);
    }

    [Fact]
    public void ExtraCreatures_StopAtTheSilvercraftObjective_AndLoadedCountsAreCapped()
    {
        var assets = fixture.Data;
        var player = new Player(1, assets);
        const ulong subRegionId = 100001004002;
        const uint sequenceId = 1142079601;
        var paramNum = assets.RegionProgress.SequenceRow(subRegionId, sequenceId)!.ParamNum;

        player.GrantRewards([new ItemGrant(29900001, 3), new ItemGrant(29900006, 2), new ItemGrant(29900011, 2)],
            EnmItemReason.EnmItemChangeNormal);
        player.RecalculateRegionProgress();
        Assert.Equal(paramNum, player.RegionProgress.Subregions[subRegionId].Sequences[sequenceId]);

        var restored = new Player(2, assets);
        restored.RegionProgress.Load([(subRegionId, [(sequenceId, 16u)], [])]);
        Assert.Equal(paramNum, restored.RegionProgress.Subregions[subRegionId].Sequences[sequenceId]);
    }

    [Fact]
    public void UnlockedCognitoPin_CountsOnlyInItsOwnSubRegion()
    {
        var assets = fixture.Data;
        var player = new Player(1, assets);
        Assert.Equal(100001001002ul, assets.Maps.Teleport(11422401)!.MapId);

        Assert.Equal(0, player.UnlockTeleport(11422401));

        Assert.Equal(1u, player.RegionProgress.Subregions[100001001002].Sequences[1112012401]);
        Assert.False(player.RegionProgress.Subregions.TryGetValue(100001004002, out var southChurch)
            && southChurch.Sequences.GetValueOrDefault(1142012401u) > 0);
    }

    [Fact]
    public void UnrelatedCreature_DoesNotAdvanceDayfairFourCreatureObjective()
    {
        var player = new Player(1, fixture.Data);
        player.GrantRewards([new ItemGrant(29900016, 1)], EnmItemReason.EnmItemChangeNormal);
        Assert.Single(player.SilverCreatures.Creatures);
        player.RecalculateRegionProgress();
        Assert.False(player.RegionProgress.Subregions.TryGetValue(100001004002, out var region)
            && region.Sequences.GetValueOrDefault(1142079601u) > 0);
    }
}
