using Lunaria.Game.Characters;
using Lunaria.Game.Mail;
using Lunaria.Game.Player;
using Lunaria.Game.Resources;
using Msg;
using Xunit;

namespace Lunaria.Tests;

[Collection("bundled-gameplay")]
public sealed class ServerCorrectnessTests(BundledGameplayFixture fixture)
{
    private Player Fresh()
    {
        var player = new Player(1, fixture.Data);
        player.Characters.GrantStarter(player.Guid);
        player.Teams.GrantStarter(player.Characters);
        return player;
    }

    [Fact]
    public void MotiveInventory_PreservesDuplicateInstances_AndAllMutationDeltas()
    {
        var player = Fresh();
        player.GrantRewards([new ItemGrant(12031001, 2)], EnmItemReason.EnmItemChangeNormal);
        var inventory = player.InventoryItems().Where(i => i.MotiveData is not null).ToArray();
        Assert.Equal(2, inventory.Length);
        Assert.Equal(2, inventory.Select(i => i.BindId).Distinct().Count());
        Assert.All(inventory, item => {
            Assert.Equal(item.BindId, item.MotiveData.MotiveUniqId);
            Assert.Equal(1u, item.ItemNum);
        });
        var acquired = player.DrainGameplayChanges().OfType<SCItemBagChangeNtf>().SelectMany(n => n.Items).ToArray();
        Assert.Equal(inventory.Select(i => i.BindId).Order(), acquired.Select(i => i.BindId).Order());

        var first = inventory[0].BindId;
        var second = inventory[1].BindId;
        var character = player.Characters.All.First().InstId;
        Assert.Equal(0, player.EquipMotive(first, character));
        Assert.Equal(first, Assert.Single(player.DrainGameplayChanges().OfType<SCItemBagChangeNtf>()).Items.Single().BindId);
        Assert.Equal(0, player.SetMotiveLock(first, true));
        Assert.Single(player.DrainGameplayChanges().OfType<SCItemBagChangeNtf>());
        Assert.NotEqual(0, player.DecomposeMotives([first]).Code);
        Assert.Empty(player.DrainGameplayChanges());

        Assert.Equal(0, player.DecomposeMotives([second]).Code);
        var removed = Assert.Single(player.DrainGameplayChanges().OfType<SCItemBagChangeNtf>()
            .SelectMany(n => n.Items), i => i.BindId == second);
        Assert.Equal(0u, removed.ItemNum);
        Assert.Equal(second, removed.MotiveData.MotiveUniqId);
        Assert.Equal(first, Assert.Single(player.InventoryItems(), i => i.MotiveData is not null).BindId);
        Assert.Equal(0, player.UnequipMotive(first, character));
        Assert.Single(player.DrainGameplayChanges().OfType<SCItemBagChangeNtf>());
    }

    [Fact]
    public void MotiveRefinement_UpdatesSurvivor_AndRemovesConsumedInstance()
    {
        var player = Fresh();
        player.GrantRewards([new ItemGrant(12031001, 2)], EnmItemReason.EnmItemChangeNormal);
        var ids = player.Motives.All.Select(m => m.UniqId).ToArray();
        player.DrainGameplayChanges();
        Assert.Equal(0, player.RefineMotive(ids[0], [ids[1]]).Code);
        var changes = player.DrainGameplayChanges().OfType<SCItemBagChangeNtf>().SelectMany(n => n.Items).ToArray();
        Assert.Equal(1u, Assert.Single(changes, i => i.BindId == ids[0]).ItemNum);
        Assert.Equal(0u, Assert.Single(changes, i => i.BindId == ids[1]).ItemNum);
        Assert.Single(player.Motives.All);
    }

    [Theory]
    [InlineData(true, true, 0)]
    [InlineData(false, true, 0)]
    [InlineData(false, false, 0)]
    [InlineData(true, false, 1)]
    public void DungeonClearCredit_FollowsAcceptedSettlement(bool victory, bool leave, uint expectedClears)
    {
        var player = Fresh();
        player.Progress.Load(1, 0, 0, 240, DateTimeOffset.UtcNow);
        const uint dungeonId = 201001;
        Assert.Equal(0, player.EnterDungeon(dungeonId).Code);
        var eventId = fixture.Data.Unlocks.ArgEvents(GlobalEventSub.DungeonCount, dungeonId).First();
        var result = player.FinishDungeon(dungeonId, victory, leave, 0);
        Assert.Equal(0, result.Code);
        Assert.Equal(expectedClears, player.Dungeons.Finishes.GetValueOrDefault(dungeonId));
        Assert.Equal(expectedClears, player.Achievements.ProgressOf(eventId));
        if (leave) Assert.Null(result.Delivery);
        Assert.NotEqual(0, player.FinishDungeon(dungeonId, victory, leave, 0).Code);
        Assert.Equal(expectedClears, player.Achievements.ProgressOf(eventId));
    }

    [Fact]
    public void BattleReports_OnlySettleMatchingBegunBattle_AndIgnoreReplay()
    {
        var player = Fresh();
        var character = player.Characters.All.First().InstId;
        var hp = player.Characters.Hp(character);
        var report = new CSLeaveBattle {
            BattleType = EBattleType.EnmBattleTypeWanted, BattleFieldId = 1,
            BattleResult = EBattleResultType.EnmBattleResultTypeSuccess
        };
        report.CharacterData.Add(new CharacterAttribInfo { InstId = character, CurrentHp = 0, PermanentLiquid = -1 });
        Assert.Equal(0, player.LeaveBattle(report).Result);
        Assert.Equal(hp, player.Characters.Hp(character));
        Assert.Empty(player.DrainGameplayChanges());
        Assert.Equal(0, player.Battles.Enter(report.BattleType, 1, 0, default));
        Assert.Equal(0, player.LeaveBattle(report).Result);
        Assert.Equal(hp, player.Characters.Hp(character));
        Assert.Equal(0, player.Battles.Enter(report.BattleType, 1, 0, default));
        Assert.Equal(0, player.Battles.Start(report.BattleType, 1));
        var wrong = report.Clone();
        wrong.BattleFieldId = 2;
        Assert.NotEqual(0, player.LeaveBattle(wrong).Result);
        Assert.NotNull(player.Battles.Current);
        Assert.Equal(hp, player.Characters.Hp(character));
        Assert.Equal(0, player.LeaveBattle(report).Result);
        Assert.Equal(0, player.Characters.Hp(character));
        player.DrainGameplayChanges();
        player.Characters.SetHp(character, hp);
        Assert.Equal(0, player.LeaveBattle(report).Result);
        Assert.Equal(hp, player.Characters.Hp(character));
        Assert.Empty(player.DrainGameplayChanges());
    }

    [Fact]
    public void BattleReports_PatrolLeaveMayCarryIdentifiersTheEntryOmitted()
    {
        var player = Fresh();
        Assert.Equal(0, player.Battles.Enter(EBattleType.EnmBattleTypePatrol, 109100101, 0, default));
        Assert.Equal(0, player.Battles.Start(EBattleType.EnmBattleTypePatrol, 109100101));
        var report = new CSLeaveBattle {
            BattleType = EBattleType.EnmBattleTypePatrol, BattleFieldId = 109100101,
            BattleInstId = 3700101, MonsterFromType = EnmMonsterFromType.EmonsterFromTable,
            BattleResult = EBattleResultType.EnmBattleResultTypeSuccess
        };
        Assert.Equal(0, player.LeaveBattle(report).Result);
        Assert.Null(player.Battles.Current);
        Assert.Equal(0, player.Battles.Enter(EBattleType.EnmBattleTypePatrol, 109101201, 0, default));

        player.Battles.Leave(EBattleType.EnmBattleTypePatrol, 109101201, false);
        Assert.Equal(0, player.Battles.Enter(EBattleType.EnmBattleTypePatrol, 109101201, 7, EnmMonsterFromType.EmonsterFromTask));
        var mismatched = report.Clone();
        mismatched.BattleFieldId = 109101201;
        Assert.NotEqual(0, player.LeaveBattle(mismatched).Result);
        Assert.NotNull(player.Battles.Current);
    }

    [Fact]
    public void UpdateTeam_OpensAnotherTeamTheClientLists()
    {
        var player = Fresh();
        var proposed = player.Teams.ToTeamData(player.Teams.Get(player.Teams.Current)!).Clone();
        proposed.TeamId = 2;

        var result = player.UpdateTeam(proposed);

        Assert.Equal(0, result.Result);
        Assert.Equal(2u, result.TeamData.TeamId);
        Assert.Equal(proposed.MemberData.Select(m => m.InstId), result.TeamData.MemberData.Select(m => m.InstId));
        Assert.Equal(1u, player.Teams.Current);

        proposed.TeamId = (uint)TeamManager.MaxTeams + 1;
        Assert.Equal((int)EnmTextCode.EnmTextCharacterInvalidTeamid, player.UpdateTeam(proposed).Result);
        Assert.Null(player.Teams.Get(proposed.TeamId));
    }

    [Fact]
    public void EquipMotive_SwitchReplacesTheCurrentMotiveAndTakesItFromItsHolder()
    {
        var player = Fresh();
        var first = player.Motives.Add(player.Guid, 12031001, 1).UniqId;
        var second = player.Motives.Add(player.Guid, 12032001, 1).UniqId;
        var a = player.Characters.All.First().InstId;
        var b = player.Characters.Get(player.Characters.Add(player.Guid, 1003).InstId)?.InstId
                ?? player.Characters.All.First(c => c.InstId != a).InstId;

        Assert.Equal(0, player.EquipMotive(first, a));
        Assert.Equal(0, player.EquipMotive(second, a));
        Assert.Equal(second, player.Characters.Get(a)!.MotiveUniqId);
        Assert.Equal(0ul, player.Motives.Get(first)!.EquipedTarget);
        Assert.Equal(a, player.Motives.Get(second)!.EquipedTarget);

        Assert.Equal(0, player.EquipMotive(second, b));
        Assert.Equal(0ul, player.Characters.Get(a)!.MotiveUniqId);
        Assert.Equal(second, player.Characters.Get(b)!.MotiveUniqId);
        Assert.Equal(b, player.Motives.Get(second)!.EquipedTarget);
    }

    [Fact]
    public void MissingTeamAndInvalidMember_DoNotMutateTeams()
    {
        var player = Fresh();
        var before = player.Teams.ToTeamData(player.Teams.Get(player.Teams.Current)!);
        Assert.NotEqual(0, player.UpdateTeam(null).Result);
        Assert.NotEqual(0, player.SwitchMainCharacter(null, 0).Result);
        var proposed = before.Clone();
        proposed.MemberData[0].InstId = ulong.MaxValue;
        Assert.NotEqual(0, player.UpdateTeam(proposed).Result);
        Assert.Equal(before, player.Teams.ToTeamData(player.Teams.Get(player.Teams.Current)!));
    }

    [Theory]
    [InlineData(7u, EnmMonsterFromType.EmonsterFromInvalid, 8u, EnmMonsterFromType.EmonsterFromTable)]
    [InlineData(0u, EnmMonsterFromType.EmonsterFromTask, 7u, EnmMonsterFromType.EmonsterFromTable)]
    [InlineData(0u, EnmMonsterFromType.EmonsterFromInvalid, 7u, (EnmMonsterFromType)999)]
    public void PatrolLeave_RejectsKnownIdentifierMismatchOrInvalidSource(
        uint enteredInstance, EnmMonsterFromType enteredSource, uint reportedInstance, EnmMonsterFromType reportedSource)
    {
        var player = Fresh();
        const uint field = 109100101;
        Assert.Equal(0, player.EnterBattle(EBattleType.EnmBattleTypePatrol, field, enteredInstance, enteredSource));
        Assert.Equal(0, player.StartBattle(EBattleType.EnmBattleTypePatrol, field));
        var battle = player.CurrentBattle;

        var result = player.LeaveBattle(new CSLeaveBattle {
            BattleType = EBattleType.EnmBattleTypePatrol, BattleFieldId = field,
            BattleInstId = reportedInstance, MonsterFromType = reportedSource,
            BattleResult = EBattleResultType.EnmBattleResultTypeSuccess
        });

        Assert.Equal((int)EnmTextCode.EnmTextBattleStateNotMatch, result.Result);
        Assert.Same(battle, player.CurrentBattle);
        Assert.Empty(player.DrainGameplayChanges());
    }

    [Fact]
    public void ExperienceOnlyDelivery_IsVisible_AndCombinesWithoutApplyingAgain()
    {
        var player = Fresh();
        var reason = EnmItemReason.EnmItemChangeDailyMissionReward;
        Assert.True(player.TeamExpFor(reason) > 0);
        var first = player.GrantRewards([], reason);
        var second = player.GrantRewards([], reason);
        var level = player.LevelData();
        var combined = RewardDelivery.Combine([first, second]);
        Assert.True(first.HasChanges);
        Assert.Equal((ulong)player.TeamExpFor(reason), first.TeamExpFromReason);
        Assert.Equal(first.TeamExpAwarded + second.TeamExpAwarded, combined.TeamExpAwarded);
        Assert.Equal(level, player.LevelData());
    }

    [Fact]
    public void WantedRedemption_UsesFixedTableCost_AndPreservesUnspentStamina()
    {
        var player = Fresh();
        Assert.Equal(0, player.EnterWanted(10101));
        var entry = fixture.Data.Wanted.Entry(10101)!;
        var price = fixture.Data.Wanted.Poster(entry.WantedPosterId)!.OptionalAwardCost;
        Assert.Equal(60u, price);
        player.Progress.Load(1, 0, 0, (int)price + 17, DateTimeOffset.UtcNow);
        Assert.NotEqual(0, player.WantedStaminaExchange().Result);
        Assert.True(player.Wanted.OnBattleEnded(true).Completed);
        var bought = player.WantedStaminaExchange();
        Assert.Equal(0, bought.Result);
        Assert.Equal(price, bought.SpentStamina);
        Assert.Equal(17, player.Progress.Stamina);
        Assert.True(bought.Delivery.HasChanges);
        Assert.DoesNotContain(bought.Delivery.Credited, grant => grant.ItemId == fixture.Data.Items.CurrencyItemFor(13));
        player.DrainGameplayChanges();
        Assert.NotEqual(0, player.WantedStaminaExchange().Result);
        Assert.Equal(17, player.Progress.Stamina);
        Assert.Empty(player.DrainGameplayChanges());
    }

    [Fact]
    public void TimeSettlement_ExpiresMail_RegeneratesStamina_AndDoesNotReplay()
    {
        var player = Fresh();
        var now = DateTimeOffset.UtcNow;
        player.Progress.Load(1, 0, 0, 0, now);
        player.Mails.Load([new MailEntry { MailId = 77, ExpireTime = (uint)now.AddSeconds(1).ToUnixTimeSeconds() }]);
        var later = now.AddSeconds(fixture.Data.GlobalConfig.StaminaRegenInterval * 3);
        var messages = player.AdvanceTime(later);
        Assert.Equal(3, player.Progress.Stamina);
        Assert.Equal(77u, Assert.Single(Assert.Single(messages.OfType<SCMailAddDelNft>()).DelMailIds));
        Assert.Empty(player.Mails.Entries);
        var stamina = Assert.Single(player.DrainGameplayChanges().OfType<SCPlayerAttrUpdateNtf>());
        Assert.Contains(stamina.UpdateAttrs, attr => attr.AttrType == (int)PlayerAttrType.EnmPlayerAttrStaminaCur && attr.ValueInt32 == 3);
        Assert.Empty(player.AdvanceTime(later));
        Assert.Empty(player.DrainGameplayChanges());
        Assert.Empty(player.AdvanceTime(later.AddSeconds(-1)));
        Assert.Equal(3, player.Progress.Stamina);
    }
}
