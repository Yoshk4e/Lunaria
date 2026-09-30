using Lunaria.Game.Player.Gameplay;
using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    public int EnterWanted(uint entryId, IReadOnlyList<uint>? characterIds = null)
    {
        if (Dungeons.Current is not null || Battles.Current is not null)
            return (int)EnmTextCode.EnmTextWrongParam;
        InstallQuestGates();
        var code = Wanted.CheckEnter(entryId);

        if (code != 0)
            return code;

        if (characterIds is { Count: > 0 })
        {
            if (characterIds.Count > Lunaria.Game.Characters.TeamManager.MaxMembers)
                return (int)EnmTextCode.EnmTextWrongParam;
            var selection = new TeamData { TeamId = entryId };
            for (var index = 0; index < characterIds.Count; index++)
            {
                if (characterIds[index] == 0) continue;
                if (Characters.Get(characterIds[index]) is not {} character) return (int)EnmTextCode.EnmTextWrongParam;
                selection.MemberData.Add(new TeamMemberData { MemberSlotId = (uint)index + 1,
                    InstId = character.InstId, CharacterId = character.CharacterId });
            }
            var selectionResult = UpdateTemporaryTeam((int)EnmTmpTeamType.Wanted, entryId, selection);
            if (selectionResult.Result != 0) return selectionResult.Result;
        }
        if (QueryTemporaryTeam((int)EnmTmpTeamType.Wanted, entryId)?.MemberData.Count is not > 0)
            return (int)EnmTextCode.EnmTextWrongParam;
        Wanted.Enter(entryId);
        _wantedTasksStep = 0;
        Tasks.ResetNamespace(TaskAssets.Wanted);
        ReconcileTemporaryTeam();
        return 0;
    }

    public int LeaveWanted()
    {
        if (Battles.Current is not null) return (int)EnmTextCode.EnmTextWrongParam;
        var code = Wanted.Leave();
        if (code == 0) { Tasks.ResetNamespace(TaskAssets.Wanted); ReconcileTemporaryTeam(); }
        return code;
    }

    public (int Result, bool Finished, SCWantedStepNtf? Notification) ChooseWantedAward(uint stepAwardId, uint award, ulong replacedBionicsId)
    {
        var before = Wanted.ToResource();
        var type = Wanted.CaptureRun()?.Current.Awards.FirstOrDefault(a => a.AwardId == stepAwardId)?.Type;
        var result = Wanted.ChooseAward(stepAwardId, award, replacedBionicsId);
        if (result.Result == 0)
        {
            RecycleRemovedWantedCreatures(before);
            Gameplay.Publish(new WantedResourcesChanged(before, (EWantedAwardType?)type));
        }
        return result;
    }

    public int GiveUpWantedBionics(ulong id)
    {
        var before = Wanted.ToResource();
        var code = Wanted.GiveUpBionics(id);
        if (code == 0)
        {
            RecycleRemovedWantedCreatures(before);
            Gameplay.Publish(new WantedResourcesChanged(before));
        }
        return code;
    }

    private void RecycleRemovedWantedCreatures(CmdWantedResource before)
    {
        var owned = Wanted.ToResource().Bionics.Select(b => (b.UniqId, b.BionicsId)).ToHashSet();
        var price = before.Bionics.Where(b => !owned.Contains((b.UniqId, b.BionicsId)))
            .Sum(b => (long)(assets.Wanted.Creature(b.BionicsId)?.Price ?? 0));
        if (price == 0 || assets.Items.CurrencyItemFor((int)MoneyType.ThoughtSand) is not {} currency) return;
        GrantRewards([new ItemGrant(currency, checked((uint)price))], EnmItemReason.EnmItemChangeNormal);
    }

    public (bool Completed, SCWantedStepNtf? Step, RewardDelivery Delivery) ResolveWantedAdventure(
        uint adventureId,
        uint contentId,
        uint dialogId
    )
    {
        var before = Wanted.ToResource();
        var (completed, step, grants) = Wanted.OnAdventureResolved(adventureId, contentId, dialogId, optionResult: 0);
        if (completed) Gameplay.Publish(new WantedResourcesChanged(before));
        var delivery = completed ? GrantRewards(grants, EnmItemReason.EnmItemChangeWantedAdventure) : RewardDelivery.Empty;
        return (completed, step, delivery);
    }

    public (int Result, CmdWantedGoods? Goods) BuyWantedShopGood(uint shopId, uint goodsId, uint buyCount)
    {
        var (code, sandCost) = Wanted.CheckShopBuy(shopId, goodsId, buyCount);

        if (code != 0)
            return (code, null);

        var goods = new CmdWantedGoods { GoodsId = goodsId };
        var before = Wanted.ToResource();

        var paid = Purchase(
            [],
            [((int)MoneyType.ThoughtSand, sandCost)],
            () => {
                var (row, _, _, _) = Wanted.CommitShopBuy(shopId, goodsId, buyCount);
                goods = row;
                return 0;
            });

        if (paid == 0) Gameplay.Publish(new WantedResourcesChanged(before));
        return paid != 0 ? (paid, null) : (0, goods);
    }

    public int BuyWantedRevive(IReadOnlyList<ulong> characterIds)
    {
        if (Wanted.NextReviveCost() is not {} cost)
            return (int)EnmTextCode.EnmTextWantedReviveAllFailed;

        var team = CurrentTeamMembers();
        var fallen = characterIds.Where(id => team.Contains(id) && Characters.Owns(id) && Characters.Hp(id) == 0).Distinct().ToList();

        if (fallen.Count == 0)
            return (int)EnmTextCode.EnmTextWantedReviveNotExists;

        var code = Purchase(
            [],
            [((int)MoneyType.ThoughtSand, cost)],
            () => {
                var settled = Wanted.CommitRevive();

                if (settled != 0)
                    return settled;

                // ReviveMaxHp is in basis points. Permanent liquid is fully restored.
                foreach (var instId in fallen)
                {
                    Characters.SetHp(instId, (int)Math.Min(int.MaxValue, (long)Characters.MaxHp(instId) * assets.Wanted.ReviveMaxHp / 10_000));
                    Characters.SetPermanentLiquid(instId, Characters.PermanentLiquidMax(instId));
                }

                return 0;
            });

        if (code == 0)
            Gameplay.Publish(new VitalsChanged(fallen));

        return code;
    }

    public bool WantedRecover()
    {
        if (!Wanted.CanRecover())
            return false;

        var team = CurrentTeamMembers().ToList();

        foreach (var instId in team)
        {
            Characters.SetHp(instId, Characters.MaxHp(instId));
            Characters.SetPermanentLiquid(instId, Characters.PermanentLiquidMax(instId));
        }

        Gameplay.Publish(new VitalsChanged(team));
        return true;
    }

    public (int Result, SCWantedOver? Settlement, RewardDelivery? Delivery) WantedOver()
    {
        if (Battles.Current is not null) return ((int)EnmTextCode.EnmTextWrongParam, null, null);
        var (code, settlement, grants) = Wanted.Over();

        if (code != 0)
            return (code, null, null);

        Tasks.ResetNamespace(TaskAssets.Wanted);

        ReconcileTemporaryTeam();

        var delivery = GrantRewards(grants, settlement.Victory ? EnmItemReason.EnmItemChangeWantedOver : default);
        if (settlement.Victory) Gameplay.Publish(new WantedCleared(settlement.WantedId));

        if (assets.BattlePasses.Exists(settlement.BattlePassId)
            && BattlePasses.AddExp(settlement.BattlePassId, settlement.BattlePassAddScore) is { Changed: true, Data: {} pass })
        {
            settlement.BattlePassTotalScore = pass.Exp;
            Gameplay.Publish(new BattlePassChanged(settlement.BattlePassId));
        }

        return (0, settlement, delivery);
    }

    public (int Result, RewardDelivery Delivery, uint SpentStamina) WantedStaminaExchange()
    {
        if (!Wanted.IsRunning || assets.Wanted.Entry(Wanted.CurrentEntryId) is not {} entry)
            return ((int)EnmTextCode.EnmTextWantedNotInWanted, RewardDelivery.Empty, 0);
        if (!Wanted.CanRedeem)
            return ((int)EnmTextCode.EnmTextWrongParam, RewardDelivery.Empty, 0);

        var spent = assets.Wanted.Poster(entry.WantedPosterId)?.OptionalAwardCost ?? 0;
        if (spent == 0 || spent > int.MaxValue || !assets.DropTable.Exists(entry.OptionalAward))
            return ((int)EnmTextCode.EnmTextWrongParam, RewardDelivery.Empty, 0);

        var code = Progress.SpendStamina((int)spent, DateTimeOffset.UtcNow);
        if (code != 0) return (code, RewardDelivery.Empty, 0);

        var grants = assets.DropTable.Roll(entry.OptionalAward, GachaRng);
        Wanted.MarkRedeemed();
        Gameplay.Publish(new StaminaSpent(spent));
        return (0, GrantRewards(grants, EnmItemReason.EnmItemChangeWantedExchange), spent);
    }
}
