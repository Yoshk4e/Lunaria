using Lunaria.Game.Player.Gameplay;
using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    public (int Code, RewardDelivery Delivery) ClaimAchievementRewards(IReadOnlyList<uint> ids)
    {
        using var operationTime = BeginOperation();
        var code = Achievements.CheckClaim(ids);
        if (code != 0) return (code, RewardDelivery.Empty);
        var grants = ids.SelectMany(Achievements.RewardOf).ToArray();
        Achievements.MarkClaimed(ids);
        return (0, GrantRewards(grants, EnmItemReason.EnmItemChangeAchievement));
    }

    public (int Code, uint Level, IReadOnlyList<ItemGrant> Items, RewardDelivery Delivery) ClaimBattlePassAwards(uint passId)
    {
        using var operationTime = BeginOperation();
        if (!BattlePasses.Exists(passId))
            return ((int)EnmTextCode.EnmTextBattlePassNotExist, 0, [], RewardDelivery.Empty);
        var levels = BattlePasses.ClaimableLevels(passId);
        if (levels.Count == 0)
            return ((int)EnmTextCode.EnmTextBattlePassLevelAward, 0, [], RewardDelivery.Empty);

        var grants = levels.SelectMany(level => BattlePasses.AwardOf(passId, level)).ToArray();
        var highest = levels[^1];
        BattlePasses.MarkAwardClaimed(passId, highest);
        var delivery = GrantRewards(grants, EnmItemReason.EnmItemChangeBattlePassLevelAward);
        // Granting pass XP already sent the updated claim state.
        if (!delivery.ChangedBattlePasses.Contains(passId)) Gameplay.Publish(new BattlePassChanged(passId));
        return (0, highest, grants, delivery);
    }

    public RewardDelivery ClaimDailyMissionRewards()
    {
        using var operationTime = BeginOperation();
        var deliveries = new List<RewardDelivery>();
        foreach (var reward in DailyMissions.EligibleRewards())
        {
            DailyMissions.MarkRewardClaimed(reward.Id);
            // Keep the team XP from each newly claimed reward.
            deliveries.Add(GrantRewards(Assets.DailyMissions.RewardItems(reward.Id), EnmItemReason.EnmItemChangeDailyMissionReward));
        }
        return RewardDelivery.Combine(deliveries);
    }

    public (int Code, IReadOnlyList<uint> Values, RewardDelivery Delivery) ClaimRegionRewards(ulong subRegionId)
    {
        using var operationTime = BeginOperation();
        var eligible = RegionProgress.EligibleRewards(subRegionId);
        if (eligible.Count == 0)
            return ((int)EnmTextCode.EnmTextSubRegionNoAvailableRewards, [], RewardDelivery.Empty);
        var values = eligible.Select(reward => reward.PrograssValue).ToArray();
        var grants = eligible.SelectMany(RegionProgress.RewardOf).ToArray();
        RegionProgress.MarkClaimed(subRegionId, values);
        return (0, values, GrantRewards(grants, EnmItemReason.EnmItemChangeRegionProgress));
    }

    public (int Code, IReadOnlyList<ItemGrant> Items, RewardDelivery Delivery) ClaimSignInReward(uint activityId, uint day)
    {
        using var operationTime = BeginOperation();
        var (code, items) = SignIn.Claim(activityId, day);
        return (code, items, code == 0 ? GrantRewards(items, EnmItemReason.EnmItemChangeSigninActivity) : RewardDelivery.Empty);
    }

    public (IReadOnlyList<uint> Changed, RewardDelivery Delivery) ReadGuides(IReadOnlyList<uint> ids)
    {
        using var operationTime = BeginOperation();
        var changed = Guides.MarkRead(ids);

        if (changed.Count == 0)
            return ([], RewardDelivery.Empty);

        Gameplay.Publish(new GuidesChanged(changed));

        var grants = changed.SelectMany(BrowseRewardsOf).ToArray();
        return (changed, grants.Length > 0 ? GrantRewards(grants, EnmItemReason.EnmItemChangeGuide) : RewardDelivery.Empty);
    }

    private IReadOnlyList<ItemGrant> BrowseRewardsOf(uint guideId)
    {
        var dropId = Assets.Guides.BrowseRewardOf(guideId);
        return Assets.Drops.Exists(dropId) ? Assets.Drops.Bundle(dropId) : Assets.DropTable.Roll(dropId, RandomSources.Loot);
    }
}
