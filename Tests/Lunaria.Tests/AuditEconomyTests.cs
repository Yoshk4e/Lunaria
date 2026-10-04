using Lunaria.Game.Player;
using Lunaria.Game.Player.Managers;
using Lunaria.Game.Resources;
using Msg;
using Xunit;

namespace Lunaria.Tests;

[Collection("bundled-gameplay")]
[Trait("Category", "Audit")]
public sealed class AuditEconomyTests(BundledGameplayFixture fixture)
{
    [Fact]
    public void ShopPurchases_AfterClockRollback_CannotResetAnAlreadyUsedQuota()
    {
        var assets = fixture.Data;
        var player = new Player(1, assets);
        var (shopId, good) = fixture.Rows("P_ShopTable").Select(r => r.GetProperty("id").GetUInt32())
            .Where(id => !assets.Shops.NeedsSatiety(id))
            .SelectMany(id => assets.Shops.Goods(id).Select(good => (id, good)))
            .First(pair => pair.good.LimitType == 2 && pair.good.LimitNum > 1);
        var now = new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
        player.Wallet.Credit(good.MoneyType, (long)good.CostNum * (good.LimitNum + 2));
        Assert.Equal(0, player.BuyFromShop(shopId, [(good.Id, 1)], now).Code);
        Assert.Equal(0, player.BuyFromShop(shopId, [(good.Id, good.LimitNum - 1)], now.AddMonths(-1)).Code);
        var balance = player.Wallet.Balance(good.MoneyType);

        var extra = player.BuyFromShop(shopId, [(good.Id, 1)], now);

        Assert.Equal((int)EnmTextCode.EnmTextShopGoodsNotEnough, extra.Code);
        Assert.Equal(balance, player.Wallet.Balance(good.MoneyType));
        Assert.Equal(good.LimitNum, player.Shop.BoughtOf(good, now));
        Assert.Equal(0, player.BuyFromShop(shopId, [(good.Id, 1)], now.AddMonths(1)).Code);
    }

    [Fact]
    public void BuffShop_ListsItsBuffGoodsAndSellsThem()
    {
        const uint voicepipeCafe = 202; // Type 1 shop, groups 1 and 4 of P_ShopBuffTable
        var assets = fixture.Data;
        var goods = assets.Shops.Goods(voicepipeCafe);
        Assert.Equal(fixture.Rows("P_ShopBuffTable").Count(r => r.GetProperty("group").GetUInt32() is 1 or 4), goods.Count);
        Assert.Contains(goods, good => good.ItemId == 21207001 && good.ItemNum == 1);

        var player = new Player(1, assets);
        var good = goods[0];
        player.Wallet.Credit(good.MoneyType, good.CostNum);
        var bought = player.BuyFromShop(voicepipeCafe, [(good.Id, 1)], DateTimeOffset.UtcNow);
        Assert.Equal(0, bought.Code);
        Assert.Equal(0, player.Wallet.Balance(good.MoneyType));
    }

    [Fact]
    public void LimitedGood_ListsTheStockLeftInThePeriod()
    {
        var assets = fixture.Data;
        var player = new Player(1, assets);
        var (shopId, good) = fixture.Rows("P_ShopTable").Select(r => r.GetProperty("id").GetUInt32())
            .Where(id => !assets.Shops.NeedsSatiety(id))
            .SelectMany(id => assets.Shops.Goods(id).Select(good => (id, good)))
            .First(pair => pair.good.LimitNum > 1);
        var now = new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
        uint Left() => player.Shop.GoodsInfo(shopId, now).Single(info => info.Id == good.Id).PeriodNum;

        Assert.Equal(good.LimitNum, Left());
        player.Wallet.Credit(good.MoneyType, good.CostNum);
        Assert.Equal(0, player.BuyFromShop(shopId, [(good.Id, 1)], now).Code);

        Assert.Equal(good.LimitNum - 1, Left());
    }

    [Fact]
    public void BankedHouseIncome_AfterClockRollback_CanOnlyBeClaimedOnce()
    {
        var assets = fixture.Data;
        var player = new Player(1, assets);
        var house = assets.Houses.All.First(h => assets.Houses.MaxLevel(h.LevelGroup) > 1
            && assets.Houses.IncomePerInterval(h.Id, 1) > 0);
        var upgradeAt = DateTimeOffset.UtcNow.AddDays(1);
        var openedAt = upgradeAt.AddSeconds(-(long)assets.Policy.HouseRent.IntervalSeconds);
        player.Houses.MarkBought(house.Id, openedAt);
        player.Houses.MarkOpened(house.Id, openedAt);
        Assert.NotNull(player.Houses.MarkUpgraded(house.Id, upgradeAt));
        Assert.True(player.Houses.Houses[house.Id].BankedIncome > 0);

        // The clock has moved backward since the upgrade banked the old rent.
        var first = player.ClaimHouseIncome();
        Assert.NotEmpty(first.Credited);
        var balance = player.Wallet.Balance((int)HouseManager.HouseCurrency);
        var second = player.ClaimHouseIncome();

        Assert.False(second.HasChanges, "Banked rent must not remain claimable after it has been paid.");
        Assert.Equal(balance, player.Wallet.Balance((int)HouseManager.HouseCurrency));
        Assert.Equal(upgradeAt, player.Houses.Houses[house.Id].IncomeAnchor);
        Assert.Equal(0u, player.Houses.Houses[house.Id].BankedIncome);
    }

    [Fact]
    public void ZeroMotiveExperience_ReportsTheUnchangedLevel()
    {
        var player = new Player(1, fixture.Data);
        var grant = player.Motives.Add(player.Guid, 12031001, 1);
        Assert.True(grant.Ok);
        var before = player.Motives.Get(grant.UniqId)!;
        player.Motives.ClearDirty();

        var result = player.Motives.GrantExp(grant.UniqId, 0);

        Assert.Equal(0, result.Code);
        Assert.Equal(before.Level, result.OldLevel);
        Assert.Equal(before.Level, result.NewLevel);
        Assert.Equal(before, player.Motives.Get(grant.UniqId));
        Assert.False(player.Motives.IsDirty);
    }
}
