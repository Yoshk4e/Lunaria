using Lunaria.Game.Mail;
using Lunaria.Game.Player;
using Lunaria.Game.Player.Gameplay;
using Lunaria.Game.Resources;
using Msg;
using Xunit;

namespace Lunaria.Tests;

[Collection("bundled-gameplay")]
public sealed class OperationClockTests(BundledGameplayFixture fixture)
{
    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void RewardEnumerationCrossingMidnight_UsesOneTimestampForAllDeliveryAndHooks()
    {
        var before = new DateTimeOffset(2026, 10, 1, 23, 59, 59, TimeSpan.Zero);
        var clock = new ManualClock(before);
        var player = new Player(1, fixture.Data, clock);
        player.DailyMissions.ToDailyMissionData();
        IEnumerable<ItemGrant> Rewards()
        {
            yield return new(12031001, 1);
            clock.Now = before.AddSeconds(2);
            yield return new(12031001, 1);
        }
        player.GrantRewards(Rewards(), EnmItemReason.EnmItemChangeNormal);
        Assert.Equal(2, player.Motives.Count);
        Assert.All(player.Motives.All, m => Assert.Equal((ulong)before.ToUnixTimeSeconds(), m.ClaimTime));
        Assert.Equal(before.Date, player.DailyMissions.DayAnchor.Date);
        Assert.Equal(clock.Now, player.UtcNow);
        player.AdvanceTime(clock.Now);
        Assert.Equal(clock.Now.Date, player.DailyMissions.DayAnchor.Date);
    }

    [Fact]
    public void NestedClaimAndRoleReplacement_ShareOuterTime_ThenNextOperationExpiresMail()
    {
        var before = new DateTimeOffset(2026, 10, 1, 23, 59, 59, TimeSpan.Zero);
        var clock = new ManualClock(before);
        var player = new Player(1, fixture.Data, clock);
        player.Mails.Load([
            new MailEntry { MailId = 1, Items = [new(1, 3)], ExpireTime = (uint)before.AddSeconds(1).ToUnixTimeSeconds() },
            new MailEntry { MailId = 2, Items = [new(1, 4)], ExpireTime = (uint)before.AddSeconds(1).ToUnixTimeSeconds() }
        ]);
        using (player.BeginOperation())
        {
            clock.Now = before.AddSeconds(2);
            Assert.Equal(before, player.CreateRoleSession().UtcNow);
            Assert.Equal(0, player.ClaimMailAttachments(1).Code);
        }
        Assert.NotEqual(0, player.ClaimMailAttachments(2).Code);
        Assert.Equal(3UL, player.OwnedItemCount(1));
    }

    [Fact]
    public void WeatherRolls_DoNotChangeSeededWantedRoute()
    {
        Player Fresh() => new(1, fixture.Data, random: new GameplayRandom(wanted: new Random(31), weather: new Random(42)));
        var first = Fresh();
        var second = Fresh();
        first.GrantStarterState();
        second.GrantStarterState();
        for (var i = 0; i < 10; i++) first.SetupGameTime(60, 0);
        Assert.Equal(0, first.EnterWanted(10101));
        Assert.Equal(0, second.EnterWanted(10101));
        Assert.Equal(first.Wanted.CurrentEventId, second.Wanted.CurrentEventId);
        Assert.Equal(first.Wanted.CaptureRun()!.RouteId, second.Wanted.CaptureRun()!.RouteId);
    }
}
