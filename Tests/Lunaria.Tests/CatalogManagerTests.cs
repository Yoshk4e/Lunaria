using Lunaria.Game.Characters;
using Lunaria.Game.Gacha;
using Lunaria.Game.Player;
using Lunaria.Game.Player.Managers;
using Lunaria.Game.Player.Persistence;
using Lunaria.Game.Resources;
using Lunaria.Game.World;
using Msg;
using Xunit;

namespace Lunaria.Tests;

[Collection("bundled-gameplay")]
public sealed class CatalogManagerTests(BundledGameplayFixture fixture)
{
    private GameData Assets => fixture.Data;
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
    private sealed class LowestRoll : Random
    {
        public override double NextDouble() => 0;
        public override int Next(int maxValue) => 0;
    }

    [Fact]
    public void Gacha_NewCharactersHaveTheirSkillGroupsImmediately()
    {
        var player = new Player(1, Assets);
        var banner = Assets.Gacha.Banners.First(b => b.Kind == "character");
        player.Wallet.Credit(Assets.Gacha.CostMoneyType(banner.PoolId), Assets.Gacha.PullCost(banner.PoolId));
        var result = player.DoGacha(banner.BannerId, false, Now, new LowestRoll());
        Assert.Equal(0, result.Code);
        var character = Assert.Single(player.Characters.All);
        var expected = Assets.Characters.StartingSkillGroups(character.CharacterId).ToArray();
        Assert.NotEmpty(expected);
        foreach (var (group, level) in expected) Assert.Equal(level, player.Skills.GroupLevel(group));
        Assert.Contains(player.Skills.Talents(), t => t.InstId == character.InstId);
        Assert.Single(player.DrainGameplayChanges().OfType<SCCharacterNewcomerNtf>());
    }

    [Fact]
    public void Gacha_ResultScreenListsEachPullByItem()
    {
        var player = new Player(1, Assets);
        var banner = Assets.Gacha.Banners.First(b => b.Kind == "character");
        player.Wallet.Credit(Assets.Gacha.CostMoneyType(banner.PoolId), 2L * Assets.Gacha.PullCost(banner.PoolId));
        var card = Assets.Items.CharacterCardFor(banner.FeaturedFiveStar);
        Assert.NotNull(card);

        var first = Assert.Single(player.DoGacha(banner.BannerId, false, Now, new LowestRoll()).Delivery!.Delivery.Presentation
            .OfType<SCPreciousAwardShowNtf>());
        Assert.Equal(EnmItemReason.EnmItemChangeGachaReward, first.Source);
        var drawn = Assert.Single(first.Items);
        Assert.Equal(card, drawn.ItemId);
        Assert.True(drawn.IsNew);
        Assert.Empty(drawn.RepeatConvert);

        var again = Assert.Single(Assert.Single(player.DoGacha(banner.BannerId, false, Now, new LowestRoll()).Delivery!.Delivery
            .Presentation.OfType<SCPreciousAwardShowNtf>()).Items);
        Assert.Equal(card, again.ItemId);
        Assert.False(again.IsNew);
        Assert.Single(again.RepeatConvert);
    }

    [Fact]
    public void Gacha_InsufficientFundsAndDailyLimitDoNotChargeOrRoll()
    {
        var player = new Player(1, Assets);
        var banner = Assets.Gacha.Banners.First();
        var cap = Assets.Gacha.DailyLimit(banner.PoolId);
        var currency = Assets.Gacha.CostMoneyType(banner.PoolId);
        var price = Assets.Gacha.PullCost(banner.PoolId);
        Assert.True(cap > 0);
        Assert.NotEqual(0, player.DoGacha(banner.BannerId, false, Now, new LowestRoll()).Code);
        Assert.Empty(player.Gacha.Entries);
        player.Wallet.Credit(currency, 20L * price);
        player.Gacha.Load([(banner.BannerId, cap - 1, 0u, 0u, 0u, false, 0u, cap - 1, Now)]);
        var before = player.Gacha.Entries[banner.BannerId];
        Assert.NotEqual(0, player.DoGacha(banner.BannerId, true, Now, new LowestRoll()).Code);
        Assert.Equal(before, player.Gacha.Entries[banner.BannerId]);
        Assert.Equal(20L * price, player.Wallet.Balance(currency));
        Assert.Equal(0, player.DoGacha(banner.BannerId, false, Now, new LowestRoll()).Code);
        var balance = player.Wallet.Balance(currency);
        Assert.NotEqual(0, player.DoGacha(banner.BannerId, false, Now, new LowestRoll()).Code);
        Assert.Equal(balance, player.Wallet.Balance(currency));
        Assert.Equal(0, player.DoGacha(banner.BannerId, false, Now.AddDays(1), new LowestRoll()).Code);
        Assert.Equal(1u, player.Gacha.DailyCountOf(banner.BannerId, Now.AddDays(1)));
        // The client reads daily_count as the pulls left today.
        Assert.Equal(cap - 1, player.Gacha.PoolInfo(banner.BannerId, Now.AddDays(1)).DailyCount);
        Assert.Equal(cap, new Player(2, Assets).Gacha.PoolInfo(banner.BannerId, Now).DailyCount);
    }

    [Fact]
    public void Gacha_ClaimedRebateMaskSurvivesReload()
    {
        var manager = new GachaManager(Assets);
        var banner = Assets.Gacha.Banners.First();
        var milestones = Assets.Gacha.Rebates(banner.PoolId);
        Assert.NotEmpty(milestones);
        var first = milestones.Min(m => m.DrawCount);
        manager.Load([(banner.BannerId, first, 0u, 0u, 0u, false, 0u, 0u, Now)]);
        Assert.Equal(milestones.Where(m => m.DrawCount == first).Select(m => m.Reward), manager.ClaimRebates(banner.BannerId));
        Assert.Empty(manager.ClaimRebates(banner.BannerId));
        var state = manager.Entries[banner.BannerId];
        var restored = new GachaManager(Assets);
        restored.Load([(banner.BannerId, state.Total, state.SinceFive, state.SinceFour, state.FeaturedSince,
            state.Guaranteed, state.ClaimedMask, state.DailyCount, state.DailyAnchor)]);
        Assert.Empty(restored.ClaimRebates(banner.BannerId));
        Assert.Equal(manager.ClaimedMaskOf(banner.BannerId), restored.ClaimedMaskOf(banner.BannerId));
    }

    [Fact]
    public void Skills_LoadCannotGrantGroupsForCharactersOutsideTheRoster()
    {
        var player = new Player(1, Assets);
        var groups = Assets.Characters.StartingSkillGroups(1001).ToArray();
        Assert.NotEmpty(groups);
        player.Skills.Load(groups, [], player.Characters);
        Assert.Empty(player.Skills.SkillGroups());
        Assert.NotEqual(0, player.RaiseSkillGroup(groups[0].Group));
    }

    [Fact]
    public void Expose_EverySubregionUsesItsConfiguredNpcGroupsAndWeights()
    {
        var manager = new ExposeManager(Assets.Expose);
        var battles = fixture.Rows("P_NPCGroupEnterBattle").ToDictionary(r => r.GetProperty("npcGroupId").GetUInt64());
        foreach (var row in fixture.Rows("P_SubRegionNPCGroup"))
        {
            var region = row.GetProperty("id").GetUInt64();
            var groups = row.GetProperty("npcGroupIdList").EnumerateArray().Select(v => v.GetUInt64()).ToArray();
            var response = manager.MonsterList(region);
            Assert.Equal(region, response.Subregion);
            Assert.Equal(groups, response.NpcgroupData.Select(g => g.NpcGroupId));
            foreach (var group in response.NpcgroupData)
            {
                var expected = battles[group.NpcGroupId].GetProperty("enterBattleList").EnumerateArray()
                    .Select(v => v.GetString()!.Split(',')).Select(v => (ulong.Parse(v[0]), uint.Parse(v[1])));
                Assert.Equal(expected, group.BattleList.Select(b => (b.BattleId, b.Weight)));
            }
        }
        Assert.Empty(manager.MonsterList(ulong.MaxValue).NpcgroupData);
    }

    [Fact]
    public void Case_EvidenceNeedsOwnershipAndCannotLeakFromAnotherCase()
    {
        var manager = new CaseManager(Assets);
        var row = fixture.Rows("P_CaseEvidenceTable").First(r => Assets.Cases.CaseExists(r.GetProperty("caseId").GetUInt32()));
        var caseId = row.GetProperty("caseId").GetUInt32();
        var evidence = row.GetProperty("id").GetUInt64();
        Assert.NotEqual(0, manager.DecryptEvidence(evidence));
        // Opening the case grants its evidence.
        manager.OpenCase(caseId);
        Assert.True(manager.GiveEvidence(evidence));
        Assert.Equal(0, manager.DecryptEvidence(evidence));
        manager.ClearDirty();
        Assert.Equal(0, manager.DecryptEvidence(evidence));
        Assert.False(manager.IsDirty);
        var restored = new CaseManager(Assets);
        restored.Load([(caseId, 0u, Array.Empty<ulong>().AsEnumerable(), new[] { evidence, ulong.MaxValue }.AsEnumerable())], []);
        var status = Assert.Single(Assert.Single(restored.ToCaseData().ProcessingCase).EvidenceStatus);
        Assert.Equal(evidence, status.EvidenceId);
        Assert.True(status.Decrypted);
    }

    [Fact]
    public void RoleNaming_IsAtomicAndCannotBeReplayedAsARename()
    {
        var roles = new RoleManager();
        roles.Load([new RoleRow(1, 1, 0, "", "", 0, false)]);
        roles.SetActive(1);
        var before = roles.Active();
        roles.ApplyNaming((int)EnmGender.Male, "Alice"u8, "Smith"u8, name => name == "Smith");
        Assert.Equal(before, roles.Active());
        Assert.False(roles.IsDirty);
        roles.ApplyNaming((int)EnmGender.Male, "Alice"u8, "Smith"u8, _ => false);
        Assert.True(roles.Active()!.Initialized);
        Assert.Equal("Alice", roles.Active()!.Name);
        roles.MarkPersisted(1);
        roles.ApplyNaming((int)EnmGender.Female, "Other"u8, "Name"u8, _ => false);
        Assert.Equal("Alice", roles.Active()!.Name);
        Assert.False(roles.IsDirty);
    }
}
