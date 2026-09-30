using Lunaria.Game.Battle;
using Lunaria.Game.Characters;
using Lunaria.Game.Mail;
using Lunaria.Game.Player;
using Lunaria.Game.Resources;
using Msg;
using Xunit;

namespace Lunaria.Tests;

[Collection("bundled-gameplay")]
public sealed class ServerAuditTests(BundledGameplayFixture fixture)
{
    [Fact]
    public void LargeStaminaGrant_SaturatesWithoutGoingNegative()
    {
        var player = new Player(1, fixture.Data);
        var now = DateTimeOffset.UtcNow;
        player.Progress.Load(1, 0, 0, 1, now);
        Assert.Equal(0, player.Progress.AddStamina(int.MaxValue, now));
        Assert.Equal(player.Progress.StaminaMax, player.Progress.Stamina);
        Assert.NotEqual(0, player.Progress.AddStamina(1, now));
        Assert.Equal(player.Progress.StaminaMax, player.Progress.Stamina);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void LargeLiquidGrant_FillsTheGaugeInsteadOfEmptyingIt(int element)
    {
        var initial = TeamLiquid.Empty.Add(element, 1);
        Assert.Equal(TeamLiquid.Empty.Add(element, TeamLiquid.MaxBasisPoints), initial.Add(element, int.MaxValue));
        Assert.Equal(TeamLiquid.Empty, initial.Add(element, int.MinValue));
    }

    [Fact]
    public void MaximumAchievementIncrement_RemainsFinishedAndDoesNotWrap()
    {
        var player = new Player(1, fixture.Data);
        var achievement = fixture.Data.Achievements.All.First();
        var threshold = fixture.Data.Achievements.NeedCount(achievement.FinishId);
        Assert.Equal(0, player.Achievements.AddProgress(achievement.FinishId, 1).Result);
        Assert.Equal(0, player.Achievements.AddProgress(achievement.FinishId, uint.MaxValue).Result);
        Assert.Equal((ulong)threshold, player.Achievements.ProgressOf(achievement.FinishId));
        Assert.True(player.Achievements.IsEventFinished(achievement.FinishId));
        Assert.False(player.Achievements.AddProgress(achievement.FinishId, uint.MaxValue).Changed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExpiredMail_CannotPayAttachmentsBeforeTheNextScheduledSweep(bool claimAll)
    {
        var player = new Player(1, fixture.Data);
        player.Mails.Load([
            new MailEntry { MailId = 1, ExpireTime = (uint)DateTimeOffset.UtcNow.AddSeconds(-1).ToUnixTimeSeconds(),
                Items = [new ItemGrant(1, 42)] },
            new MailEntry { MailId = 2, ExpireTime = 0, Items = [new ItemGrant(1, 7)] }
        ]);
        if (claimAll)
        {
            var result = player.ClaimAllMailAttachments();
            Assert.Equal(new uint[] { 2 }, result.ClaimedIds);
            Assert.Equal(7, player.Wallet.Balance(1));
        }
        else
        {
            Assert.NotEqual(0, player.ClaimMailAttachments(1).Code);
            Assert.Equal(0, player.Wallet.Balance(1));
        }
    }

    [Fact]
    public void ReenteringBattle_MustMatchTheWholeIdentity()
    {
        var battles = new BattleManager();
        var type = EBattleType.EnmBattleTypeExpose;
        Assert.Equal(0, battles.Enter(type, 123, 10, default));
        Assert.Equal(0, battles.Start(type, 123));
        var before = battles.Current;
        Assert.Equal(0, battles.Enter(type, 123, 10, default));
        Assert.NotEqual(0, battles.Enter(type, 123, 11, default));
        Assert.Equal(before, battles.Current);
        Assert.NotEqual(0, battles.Enter(type, 123, 10, (EnmMonsterFromType)int.MaxValue));
        Assert.Equal(before, battles.Current);
    }
}
