using Lunaria.Game.Player;
using Lunaria.Game.Resources;
using Msg;
using Xunit;

namespace Lunaria.Tests;

[Collection("bundled-gameplay")]
public sealed class GameplaySyncTests(BundledGameplayFixture fixture)
{
    private Player Fresh()
    {
        var player = new Player(sessionId: 1, fixture.Data);
        player.Characters.GrantStarter(player.Guid);
        player.Teams.GrantStarter(player.Characters);
        return player;
    }

    private static void Stock(Player player, uint itemId, uint count = 1)
    {
        player.Bag.Add(itemId, count);
        player.Bag.DrainChanged();
    }

    [Theory]
    [InlineData(21206001u)]
    [InlineData(21206007u)]
    public void ItemUse_PublishesSettledVitalsAndSpentCell(uint itemId)
    {
        var player = Fresh();
        var id = player.Characters.All.First().InstId;
        player.Characters.SetHp(id, 1);
        player.Characters.SetPermanentLiquid(id, 0);
        Stock(player, itemId);

        var used = player.UseItem(itemId, count: 1, [id]);

        Assert.Equal(0, used.Code);
        Assert.Equal(1u, used.Used);
        var settled = player.Characters.AttribData(id);
        player.Characters.SetHp(id, 0);
        player.Characters.SetPermanentLiquid(id, 0);

        var changes = player.DrainGameplayChanges();
        Assert.Equal(settled, Assert.Single(changes.OfType<SCOutsideAttribNtf>()).Data);
        var bag = Assert.Single(changes.OfType<SCItemBagChangeNtf>());
        Assert.Equal(EnmItemReason.EnmItemChangeUse, bag.Reason);
        var cell = Assert.Single(bag.Items);
        Assert.Equal(itemId, cell.ItemId);
        Assert.Equal(0u, cell.ItemNum);
        Assert.Empty(player.DrainGameplayChanges());
        Assert.Empty(player.Bag.ChangedItemsData());
    }

    [Fact]
    public void ItemUse_PublishesTemporaryLiquid()
    {
        var player = Fresh();
        Stock(player, 21206013);

        var used = player.UseItem(21206013, count: 1, []);

        Assert.Equal(0, used.Code);
        Assert.Equal(1u, used.Used);
        var changes = player.DrainGameplayChanges();
        Assert.Equal(player.Teams.TemporaryLiquidNotification(), Assert.Single(changes.OfType<SCTemporaryLiquidPoolNtf>()));
        Assert.Empty(changes.OfType<SCOutsideAttribNtf>());
        Assert.Single(changes.OfType<SCItemBagChangeNtf>());
        Assert.Empty(player.DrainGameplayChanges());
    }

    [Fact]
    public void ItemUse_FullHealthAndRejectedUsesPublishNothing()
    {
        var player = Fresh();
        var id = player.Characters.All.First().InstId;
        Stock(player, 21206001);

        var unchanged = player.UseItem(21206001, count: 1, [id]);
        Assert.Equal(0, unchanged.Code);
        Assert.Equal(0u, unchanged.Used);
        Assert.NotEqual(0, player.UseItem(21206001, count: 0, [id]).Code);
        Assert.NotEqual(0, player.UseItem(21206001, count: 1, [ulong.MaxValue]).Code);
        Assert.Equal(1u, player.Bag.CountOf(21206001));
        Assert.Empty(player.DrainGameplayChanges());
    }

    [Fact]
    public void ItemUse_StaminaSnapshotsKeepEachSuccessfulUse()
    {
        var player = Fresh();
        player.Progress.Load(teamLevel: 1, teamExp: 0, satiety: 0, stamina: 0, DateTimeOffset.UtcNow);
        Stock(player, 21308001, count: 2);

        Assert.Equal(0, player.UseItem(21308001, count: 1, []).Code);
        var first = player.Progress.Stamina;
        Assert.Equal(0, player.UseItem(21308001, count: 1, []).Code);
        var second = player.Progress.Stamina;
        Assert.True(second > first);

        var changes = player.DrainGameplayChanges();
        var meters = changes.OfType<SCPlayerAttrUpdateNtf>().SelectMany(n => n.UpdateAttrs)
            .Where(a => a.AttrType == (int)PlayerAttrType.EnmPlayerAttrStaminaCur).ToArray();
        Assert.Equal(new[] { first, second }, meters.Select(a => a.ValueInt32));
        Assert.Equal(new uint[] { 1, 0 }, changes.OfType<SCItemBagChangeNtf>().Select(n => Assert.Single(n.Items).ItemNum));
        Assert.Empty(player.DrainGameplayChanges());
    }

    [Fact]
    public void ItemUse_BuffAndCooldownPublishOnce_RefusedReusePublishesNothing()
    {
        var player = Fresh();
        Stock(player, 21206015, count: 2);

        var used = player.UseItem(21206015, count: 1, []);
        Assert.Equal(0, used.Code);
        var changes = player.DrainGameplayChanges();
        Assert.Equal(110013u, Assert.Single(changes.OfType<SCBuffAdd>()).Data.Id);
        var cooldown = Assert.Single(changes.OfType<SCItemCDNtf>()).Cd;
        Assert.Equal(104u, cooldown.Type);
        Assert.True(cooldown.CdTime > (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Assert.Single(changes.OfType<SCItemBagChangeNtf>());

        Assert.Equal((int)EnmTextCode.EnmTextItemCd, player.UseItem(21206015, count: 1, []).Code);
        Assert.Equal(1u, player.Bag.CountOf(21206015));
        Assert.Empty(player.DrainGameplayChanges());
    }

    [Fact]
    public void RewardGrants_QueueIndependentBagAndWalletSnapshots()
    {
        var player = Fresh();
        player.GrantRewards([new ItemGrant(21206001, 1), new ItemGrant(1, 10)], EnmItemReason.EnmItemChangeNormal);
        player.GrantRewards([new ItemGrant(21206001, 2), new ItemGrant(1, 20)], EnmItemReason.EnmItemChangeShopBuy);

        var changes = player.DrainGameplayChanges();
        var bags = changes.OfType<SCItemBagChangeNtf>().ToArray();
        Assert.Equal(new[] { EnmItemReason.EnmItemChangeNormal, EnmItemReason.EnmItemChangeShopBuy }, bags.Select(b => b.Reason));
        Assert.Equal(new uint[] { 1, 3 }, bags.Select(b => Assert.Single(b.Items).ItemNum));
        Assert.Equal(new long[] { 10, 30 }, changes.OfType<SCMoneyUpdate>().Select(m => m.Amount));
        Assert.Empty(player.Bag.ChangedItemsData());
        Assert.Empty(player.Wallet.ChangedBalances());
        Assert.True(player.Bag.IsDirty);
        Assert.True(player.Wallet.IsDirty);
        Assert.Empty(player.DrainGameplayChanges());
    }

    [Fact]
    public void HousePurchase_PublishesZeroBalanceOnce_RefusedRepeatPublishesNothing()
    {
        var player = Fresh();
        var price = fixture.Data.Houses.Get(1)!.Price;
        player.Wallet.Credit((int)MoneyType.HouseCoins, price);
        player.Wallet.ClearChanged();

        Assert.Equal(0, player.BuyHouse(1).Code);
        var money = Assert.Single(player.DrainGameplayChanges().OfType<SCMoneyUpdate>());
        Assert.Equal((int)MoneyType.HouseCoins, money.Type);
        Assert.Equal(0, money.Amount);
        Assert.NotEqual(0, player.BuyHouse(1).Code);
        Assert.Empty(player.DrainGameplayChanges());
        Assert.Empty(player.Wallet.ChangedBalances());
    }
}
