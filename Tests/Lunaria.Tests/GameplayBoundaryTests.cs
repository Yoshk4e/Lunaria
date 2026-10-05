using Lunaria.Game.Motives;
using Lunaria.Game.Player;
using Lunaria.Game.Resources;
using Lunaria.Game.Tasks;
using Msg;
using Xunit;

namespace Lunaria.Tests;

[Collection("bundled-gameplay")]
public sealed class GameplayBoundaryTests(BundledGameplayFixture fixture)
{
    private GameData Assets => fixture.Data;

    private Player Fresh()
    {
        var player = new Player(1, Assets);
        player.Characters.GrantStarter(player.Guid);
        player.Teams.GrantStarter(player.Characters);
        return player;
    }

    [Fact]
    public void AchievementClaim_ValidatesWholeSet_ThenPaysOnlyOnce()
    {
        var player = Fresh();
        var row = Assets.Achievements.All.First();
        player.Achievements.AddProgress(row.FinishId, uint.MaxValue);
        Assert.NotEqual(0, player.ClaimAchievementRewards([row.Id, row.Id]).Code);
        Assert.Empty(player.DrainGameplayChanges());

        var result = player.ClaimAchievementRewards([row.Id]);
        Assert.Equal(0, result.Code);
        Assert.True(result.Delivery.HasChanges);
        Assert.NotEmpty(result.Delivery.Presentation);
        Assert.NotEmpty(player.DrainGameplayChanges());
        Assert.NotEqual(0, player.ClaimAchievementRewards([row.Id]).Code);
        Assert.Empty(player.DrainGameplayChanges());
    }

    [Fact]
    public void DailyClaims_PreserveExperiencePerReward_AndDoNotReplay()
    {
        var player = Fresh();
        player.DailyMissions.Load(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), [], uint.MaxValue, []);
        var eligible = player.DailyMissions.EligibleRewards();
        Assert.True(eligible.Count > 1);
        var expected = (uint)eligible.Count * player.TeamExpFor(EnmItemReason.EnmItemChangeDailyMissionReward);
        var before = player.LevelData();

        var delivery = player.ClaimDailyMissionRewards();

        Assert.True(delivery.HasChanges);
        Assert.Equal(eligible.Count, player.DailyMissions.ClaimedRewards.Count);
        ulong spentOnLevels = 0;
        for (var level = before.TeamLevel; level < player.Progress.TeamLevel; level++)
            spentOnLevels += Assets.Progression.TeamExpToAdvance(level)!.Value;
        Assert.Equal((ulong)before.TeamExp + expected + delivery.TeamExpFromItems,
            spentOnLevels + player.Progress.TeamExp);
        player.DrainGameplayChanges();
        Assert.False(player.ClaimDailyMissionRewards().HasChanges);
        Assert.Empty(player.DrainGameplayChanges());
    }

    [Fact]
    public void BattlePassClaim_PaysAndPublishesClaimedPrefixOnce()
    {
        var player = Fresh();
        player.BattlePasses.AddExp(1001, 150);
        var result = player.ClaimBattlePassAwards(1001);
        Assert.Equal(0, result.Code);
        Assert.NotEmpty(result.Items);
        var notification = Assert.Single(player.DrainGameplayChanges().OfType<SCBattlePassNtf>());
        Assert.Equal(player.BattlePasses.NotificationOf(1001), notification.Data);
        Assert.NotEqual(0, player.ClaimBattlePassAwards(1001).Code);
        Assert.Empty(player.DrainGameplayChanges());
    }

    [Fact]
    public void SignInClaim_PaysWithoutASecondGrantCall()
    {
        var player = Fresh();
        var start = Assets.SignIn.Activity(3)!.TimeOffsetStart;
        player.SignIn.Query(3, DateTimeOffset.FromUnixTimeSeconds((long)start).AddHours(1));
        var result = player.ClaimSignInReward(3, 1);
        Assert.Equal(0, result.Code);
        Assert.NotEmpty(result.Items);
        Assert.True(result.Delivery.HasChanges);
        Assert.NotEmpty(player.DrainGameplayChanges());
        Assert.NotEqual(0, player.ClaimSignInReward(3, 1).Code);
        Assert.Empty(player.DrainGameplayChanges());
    }

    [Fact]
    public void RegionClaim_PaysEveryEligibleThresholdOnce()
    {
        var player = Fresh();
        var id = Assets.RegionProgress.TrackedSubRegions.First(id => Assets.RegionProgress.Rewards(id).Count > 0
            && Assets.RegionProgress.Sequences(id).Any(s => Assets.RegionProgress.SequenceRow(id, s)?.ParamNum > 0));
        Assert.NotEqual(0, player.ClaimRegionRewards(id).Code);
        foreach (var sequence in Assets.RegionProgress.Sequences(id))
            player.RegionProgress.SetSequence(id, sequence, (uint)(Assets.RegionProgress.SequenceRow(id, sequence)?.ParamNum ?? 0));
        var eligible = player.RegionProgress.EligibleRewards(id);
        Assert.NotEmpty(eligible);
        var result = player.ClaimRegionRewards(id);
        Assert.Equal(0, result.Code);
        Assert.Equal(eligible.Select(r => r.PrograssValue), result.Values);
        Assert.True(result.Delivery.HasChanges);
        player.DrainGameplayChanges();
        Assert.NotEqual(0, player.ClaimRegionRewards(id).Code);
        Assert.Empty(player.DrainGameplayChanges());
    }

    [Fact]
    public void MotiveDecompose_PaysRecycleBeforeReturning_AndRejectsReplay()
    {
        var player = Fresh();
        player.Motives.Load([new MotiveState { UniqId = 1, MotiveId = 12031001, ItemId = 12031001, Exp = 10_000 }]);
        var outcome = player.DecomposeMotives([1]);
        Assert.Equal(0, outcome.Code);
        Assert.NotEmpty(outcome.Recycle);
        Assert.Null(player.Motives.Get(1));
        Assert.All(outcome.Recycle, g => Assert.Equal(g.Count, player.Bag.CountOf(g.ItemId)));
        Assert.Equal(EnmItemReason.EnmItemChangeDecomposeMotives, outcome.Delivery.Reason);
        Assert.Single(player.DrainGameplayChanges().OfType<SCItemBagChangeNtf>());
        Assert.NotEqual(0, player.DecomposeMotives([1]).Code);
        Assert.Empty(player.DrainGameplayChanges());
    }

    [Fact]
    public void MotiveLevelUp_PaysOverflowAndKeepsSpendAndRecycleSeparate()
    {
        var player = Fresh();
        var added = player.Motives.Add(player.Guid, 12031001, 0);
        Assert.True(added.Ok);
        var item = fixture.Rows("P_ItemTable").Select(r => r.GetProperty("id").GetUInt32())
            .First(id => player.Motives.ItemExpValue(id) > 0);
        player.Bag.Add(item, 1000);
        player.Bag.DrainChanged();

        var result = player.LevelUpMotive(added.UniqId, [new ItemGrant(item, 1000)], []);

        Assert.Equal(0, result.Code);
        Assert.NotEmpty(result.Recycle);
        Assert.All(result.Recycle, g => Assert.Equal(g.Count, player.Bag.CountOf(g.ItemId)));
        var bags = player.DrainGameplayChanges().OfType<SCItemBagChangeNtf>().ToArray();
        Assert.Equal(new[] { EnmItemReason.EnmItemChangeMotiveLevelUp, EnmItemReason.EnmItemChangeMotiveLevelUpRecycle }, bags.Select(b => b.Reason));
        Assert.Equal(0u, Assert.Single(bags[0].Items, i => i.ItemId == item).ItemNum);
    }

    [Fact]
    public void MonthCards_PayAndExpireOnceBeforePresentationIsSent()
    {
        var player = Fresh();
        var now = DateTimeOffset.FromUnixTimeSeconds(1700000000);
        player.MonthCards.Load([(2001u, now.ToUnixTimeSeconds(), now.AddDays(-2).ToUnixTimeSeconds())]);
        var messages = player.SettleMonthCards(now);
        var reward = Assert.Single(messages.OfType<SCMonthCardRewardNtf>());
        Assert.Equal(180u, reward.ItemNum);
        var moneyType = Assets.Items.MoneyTypeOf(reward.ItemId)!.Value;
        Assert.Equal(180, player.Wallet.Balance(moneyType));
        Assert.Single(messages.OfType<SCMonthCardOverdueNtf>());
        Assert.Single(player.DrainGameplayChanges().OfType<SCMoneyUpdate>());
        Assert.Empty(player.SettleMonthCards(now));
        Assert.Empty(player.DrainGameplayChanges());
    }

    [Fact]
    public void WorldSelectionAndExperience_HaveOneLevelNotificationOwner()
    {
        var player = Fresh();
        player.Progress.Load(20, 0, 0, 0, DateTimeOffset.UtcNow);
        player.CompleteRoleLogin();
        player.DrainGameplayChanges();
        Assert.Equal(0, player.SelectWorldLevel(1).Result);
        var selected = Assert.Single(player.DrainGameplayChanges().OfType<SCPlayerLevelDataNtf>());
        Assert.Equal(2u, selected.BeforeList.WorldLevelCur);
        Assert.Equal(1u, selected.UpdateList.WorldLevelCur);
        Assert.Equal(0, player.SelectWorldLevel(1).Result);
        Assert.Empty(player.DrainGameplayChanges());
        player.GrantTeamExp(10);
        player.SettleServerTargets();
        Assert.Single(player.DrainGameplayChanges().OfType<SCPlayerLevelDataNtf>());
    }

    [Fact]
    public void QuestGateWithoutExperience_IsCapturedByAggregateSettlement()
    {
        var player = Fresh();
        var open = false;
        player.Progress.QuestGate = _ => open;
        player.Progress.Load(20, 0, 0, 0, DateTimeOffset.UtcNow);
        player.CompleteRoleLogin();
        player.DrainGameplayChanges();
        open = true;
        player.SettleServerTargets();
        var changed = Assert.Single(player.DrainGameplayChanges().OfType<SCPlayerLevelDataNtf>());
        Assert.Equal(1u, changed.BeforeList.WorldLevelMax);
        Assert.Equal(2u, changed.UpdateList.WorldLevelMax);
        Assert.Equal(0u, changed.UpdateList.TeamExp);
        player.SettleServerTargets();
        Assert.Empty(player.DrainGameplayChanges());
    }

    [Fact]
    public void TaskCompletion_CapturesQuestGateAndKeepsProgressAfterReply()
    {
        var player = Fresh();
        player.InstallQuestGates();
        player.Progress.Load(20, 0, 0, 0, DateTimeOffset.UtcNow);
        player.Tasks.Load([(TaskAssets.QuestMain, 31039u, 3103912UL, Array.Empty<(ulong, uint, uint)>())], []);
        var before = player.LevelData();
        var result = player.ReportTaskAction(TaskAssets.QuestMain, 310391201, 1);
        Assert.Equal(0, result.Code);
        Assert.True(result.Outcome!.Progress.TaskCompleted);
        var changes = player.DrainGameplayChanges();
        var level = Assert.Single(changes.OfType<SCPlayerLevelDataNtf>());
        Assert.Equal(before, level.BeforeList);
        Assert.Equal(2u, level.UpdateList.WorldLevelMax);
        Assert.DoesNotContain(changes, m => m is SCTaskProgressUpdateNtf);
        Assert.DoesNotContain(result.Outcome.EffectNotifications, m => m is SCTaskProgressUpdateNtf);
        Assert.Contains(result.Outcome.Notifications, m => m is SCTaskProgressUpdateNtf);
        player.SettleServerTargets();
        Assert.Empty(player.DrainGameplayChanges().OfType<SCPlayerLevelDataNtf>());
    }

    [Fact]
    public void CharacterHealing_QueuesBothVitalsAndRoster_OnlyWhenChanged()
    {
        var player = Fresh();
        var id = player.Characters.All.First().InstId;
        player.Characters.SetHp(id, 1);
        player.HealRoster();
        var changes = player.DrainGameplayChanges();
        Assert.Single(changes.OfType<SCOutsideAttribNtf>());
        Assert.Single(changes.OfType<SCCharacterUpdateNtf>());
        player.HealRoster();
        Assert.Empty(player.DrainGameplayChanges());
    }

    private ulong GuideStep() => fixture.Rows("P_GraphicGuideTable")
        .Select(row => row.TryGetProperty("stepId", out var step) ? step.GetUInt64() : 0UL)
        .First(id => id != 0);

    [Fact]
    public void GuideUnlockAndRead_KeepSeparateSnapshots_AndDoNotReplay()
    {
        var player = Fresh();
        var stepId = GuideStep();
        var guideIds = Assets.Guides.GuidesForStep(stepId);
        Assert.NotEmpty(guideIds);

        player.Tasks.Load([(TaskAssets.QuestMain, Assets.Tasks.TaskOfStep(TaskAssets.QuestMain, stepId), stepId,
            Array.Empty<(ulong, uint, uint)>())], []);
        var outcome = player.ReportTaskAction(TaskAssets.QuestMain, Assets.Tasks.Actions(TaskAssets.QuestMain, stepId)[0], 1);
        Assert.Equal(0, outcome.Code);
        Assert.True(outcome.Outcome!.Progress.StepAdvanced);

        var delivery = outcome.Outcome.Delivery;
        Assert.Equal(guideIds, delivery.UnlockedGuides);
        var unlocked = player.Guides.InfosOf(delivery.UnlockedGuides).ToArray();
        Assert.Equal(delivery.UnlockedGuides, player.ReadGuides(delivery.UnlockedGuides).Changed);
        var read = player.Guides.InfosOf(delivery.UnlockedGuides).ToArray();

        var changes = player.DrainGameplayChanges().OfType<SCUnlockGuideNtf>().ToArray();
        Assert.Equal(2, changes.Length);
        Assert.Equal(unlocked, changes[0].GuideInfo);
        Assert.Equal(read, changes[1].GuideInfo);
        Assert.DoesNotContain(delivery.Presentation, message => message is SCUnlockGuideNtf);
        Assert.Empty(player.ReadGuides(delivery.UnlockedGuides).Changed);
        Assert.Empty(player.DrainGameplayChanges());
    }

    [Fact]
    public void GuideRead_PaysTheBrowseReward_OnlyOnTheTransition()
    {
        var player = Fresh();
        var guide = Assets.Guides.All.First();
        var dropId = Assets.Guides.BrowseRewardOf(guide);
        Assert.NotEqual(expected: 0u, dropId);
        var reward = Assets.DropTable.Roll(dropId, new Random(1));
        Assert.NotEmpty(reward);

        Assert.True(player.Guides.Unlock(guide, 0));
        var (changed, delivery) = player.ReadGuides([guide]);
        Assert.Equal([guide], changed);
        Assert.Equal(EnmItemReason.EnmItemChangeGuide, delivery.Reason);
        var coins = reward.Where(grant => Assets.Items.MoneyTypeOf(grant.ItemId) is not null).ToArray();
        var banked = reward.Where(grant => Assets.Items.TeamExpOf(grant.ItemId) is not null)
            .Aggregate(seed: 0UL, (sum, grant) => sum + (ulong)grant.Count * Assets.Items.TeamExpOf(grant.ItemId)!.Value);
        Assert.Equal(coins, delivery.Credited);
        Assert.Equal(banked, delivery.TeamExpFromItems);
        Assert.NotEmpty(delivery.Presentation);

        var replay = player.ReadGuides([guide]);
        Assert.Empty(replay.Changed);
        Assert.False(replay.Delivery.HasChanges);
    }

    [Fact]
    public void GuideUnlockAtLogin_OpensOnlyThePagesBehindTheWalk()
    {
        var player = Fresh();
        var stepId = GuideStep();
        var taskId = Assets.Tasks.TaskOfStep(TaskAssets.QuestMain, stepId);
        var chain = Assets.Tasks.Steps(TaskAssets.QuestMain, taskId);
        var current = chain[^1];
        var behind = chain.Take(chain.Count - 1).SelectMany(Assets.Guides.GuidesForStep).ToArray();
        Assert.NotEmpty(behind);

        player.Tasks.Load([(TaskAssets.QuestMain, taskId, current, Array.Empty<(ulong, uint, uint)>())], []);
        player.UnlockWalkedGuides();
        var unlocked = Assert.Single(player.DrainGameplayChanges().OfType<SCUnlockGuideNtf>());
        Assert.Equal(behind.Order(), unlocked.GuideInfo.Select(info => info.GuideId).Order());

        player.UnlockWalkedGuides();
        Assert.Empty(player.DrainGameplayChanges());
    }

    [Fact]
    public void EnterWanted_ResumesTheRunLeftOnTheSameEntry()
    {
        var player = Fresh();
        Assert.Equal(0, player.EnterWanted(10101));
        var relic = player.Assets.Wanted.AllRelics[0].Id;
        var run = player.Wanted.CaptureRun()!;
        player.Wanted.Load([], run with { Relics = [relic] });

        Assert.Equal(0, player.EnterWanted(10101));
        Assert.Equal([relic], player.Wanted.CaptureRun()!.Relics);

        var other = player.Assets.Wanted.Entry(10102) is null ? 0u : 10102u;
        if (other != 0) Assert.Equal((int)EnmTextCode.EnmTextWantedIsInWanted, player.EnterWanted(other));
    }

    [Fact]
    public void WantedAward_QueuesOnlyAddedResources_AndRejectsReplay()
    {
        var player = Fresh();
        Assert.Equal(0, player.EnterWanted(10101));
        var oldBless = player.Assets.Wanted.AllBlesses[0].Id;
        var newBless = player.Assets.Wanted.AllBlesses[1].Id;
        var run = player.Wanted.CaptureRun()!;
        player.Wanted.Load([], run with {
            Blesses = [oldBless],
            Current = run.Current with {
                EventDone = true, Status = EnmWantedStepStatus.EnmWssTaskFinished,
                Awards = [new Lunaria.Game.Wanted.WantedStepAward(1, (uint)EWantedAwardType.AddBlessSelect, [newBless], false)]
            }
        });
        player.DrainGameplayChanges();
        var awards = player.Wanted.CaptureRun()!.Current.Awards.ToArray();
        Assert.NotEmpty(awards);

        foreach (var award in awards)
        {
            Assert.Equal(0, player.ChooseWantedAward(award.AwardId, award.Options.First(), 0).Result);
            var expected = player.Wanted.ToResource();
            var changes = player.DrainGameplayChanges();
            Assert.Equal(new[] { newBless }, Assert.Single(changes.OfType<SCWantedBlessNtf>()).BlessIds);
            Assert.Equal(expected.Bonds, Assert.Single(changes.OfType<SCWantedBondNtf>()).Bonds);
            Assert.Empty(changes.OfType<SCWantedRelicsNtf>());
            Assert.Empty(changes.OfType<SCWantedBionicsNtf>());
            Assert.NotEqual(0, player.ChooseWantedAward(award.AwardId, award.Options.First(), 0).Result);
            Assert.Empty(player.DrainGameplayChanges());
        }
    }

    [Fact]
    public void WantedAward_PartialChoiceSendsNoStepAndListsOnlyOpenAwards()
    {
        var player = Fresh();
        Assert.Equal(0, player.EnterWanted(10101));
        var bless = player.Assets.Wanted.AllBlesses[0].Id;
        var relic = player.Assets.Wanted.AllRelics[0].Id;
        var run = player.Wanted.CaptureRun()!;
        player.Wanted.Load([], run with {
            Current = run.Current with {
                EventDone = true, Status = EnmWantedStepStatus.EnmWssTaskFinished,
                Awards = [
                    new Lunaria.Game.Wanted.WantedStepAward(1, (uint)EWantedAwardType.AddBlessSelect, [bless], false),
                    new Lunaria.Game.Wanted.WantedStepAward(2, (uint)EWantedAwardType.AddRelicSelect, [relic], false)
                ]
            }
        });

        var first = player.ChooseWantedAward(1, bless, 0);
        Assert.Equal(0, first.Result);
        Assert.False(first.Finished);
        Assert.Null(first.Notification);
        Assert.Equal([2u], player.Wanted.ToStepNotification()!.StepData.AwardList.Select(a => a.Id));

        var last = player.ChooseWantedAward(2, relic, 0);
        Assert.Equal(0, last.Result);
        Assert.True(last.Finished);
        Assert.NotNull(last.Notification);
    }

    [Fact]
    public void RewardPresentation_RemainsStableAcrossLaterGrants()
    {
        var player = Fresh();
        var first = player.GrantRewards([new ItemGrant(21206001, 1)], EnmItemReason.EnmItemChangeNormal);
        var second = player.GrantRewards([new ItemGrant(21206001, 2)], EnmItemReason.EnmItemChangeShopBuy);

        var firstPopup = Assert.Single(first.Presentation.OfType<SCAwardShowNtf>());
        var secondPopup = Assert.Single(second.Presentation.OfType<SCAwardShowNtf>());
        Assert.Equal(1u, Assert.Single(firstPopup.Items).ItemCount);
        Assert.Equal(2u, Assert.Single(secondPopup.Items).ItemCount);
        Assert.Equal(EnmItemReason.EnmItemChangeNormal, firstPopup.Source);
        Assert.Equal(EnmItemReason.EnmItemChangeShopBuy, secondPopup.Source);
        Assert.Equal(3u, player.Bag.CountOf(21206001));
        Assert.DoesNotContain(player.DrainGameplayChanges(), message => message is SCAwardShowNtf);
    }
}
