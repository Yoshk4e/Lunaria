using Lunaria.Game.Motives;
using Lunaria.Game.Player;
using Lunaria.Game.Player.Persistence.Saves;
using Lunaria.Game.Resources;
using Msg;
using Xunit;
using Xunit.Abstractions;

namespace Lunaria.Tests;

[Collection("bundled-gameplay")]
[Trait("Category", "AuditRound2")]
public sealed class AuditRound2EconomyTests(BundledGameplayFixture fixture, ITestOutputHelper output)
{
    [Fact]
    public void MotiveReward_AtCapacity_CanBeCollectedAfterDecomposingOne()
    {
        const uint itemId = 12031001;
        var player = new Player(1, fixture.Data);
        var item = fixture.Data.Items.Get(itemId)!;
        Assert.True(item.AutoUse);
        Assert.Equal((int)ItemUseType.AddMotive, item.UseType);
        Assert.True(fixture.Data.Motives.Rarity(itemId) < 5);
        for (var i = 0; i < MotiveManager.MaxMotives; i++)
            Assert.True(player.Motives.Add(player.Guid, itemId, 1).Ok);
        player.DrainGameplayChanges();

        var delivery = player.GrantRewards([new ItemGrant(itemId, 1)], EnmItemReason.EnmItemChangeNormal);

        Assert.Equal(MotiveManager.MaxMotives, player.Motives.Count);
        Assert.Equal(new ItemGrant(itemId, 1), Assert.Single(delivery.Stored));
        Assert.Equal(1u, player.Bag.CountOf(itemId));
        Assert.Empty(delivery.Undelivered);
        Assert.Equal(0, player.DecomposeMotives([player.Motives.All[0].UniqId]).Code);
        player.AdvanceTime(DateTimeOffset.UtcNow);
        var manualUse = player.UseItem(itemId, 1, []);
        player.InitializeRoleState(DateTimeOffset.UtcNow, hasSave: true);

        output.WriteLine($"Capacity {MotiveManager.MaxMotives}; after decomposing, time settlement, item use and login: " +
            $"owned motives={player.Motives.Count}, bag token={player.Bag.CountOf(itemId)}, item-use code={manualUse.Code}.");
        Assert.Equal(MotiveManager.MaxMotives, player.Motives.Count);
        Assert.Equal(0u, player.Bag.CountOf(itemId));
    }

    [Fact]
    public void MotiveReward_BelowCapacity_CreatesTheEquipmentInstance()
    {
        const uint itemId = 12031001;
        var player = new Player(1, fixture.Data);

        var delivery = player.GrantRewards([new ItemGrant(itemId, 1)], EnmItemReason.EnmItemChangeNormal);

        Assert.Equal(itemId, Assert.Single(player.Motives.All).MotiveId);
        Assert.Single(delivery.NewMotives);
        Assert.Empty(delivery.Stored);
        Assert.Empty(delivery.Undelivered);
        Assert.Equal(0u, player.Bag.CountOf(itemId));
    }

    [Theory]
    [InlineData(10011u)]
    [InlineData(10012u)]
    [InlineData(10013u)]
    [InlineData(10014u)]
    public void StarterSkill_CanBeRaisedAfterReceivingItsConfiguredCost(uint group)
    {
        var player = new Player(1, fixture.Data);
        player.GrantStarterState();
        Assert.True(player.Skills.KnowsGroup(group));
        Assert.Equal(0, player.Skills.CheckRaise(group));
        while (player.Skills.GroupLevel(group) < fixture.Data.Skills.MaxLevel(group))
        {
            var cost = Assert.IsType<SkillCost>(player.Skills.NextCost(group));
            Assert.NotEmpty(cost.Items);
            var before = player.Skills.GroupLevel(group)!.Value;
            player.Wallet.Credit(player.Wallet.CoinMoneyType, cost.Coin);
            var funding = player.GrantRewards(cost.Items, EnmItemReason.EnmItemChangeNormal);

            var result = player.RaiseSkillGroup(group);

            output.WriteLine($"Skill {group}: cost [{string.Join(", ", cost.Items.Select(i => $"{i.ItemId}x{i.Count}"))}], " +
                $"undefined cost items [{string.Join(", ", cost.Items.Where(i => !fixture.Data.Items.Exists(i.ItemId)).Select(i => i.ItemId))}], " +
                $"mailed {funding.Mailed.Count}, upgrade result {result}.");
            Assert.Equal(0, result);
            Assert.Equal(before + 1, player.Skills.GroupLevel(group));
            Assert.Empty(funding.Mailed);
            Assert.All(cost.Items, item => Assert.Equal(0u, player.Bag.CountOf(item.ItemId)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StoredMotiveRewards_RetryOnlyAvailableSpaceAfterReload(bool onLogin)
    {
        const uint itemId = 12031001;
        var player = new Player(1, fixture.Data);
        for (var i = 0; i < MotiveManager.MaxMotives - 1; i++)
            Assert.True(player.Motives.Add(player.Guid, itemId, 1).Ok);
        var grant = player.GrantRewards([new ItemGrant(itemId, 3)], EnmItemReason.EnmItemChangeNormal);
        Assert.Single(grant.NewMotives);
        Assert.Equal(2u, player.Bag.CountOf(itemId));
        var restored = new Player(2, fixture.Data);
        // Equipment rows are persisted separately from the role JSON, as in RoleStateStore.
        restored.Motives.Load(player.Motives.All);
        restored.Guid.Adopt(player.Guid.LastMinted);
        RoleSaveMapper.Apply(restored, RoleSaveMapper.Capture(player));
        // Use the storage manager to leave the retry specifically to login or the timer.
        Assert.Equal(0, restored.Motives.Decompose([restored.Motives.All[0].UniqId]).Code);
        var now = DateTimeOffset.UtcNow;
        if (onLogin) restored.InitializeRoleState(now, hasSave: true);
        else restored.AdvanceTime(now);
        Assert.Equal(MotiveManager.MaxMotives, restored.Motives.Count);
        Assert.Equal(1u, restored.Bag.CountOf(itemId));
        restored.AdvanceTime(now.AddSeconds(1));
        Assert.Equal(MotiveManager.MaxMotives, restored.Motives.Count);
        Assert.Equal(1u, restored.Bag.CountOf(itemId));
    }
}
