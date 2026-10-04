using Lunaria.Game.Player.Managers;
using Lunaria.Game.Resources;
using Lunaria.Game.Resources.Tables;
using Xunit;

namespace Lunaria.Tests;

public sealed class SignInIsolationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private static SignInManager Create()
    {
        var activities = new uint[] { 3, 4 }.ToDictionary(id => id.ToString(), id => new PActivityTable {
            Id = id, TimeOffsetStart = (ulong)Now.AddDays(-1).ToUnixTimeSeconds(),
            TimeOffsetStop = (ulong)Now.AddDays(10).ToUnixTimeSeconds()
        });
        var rewards = activities.Values.SelectMany(a => new uint[] { 1, 2 }.Select(day => new PSignInActivityRewardTable {
            Id = a.Id * 10 + day, ActivityId = a.Id, Day = day, Items = ["1=10"]
        })).ToDictionary(r => r.Id.ToString());
        return new SignInManager(new SignInAssets(activities, rewards));
    }

    [Fact]
    public void MovedWindow_ReopensAnEndedActivityWithTheSameLength()
    {
        var activities = new Dictionary<string, PActivityTable> {
            ["3"] = new() { Id = 3, TimeOffsetStart = (ulong)Now.AddDays(-100).ToUnixTimeSeconds(),
                TimeOffsetStop = (ulong)Now.AddDays(-90).ToUnixTimeSeconds() }
        };
        var rewards = new Dictionary<string, PSignInActivityRewardTable> {
            ["31"] = new() { Id = 31, ActivityId = 3, Day = 1, Items = ["1=10"] }
        };
        Assert.False(new SignInManager(new SignInAssets(activities, rewards)).Query(3, Now).Data!.SigninDatas[0].IsSignedIn);

        var moved = new SignInAssets(activities, rewards,
            new Dictionary<uint, ActivityWindowPolicy> { [3] = new() { Start = Now.AddHours(-1) } });

        Assert.Equal((ulong)Now.AddHours(-1).ToUnixTimeSeconds(), moved.Activity(3)!.TimeOffsetStart);
        Assert.Equal((ulong)Now.AddHours(-1).AddDays(10).ToUnixTimeSeconds(), moved.Activity(3)!.TimeOffsetStop);
        Assert.True(new SignInManager(moved).Query(3, Now).Data!.SigninDatas[0].IsSignedIn);
    }

    [Fact]
    public void Activities_DoNotShareAttendanceClaimsOrDailyClock()
    {
        var manager = Create();
        manager.Query(3, Now);
        Assert.False(manager.HasClaimableDay(4));
        Assert.NotEqual(0, manager.Claim(4, 1).Result);
        Assert.Equal(0, manager.Claim(3, 1).Result);
        var second = manager.Query(4, Now).Data!;
        Assert.True(second.SigninDatas[0].IsSignedIn);
        Assert.False(second.SigninDatas[0].HasClaimed);
        Assert.Equal(0, manager.Claim(4, 1).Result);
        manager.Query(3, Now.AddDays(1));
        Assert.False(manager.HasClaimableDay(4));
        Assert.False(manager.Query(4, Now).Data!.SigninDatas[1].IsSignedIn);
        Assert.True(manager.Query(4, Now.AddDays(1)).Data!.SigninDatas[1].IsSignedIn);
    }

    [Fact]
    public void Reload_ReplacesAllCalendarsAndKeepsTheirOwnClock()
    {
        var source = Create();
        source.Query(3, Now);
        source.Claim(3, 1);
        source.Query(4, Now.AddDays(1));
        var restored = Create();
        restored.Query(4, Now.AddDays(8));
        restored.LoadActivities(source.Activities);
        Assert.False(restored.IsDirty);
        Assert.True(restored.Query(3, Now).Data!.SigninDatas[0].HasClaimed);
        Assert.False(restored.Query(4, Now).Data!.SigninDatas[0].HasClaimed);
        Assert.False(restored.Query(4, Now.AddDays(1)).Data!.SigninDatas[1].IsSignedIn);
        Assert.True(restored.Query(3, Now.AddDays(1)).Data!.SigninDatas[1].IsSignedIn);
        Assert.True(restored.Query(4, Now.AddDays(2)).Data!.SigninDatas[1].IsSignedIn);
        restored.LoadActivities([]);
        Assert.Empty(restored.Activities);
    }

    [Fact]
    public void LegacySave_BelongsToOriginalCalendarRegardlessOfFirstQuery()
    {
        var manager = Create();
        manager.Load([1], [1], Now.ToUnixTimeSeconds() / 86400);
        Assert.False(manager.HasClaimableDay(4));
        var other = manager.Query(4, Now).Data!;
        Assert.True(other.SigninDatas[0].IsSignedIn);
        Assert.False(other.SigninDatas[0].HasClaimed);
        var original = manager.Query(3, Now).Data!;
        Assert.True(original.SigninDatas[0].HasClaimed);
        Assert.False(original.SigninDatas[1].IsSignedIn);
        Assert.NotEqual(0, manager.Claim(3, 1).Result);
    }

    [Fact]
    public void Load_DropsUnknownDaysAndActivitiesAndUnsignedClaims()
    {
        var manager = Create();
        manager.LoadActivities([new(3, [1, 999], [1, 2, 999], Now.ToUnixTimeSeconds() / 86400),
            new(999, [1], [1], null)]);
        var state = Assert.Single(manager.Activities);
        Assert.Equal(new uint[] { 1 }, state.SignedDays);
        Assert.Equal(new uint[] { 1 }, state.ClaimedDays);
        manager.ClearDirty();
        Assert.NotEqual(0, manager.Claim(4, 1).Result);
        Assert.NotEqual(0, manager.Query(999, Now).Result);
        Assert.False(manager.IsDirty);
    }
}
