using Lunaria.Game.Gacha;
using Lunaria.Game.Logging;
using Lunaria.Game.Player.Gameplay;
using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Player;

/// <summary>
/// SCDoGachaResult carries cost and pool info. Rewards and new characters or motives use other notifications.
/// The result screen opens on SCPreciousAwardShowNtf with the gacha reason, one entry per pull in pull order.
/// </summary>
public sealed record GachaDelivery(
    GachaCost Cost,
    GachaPoolInfo PoolInfo,
    RewardDelivery Delivery,
    IReadOnlyList<CharacterData> Newcomers,
    IReadOnlyList<CSMotiveElem> NewMotives
);

public sealed partial class Player
{
    public (int Code, GachaDelivery? Delivery) DoGacha(
        uint poolId,
        bool isMult,
        DateTimeOffset now,
        Random rng
    )
    {
        using var operationTime = BeginOperation(now);
        if (!Gacha.TryGetBanner(poolId, out var banner) || banner is null)
        {
            Log.Flag("gacha refused, pool {PoolId} has no banner", poolId);
            return ((int)EnmTextCode.EnmTextGachaDropErr, null);
        }

        var count = isMult ? 10 : 1;

        var dailyLimit = assets.Gacha.DailyLimit(banner.PoolId);

        if (dailyLimit > 0 && Gacha.DailyCountOf(poolId, now) + (ulong)count > dailyLimit)
        {
            Log.Flag("gacha refused, pool {PoolId} daily limit {DailyLimit} would be exceeded", poolId, dailyLimit);
            return ((int)EnmTextCode.EnmTextGachaDailyLimit, null);
        }

        var moneyType = assets.Gacha.CostMoneyType(banner.PoolId);

        if (!assets.Items.IsMoneyType(moneyType))
            return ((int)EnmTextCode.EnmTextGachaDropErr, null);

        var total = assets.Gacha.PullCost(banner.PoolId) * (ulong)count;
        var charge = total > long.MaxValue ? long.MaxValue : (long)total;

        if (!Wallet.CanAfford(moneyType, charge))
        {
            Log.Flag("gacha refused, wallet lacks {Charge} of money type {MoneyType} for {Count} pulls", charge, moneyType, count);
            return ((int)EnmTextCode.EnmTextGachaCostErr, null);
        }

        if (Wallet.Debit(moneyType, charge) != 0)
            return ((int)EnmTextCode.EnmTextGachaCostErr, null);

        var outcomes = Gacha.Pull(poolId, count, now, rng);

        var newcomers = new List<CharacterData>();
        var newMotives = new List<CSMotiveElem>();
        var itemish = new List<ItemGrant>();
        var results = new SCPreciousAwardShowNtf { Source = EnmItemReason.EnmItemChangeGachaReward };
        var claimTime = unchecked((ulong)now.ToUnixTimeSeconds());

        foreach (var outcome in outcomes)
        {
            bool added;

            if (outcome.Kind == PullKind.Character)
            {
                var grant = Characters.Add(Guid, outcome.Id);
                added = grant.Ok;

                if (added)
                    newcomers.Add(Characters.ToCharacterData(Characters.Get(grant.InstId)!));
            } else
            {
                var grant = Motives.Add(Guid, outcome.Id, claimTime);
                added = grant.Ok;

                if (added)
                {
                    newMotives.Add(Motives.ToMotiveElem(Motives.Get(grant.UniqId)!));
                    Gameplay.Publish(new MotiveAcquired(outcome.Id, Count: 1));
                }
            }

            var refund = added ? (ItemGrant?)null : banner.DuplicateRefund(outcome.Rarity);
            if (refund is {} converted) itemish.Add(converted);

            // Motive items share the motive id (MotiveState.ItemId).
            var shownItem = outcome.Kind == PullKind.Character ? assets.Items.CharacterCardFor(outcome.Id) :
                assets.Items.Exists(outcome.Id) ? outcome.Id : null;

            if (shownItem is not {} itemId)
            {
                Log.Flag("gacha pull {Kind} {Id} has no item to show on the result screen", outcome.Kind, outcome.Id);
                continue;
            }

            var shown = new PreciousAward { ItemId = itemId, IsNew = added };
            if (refund is {} repeat) shown.RepeatConvert.Add(new ItemIdCount { ItemId = repeat.ItemId, Count = repeat.Count });
            results.Items.Add(shown);
        }

        itemish.AddRange(Gacha.ClaimRebates(poolId));

        var delivery = GrantRewards(itemish, EnmItemReason.EnmItemChangeGachaReward);
        if (results.Items.Count > 0) delivery = delivery with { Presentation = [..delivery.Presentation, results] };
        Gameplay.Publish(new WalletChanged());
        if (newcomers.Count > 0) Gameplay.Publish(new CharactersAcquired(newcomers));
        var cost = new GachaCost { Type = unchecked((uint)moneyType), Amount = unchecked((ulong)charge) };
        Log.State("gacha pool {PoolId} resolved {Count} pulls, {Newcomers} new characters and {NewMotives} new motives",
            poolId, count, newcomers.Count, newMotives.Count);
        return (0, new GachaDelivery(cost, Gacha.PoolInfo(poolId, now), delivery, newcomers, newMotives));
    }

    public IReadOnlyList<GachaPoolInfo> GetPoolInfo(IEnumerable<uint> poolIds, DateTimeOffset now) =>
        poolIds.Select(poolId => Gacha.PoolInfo(poolId, now)).ToList();

    public (int Code, uint ClaimedMask) GetAccumulatedRewards(uint poolId) =>
        Gacha.BannerExists(poolId) ? (0, Gacha.ClaimedMaskOf(poolId)) : ((int)EnmTextCode.EnmTextGachaRebateErr, 0);
}
