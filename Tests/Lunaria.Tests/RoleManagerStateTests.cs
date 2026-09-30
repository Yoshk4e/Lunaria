using Lunaria.Game.Characters;
using Lunaria.Game.Player;
using Lunaria.Game.Resources;
using Msg;
using Xunit;

namespace Lunaria.Tests;

public sealed partial class RoleSessionTests
{
    [Fact]
    public async Task TeamLoad_AllDanglingMembersAreRepairedOnLogin()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var player = ctx.Player;
        var saved = player.Teams.CurrentTeam()!;
        player.Teams.Load([saved with { Members = [saved.Members[0] with { InstId = ulong.MaxValue }] }],
            1, 1, player.Characters);
        player.Wallet.Credit(1, 1);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 2));
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        Assert.NotEmpty(ctx.Player.Teams.CurrentMemberInstIds());
        Assert.All(ctx.Player.Teams.CurrentMemberInstIds(), id => Assert.NotNull(ctx.Player.Characters.Get(id)));
    }

    [Fact]
    public async Task TeamLoad_DanglingMemberCannotHideAnOwnedMemberInTheSameSlot()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var player = ctx.Player;
        var team = player.Teams.CurrentTeam()!;
        var owned = team.Members[0];
        player.Teams.Load([team with { Members = [owned with { InstId = ulong.MaxValue }, owned] }],
            1, owned.Slot, player.Characters);
        Assert.Equal(owned, Assert.Single(player.Teams.CurrentTeam()!.Members));
    }

    [Fact]
    public void MonthCard_ExactLimitAndUnknownCardAreRefusedWithoutMutation()
    {
        var player = new Player(1, _assets);
        var now = DateTimeOffset.FromUnixTimeSeconds(1700000000);
        var card = _assets.Charge.MonthCardOf(2001)!;
        player.MonthCards.Load([(card.Id, now.AddDays(card.DaysUplimit - card.ChargeDays).ToUnixTimeSeconds(), now.ToUnixTimeSeconds())]);
        var before = player.MonthCards.Cards[card.Id];
        Assert.Equal((int)EnmTextCode.EnmTextMonthCardDayUplimit, player.MonthCards.Buy(card.Id, now).Result);
        Assert.Equal((int)EnmTextCode.EnmTextMonthCardIdInvalid, player.MonthCards.Buy(uint.MaxValue, now).Result);
        Assert.Equal(before, Assert.Single(player.MonthCards.Cards).Value);
        Assert.False(player.MonthCards.IsDirty);
    }

    [Fact]
    public void MonthCard_FailedPaymentPreservesUnpaidRewardsAndExpiry()
    {
        var player = new Player(1, _assets);
        var now = DateTimeOffset.UtcNow;
        var card = _assets.Charge.MonthCardOf(2001)!;
        var goods = _assets.Charge.Offered.Single(g => g.ChargeType == ChargeAssets.MonthCard && g.SubId == card.Id);
        player.MonthCards.Load([(card.Id, now.AddDays(-2).ToUnixTimeSeconds(), now.AddDays(-4).ToUnixTimeSeconds())]);
        var before = player.MonthCards.Cards[card.Id];
        Assert.NotEqual(0, player.BuyChargeGoods(goods.Id).Code);
        Assert.Equal(before, player.MonthCards.Cards[card.Id]);
        Assert.False(player.MonthCards.IsDirty);
        Assert.Empty(player.DrainGameplayChanges());
        Assert.Equal(2 * card.DayNum, Assert.Single(player.SettleMonthCards(now).OfType<SCMonthCardRewardNtf>()).ItemNum);
    }

    [Fact]
    public void MonthCard_ActiveRenewalExtendsExistingExpiryAndSettlesAccruedDays()
    {
        var player = new Player(1, _assets);
        var now = DateTimeOffset.FromUnixTimeSeconds(1700000000);
        var card = _assets.Charge.MonthCardOf(2001)!;
        player.MonthCards.Load([(card.Id, now.AddDays(5).ToUnixTimeSeconds(), now.AddDays(-3).ToUnixTimeSeconds())]);
        var purchase = player.MonthCards.Buy(card.Id, now);
        Assert.Equal(0, purchase.Result);
        Assert.Equal(now.AddDays(5 + card.ChargeDays), purchase.State!.OverdueAt);
        Assert.Equal(3 * card.DayNum, Assert.Single(purchase.Accrued).Count);
        Assert.Empty(player.MonthCards.DueDailyGrants(now));
        Assert.Equal(card.DayNum, Assert.Single(Assert.Single(player.MonthCards.DueDailyGrants(now.AddDays(1))).Grant).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TeamLoad_SelectsAnOccupiedTeamWhenTheSavedCurrentIsUnusable(bool emptyCurrent)
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var player = ctx.Player;
        var starter = player.Teams.CurrentTeam()!;
        var valid = starter with { TeamId = 2 };
        player.Teams.Load(emptyCurrent ? [starter with { Members = [] }, valid] : [valid],
            1, 4, player.Characters);

        Assert.Equal(2u, player.Teams.Current);
        Assert.NotEmpty(player.CurrentTeamMembers());
        Assert.Contains(player.Teams.CurrentTeam()!.Members, m => m.Slot == player.Teams.UsingMemberSlot);
        player.Wallet.Credit(1, 1);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 2));
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        Assert.Equal(2u, ctx.Player.Teams.Current);
        Assert.Equal(valid.Members, ctx.Player.Teams.CurrentTeam()!.Members);
    }

    [Fact]
    public async Task TeamLoad_InvalidIdsAndDuplicateRowsCannotCrowdOutAValidTeam()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var player = ctx.Player;
        var team = player.Teams.CurrentTeam()!;
        var malformed = Enumerable.Repeat(team with { TeamId = 0 }, TeamManager.MaxTeams)
            .Concat([team, team, team with { TeamId = 2 }, team with { TeamId = uint.MaxValue }]);
        player.Teams.Load(malformed, 1, 1, player.Characters);
        Assert.Equal(new uint[] { 1, 2 }, player.Teams.All.Select(t => t.TeamId));
        Assert.NotEmpty(player.CurrentTeamMembers());
    }

    [Fact]
    public async Task MonthCard_RenewalPaysOldEntitlementOnceAndPersistsTheNewWindow()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var player = ctx.Player;
        var now = DateTimeOffset.UtcNow;
        var card = _assets.Charge.MonthCardOf(2001)!;
        var goods = _assets.Charge.Offered.Single(g => g.ChargeType == ChargeAssets.MonthCard && g.SubId == card.Id);
        var rewardCurrency = _assets.Items.MoneyTypeOf(card.DayItemId)!.Value;
        var before = player.Wallet.Balance(rewardCurrency);
        player.Wallet.Credit((int)MoneyType.BindDiamond, card.NowNum);
        player.MonthCards.Load([(card.Id, now.AddDays(-10).ToUnixTimeSeconds(), now.AddDays(-12).ToUnixTimeSeconds())]);
        player.DrainGameplayChanges();

        Assert.Equal(0, player.BuyChargeGoods(goods.Id).Code);
        Assert.Equal(before + card.NowNum + 2 * card.DayNum, player.Wallet.Balance(rewardCurrency));
        var paid = Assert.Single(player.DrainGameplayChanges().OfType<SCMonthCardRewardNtf>());
        Assert.Equal(2 * card.DayNum, paid.ItemNum);
        Assert.Empty(player.SettleMonthCards(DateTimeOffset.UtcNow).OfType<SCMonthCardRewardNtf>());
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 2));
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        Assert.Equal(before + card.NowNum + 2 * card.DayNum, ctx.Player.Wallet.Balance(rewardCurrency));
        Assert.Empty(ctx.Player.SettleMonthCards(DateTimeOffset.UtcNow).OfType<SCMonthCardRewardNtf>());
    }

    [Fact]
    public void MonthCard_RefusesAnExtensionBeyondTheLimitBeforeCharging()
    {
        var player = new Player(1, _assets);
        var now = DateTimeOffset.UtcNow;
        var card = _assets.Charge.MonthCardOf(2001)!;
        var goods = _assets.Charge.Offered.Single(g => g.ChargeType == ChargeAssets.MonthCard && g.SubId == card.Id);
        player.MonthCards.Load([(card.Id, now.AddDays(card.DaysUplimit - 1).ToUnixTimeSeconds(), now.ToUnixTimeSeconds())]);
        var before = player.MonthCards.Cards[card.Id];
        player.Wallet.Credit((int)MoneyType.BindDiamond, card.NowNum);
        player.Wallet.DrainChanged();

        Assert.Equal((int)EnmTextCode.EnmTextMonthCardDayUplimit, player.BuyChargeGoods(goods.Id).Code);
        Assert.Equal(card.NowNum, player.Wallet.Balance((int)MoneyType.BindDiamond));
        Assert.Equal(before, player.MonthCards.Cards[card.Id]);
        Assert.Empty(player.DrainGameplayChanges());
    }

    [Fact]
    public void MonthCard_ExpiredSettledCardsDoNotDirtyEveryFollowingDay()
    {
        var player = new Player(1, _assets);
        var now = DateTimeOffset.UtcNow;
        player.MonthCards.Load([(2001u, now.AddDays(-2).ToUnixTimeSeconds(), now.AddDays(-2).ToUnixTimeSeconds())]);
        Assert.Empty(player.MonthCards.DueDailyGrants(now));
        Assert.False(player.MonthCards.IsDirty);
        Assert.Empty(player.MonthCards.DueDailyGrants(now.AddDays(1)));
        Assert.False(player.MonthCards.IsDirty);
    }
}
