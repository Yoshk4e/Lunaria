using Lunaria.Game.Logging;
using Lunaria.Game.Player.Gameplay;
using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    public int EnterWanted(uint entryId, IReadOnlyList<uint>? characterIds = null)
    {
        using var operationTime = BeginOperation();
        if (Dungeons.Current is not null || Battles.Current is not null)
        {
            Log.Flag("wanted {EntryId} entry refused, a dungeon or battle is active", entryId);
            return (int)EnmTextCode.EnmTextWrongParam;
        }
        InstallQuestGates();
        // Continue on the Case Simulation screen sends the same request for the run left in progress
        // (SCWantedOutsideData current_id): resume it instead of starting over.
        var resume = Wanted.CaptureRun()?.EntryId == entryId;
        var code = resume ? 0 : Wanted.CheckEnter(entryId);

        if (code != 0)
            return code;

        // The entry request carries the main team (s_CSM_WPE_WantedPosterEnter.RequestEnterWP sends
        // TeamData:GetCurrentTeam().characters), while the lineup picked on the wanted screen arrives earlier through the
        // temporary team update. Use the request only when no lineup was picked for this entry.
        var picked = _temporarySelections.ContainsKey((EnmTmpTeamType.Wanted, entryId));
        if (characterIds is { Count: > 0 } && !picked)
        {
            if (characterIds.Count > Lunaria.Game.Characters.TeamManager.MaxMembers)
                return (int)EnmTextCode.EnmTextWrongParam;
            var previous = QueryTemporaryTeam((int)EnmTmpTeamType.Wanted, entryId);
            var selection = new TeamData { TeamId = entryId };
            for (var index = 0; index < characterIds.Count; index++)
            {
                if (characterIds[index] == 0) continue;
                if (Characters.Get(characterIds[index]) is not {} character) return (int)EnmTextCode.EnmTextWrongParam;
                var member = previous?.MemberData.FirstOrDefault(m => m.InstId == character.InstId)?.Clone()
                    ?? new TeamMemberData { InstId = character.InstId, CharacterId = character.CharacterId };
                member.MemberSlotId = (uint)index + 1;
                selection.MemberData.Add(member);
            }
            var selectionResult = UpdateTemporaryTeam((int)EnmTmpTeamType.Wanted, entryId, selection);
            if (selectionResult.Result != 0) return selectionResult.Result;
        }
        if (QueryTemporaryTeam((int)EnmTmpTeamType.Wanted, entryId)?.MemberData.Count is not > 0)
            return (int)EnmTextCode.EnmTextWrongParam;
        if (resume)
            Log.Flag("wanted run on entry {EntryId} resumed at step {Step}", entryId, Wanted.CurrentStep);
        else
        {
            Wanted.Enter(entryId);
            _wantedTasksStep = 0;
            Tasks.ResetNamespace(TaskAssets.Wanted);
        }
        WantedSuspended = false;
        ReconcileTemporaryTeam();
        return 0;
    }

    /// <summary>
    /// CS_WANTED_LEAVE is "End for now": the client goes back to the open world and Case Simulation then offers
    /// Continue or End (CS_WANTED_OVER) for the run. Keep the run and suspend it; it only closes on settlement.
    /// </summary>
    public int LeaveWanted()
    {
        using var operationTime = BeginOperation();
        if (Battles.Current is not null) return (int)EnmTextCode.EnmTextWrongParam;
        if (Wanted.IsRunning)
        {
            Log.Event("wanted run on entry {EntryId} left at step {Step}, kept for Continue", Wanted.CurrentEntryId, Wanted.CurrentStep);
            WantedSuspended = true;
            ReconcileTemporaryTeam();
            return 0;
        }
        var code = Wanted.Leave();
        Log.Event("wanted leave returned {Result}", code);
        if (code == 0) { Tasks.ResetNamespace(TaskAssets.Wanted); ReconcileTemporaryTeam(); }
        return code;
    }

    public (int Result, bool Finished, SCWantedStepNtf? Notification) ChooseWantedAward(uint stepAwardId, uint award, ulong replacedBionicsId)
    {
        using var operationTime = BeginOperation();
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
        using var operationTime = BeginOperation();
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

    public (int Result, bool Completed, SCWantedStepNtf? Step, RewardDelivery Delivery) ResolveWantedAdventure(
        uint adventureId,
        uint contentId,
        uint dialogId
    )
    {
        using var operationTime = BeginOperation();
        var before = Wanted.ToResource();
        var (code, completed, step, grants) = Wanted.OnAdventureResolved(adventureId, contentId, dialogId, optionResult: 0);
        if (completed) Gameplay.Publish(new WantedResourcesChanged(before));
        var delivery = completed ? GrantRewards(grants, EnmItemReason.EnmItemChangeWantedAdventure) : RewardDelivery.Empty;
        return (code, completed, step, delivery);
    }

    public (int Result, CmdWantedGoods? Goods) BuyWantedShopGood(uint shopId, uint goodsId, uint buyCount)
    {
        using var operationTime = BeginOperation();
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
        using var operationTime = BeginOperation();
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
        using var operationTime = BeginOperation();
        if (Battles.Current is not null || !Wanted.CanRecover())
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
        using var operationTime = BeginOperation();
        if (Battles.Current is not null)
        {
            Log.Flag("wanted over refused, battle still active");
            return ((int)EnmTextCode.EnmTextWrongParam, null, null);
        }
        var (code, settlement, grants) = Wanted.Over();

        if (code != 0)
            return (code, null, null);

        Tasks.ResetNamespace(TaskAssets.Wanted);

        ReconcileTemporaryTeam();

        var delivery = GrantRewards(grants, settlement.Victory ? EnmItemReason.EnmItemChangeWantedOver : default);
        settlement.AwardFirst.Clear();
        settlement.AwardFirst.AddRange(RewardItems(grants));
        if (settlement.Victory) Gameplay.Publish(new WantedCleared(settlement.WantedId));

        if (assets.BattlePasses.Exists(settlement.BattlePassId))
        {
            var (changed, pass) = BattlePasses.AddExp(settlement.BattlePassId, settlement.BattlePassAddScore);
            settlement.BattlePassTotalScore = pass.Exp;
            if (changed) Gameplay.Publish(new BattlePassChanged(settlement.BattlePassId));
        }

        return (0, settlement, delivery);
    }

    public (int Result, RewardDelivery Delivery, uint SpentStamina) WantedStaminaExchange()
    {
        using var operationTime = BeginOperation();
        if (!Wanted.IsRunning || assets.Wanted.Entry(Wanted.CurrentEntryId) is not {} entry)
            return ((int)EnmTextCode.EnmTextWantedNotInWanted, RewardDelivery.Empty, 0);
        if (!Wanted.CanRedeem)
            return ((int)EnmTextCode.EnmTextWrongParam, RewardDelivery.Empty, 0);

        var spent = assets.Wanted.Poster(entry.WantedPosterId)?.OptionalAwardCost ?? 0;
        if (spent == 0 || spent > int.MaxValue || !assets.DropTable.Exists(entry.OptionalAward))
            return ((int)EnmTextCode.EnmTextWrongParam, RewardDelivery.Empty, 0);

        var code = Progress.SpendStamina((int)spent, UtcNow);
        if (code != 0) return (code, RewardDelivery.Empty, 0);

        var grants = assets.DropTable.Roll(entry.OptionalAward, RandomSources.Loot);
        Wanted.MarkRedeemed();
        Gameplay.Publish(new StaminaSpent(spent));
        return (0, GrantRewards(grants, EnmItemReason.EnmItemChangeWantedExchange), spent);
    }
}
