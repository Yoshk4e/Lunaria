using Lunaria.Game.Motives;
using Lunaria.Game.Player.Gameplay;
using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    private void RetryStoredMotives(DateTimeOffset now)
    {
        if (Motives.Count >= MotiveManager.MaxMotives) return;
        var stored = Bag.All().Where(stack => assets.Items.Get(stack.ItemId) is
                { AutoUse: true, UseType: (int)ItemUseType.AddMotive, Param.Count: > 0 })
            .Select(stack => new ItemGrant(stack.ItemId, stack.Count)).ToArray();
        var changed = false;
        foreach (var grant in stored)
        {
            uint added = 0;
            var motiveId = assets.Items.Get(grant.ItemId)!.Param[0];
            while (added < grant.Count && Motives.Add(Guid, motiveId, (ulong)now.ToUnixTimeSeconds()).Ok)
                added++;
            if (added == 0) continue;
            Bag.Remove(grant.ItemId, added);
            changed = true;
            // The item was counted when stored. Count equipment acquisition now.
            Gameplay.Publish(new MotiveAcquired(motiveId, added));
        }
        if (changed) Gameplay.Publish(new BagChanged(EnmItemReason.EnmItemChangeNormal));
    }

    public int EquipMotive(ulong motiveUniq, ulong charInstId)
    {
        using var operationTime = BeginOperation();
        if (Motives.Get(motiveUniq) is null)
            return (int)EnmTextCode.EnmTextMotiveUidInvalid;

        if (Characters.Get(charInstId) is null)
            return (int)EnmTextCode.EnmTextCharacterNotExist;

        if (Motives.Get(motiveUniq)!.EquipedTarget != 0)
            return (int)EnmTextCode.EnmTextMotiveAlearyEquiped;

        if (Characters.Get(charInstId)!.MotiveUniqId != 0)
            return (int)EnmTextCode.EnmTextMotiveAlearyEquiped;

        var code = Characters.EquipMotive(charInstId, motiveUniq);

        if (code != 0)
            return code;

        code = Motives.ApplyEquip(motiveUniq, charInstId);

        if (code != 0)
        {
            Characters.ForceClearMotiveSlot(charInstId);
            return code;
        }

        Gameplay.Publish(new BagChanged(EnmItemReason.EnmItemChangeNormal));
        Gameplay.Publish(new CharactersChanged([charInstId]));
        return 0;
    }

    public int UnequipMotive(ulong motiveUniq, ulong charInstId)
    {
        using var operationTime = BeginOperation();
        if (Motives.Get(motiveUniq) is null)
            return (int)EnmTextCode.EnmTextMotiveUidInvalid;

        if (Characters.Get(charInstId) is null)
            return (int)EnmTextCode.EnmTextCharacterNotExist;

        var code = Motives.CheckUnequip(motiveUniq, charInstId);

        if (code != 0)
            return code;

        if (Characters.Get(charInstId)!.MotiveUniqId != motiveUniq)
            return (int)EnmTextCode.EnmTextMotiveUidInvalid;

        code = Characters.UnequipMotive(charInstId, motiveUniq);

        if (code != 0)
            return code;

        code = Motives.ApplyUnequip(motiveUniq, charInstId);

        if (code != 0)
        {
            Characters.EquipMotive(charInstId, motiveUniq);
            return code;
        }

        Gameplay.Publish(new BagChanged(EnmItemReason.EnmItemChangeNormal));
        Gameplay.Publish(new CharactersChanged([charInstId]));
        return 0;
    }

    public int SetMotiveLock(ulong motiveUniq, bool locked)
    {
        using var operationTime = BeginOperation();
        var code = Motives.SetLocked(motiveUniq, locked);
        if (code == 0) Gameplay.Publish(new BagChanged(EnmItemReason.EnmItemChangeMotiveLockOrUnlock));
        return code;
    }

    public MotiveLevelOutcome LevelUpMotive(
        ulong motiveUniq,
        IReadOnlyList<ItemGrant> items,
        IReadOnlyList<ulong> feedUniqs
    )
    {
        using var operationTime = BeginOperation();
        if (Motives.Get(motiveUniq) is not {} target)
            return MotiveLevelOutcome.Rejected((int)EnmTextCode.EnmTextMotiveUidInvalid);

        var cap = assets.Motives.LevelCap(target.MotiveId, target.BreakLevel);

        if (target.Level >= cap)
        {
            var max = assets.Motives.MaxLevel(target.MotiveId);
            var full = cap >= max ? (int)EnmTextCode.EnmTextMotiveLevelReachedMax : (int)EnmTextCode.EnmTextMotiveNeedBreak;
            return MotiveLevelOutcome.Rejected(full);
        }

        if (items.Count == 0 && feedUniqs.Count == 0)
            return MotiveLevelOutcome.Rejected((int)EnmTextCode.EnmTextMotiveLevelupItemInvalid);

        if (feedUniqs.Distinct().Count() != feedUniqs.Count)
            return MotiveLevelOutcome.Rejected((int)EnmTextCode.EnmTextMotiveRepeat);

        var feedStates = new List<MotiveState>(feedUniqs.Count);

        foreach (var uniq in feedUniqs)
        {
            if (uniq == motiveUniq)
                return MotiveLevelOutcome.Rejected((int)EnmTextCode.EnmTextMotiveLevelupUseSelf);

            if (Motives.Get(uniq) is not {} feed)
                return MotiveLevelOutcome.Rejected((int)EnmTextCode.EnmTextMotiveUidInvalid);

            if (feed.Locked)
                return MotiveLevelOutcome.Rejected((int)EnmTextCode.EnmTextMotiveLocked);

            if (feed.EquipedTarget != 0)
                return MotiveLevelOutcome.Rejected((int)EnmTextCode.EnmTextMotiveEquiped);

            feedStates.Add(feed);
        }

        ulong itemExp = 0;

        foreach (var grant in items)
        {
            if (grant.Count == 0)
                return MotiveLevelOutcome.Rejected((int)EnmTextCode.EnmTextMotiveLevelupItemInvalid);

            var per = Motives.ItemExpValue(grant.ItemId);

            if (per == 0)
                return MotiveLevelOutcome.Rejected((int)EnmTextCode.EnmTextMotiveLevelupItemInvalid);

            itemExp += (ulong)per * grant.Count;
        }

        ulong feedExp = 0;

        foreach (var feed in feedStates)
        {
            feedExp += Motives.FedValue(feed);
        }

        var total = itemExp + feedExp;

        if (total == 0 || total > uint.MaxValue)
            return MotiveLevelOutcome.Rejected((int)EnmTextCode.EnmTextMotiveLevelupItemInvalid);

        if (!Bag.CanAfford(items))
            return MotiveLevelOutcome.Rejected((int)EnmTextCode.EnmTextItemNotEnough);

        var oldLevel = target.Level;
        var paid = Bag.RemoveAll(items);

        if (paid != 0)
            return MotiveLevelOutcome.Rejected((int)EnmTextCode.EnmTextMotiveLevelupCostItemFailed);

        var consumed = Motives.ConsumeFeedsForLevel(motiveUniq, feedUniqs, out _);

        if (consumed != 0)
        {
            Bag.AddAll(items);
            return MotiveLevelOutcome.Rejected((int)EnmTextCode.EnmTextMotiveLevelupCostMotiveFailed);
        }

        var granted = Motives.GrantExp(motiveUniq, (uint)total);

        if (granted.Code != 0)
        {
            // The ceiling check should prevent refusal. A bag refund cannot restore consumed motives.
            return MotiveLevelOutcome.Rejected(granted.Code);
        }

        var recycle = Motives.ExpToMaterials(granted.Dropped);
        RetryStoredMotives(UtcNow);
        Gameplay.Publish(new BagChanged(EnmItemReason.EnmItemChangeMotiveLevelUp));
        return new MotiveLevelOutcome(Code: 0, oldLevel, granted.NewLevel, recycle) {
            Delivery = recycle.Count > 0 ? GrantRewards(recycle, EnmItemReason.EnmItemChangeMotiveLevelUpRecycle) : RewardDelivery.Empty
        };
    }

    public MotiveBreakOutcome BreakMotive(ulong motiveUniq)
    {
        using var operationTime = BeginOperation();
        var code = Motives.CheckBreak(motiveUniq, Progress.WorldLevel);

        if (code != 0)
        {
            var current = Motives.Get(motiveUniq);
            return new MotiveBreakOutcome(code, current?.BreakLevel ?? 0, current?.BreakLevel ?? 0);
        }

        var step = Motives.NextBreak(motiveUniq)!;
        var oldBreak = Motives.Get(motiveUniq)!.BreakLevel;
        var purchase = Purchase(step.CostItems, step.CostCurrency, () => Motives.ApplyBreak(motiveUniq, Progress.WorldLevel),
            EnmItemReason.EnmItemChangeMotiveBreak);

        if (purchase != 0)
            return new MotiveBreakOutcome(purchase, oldBreak, oldBreak);

        Gameplay.Publish(new BagChanged(EnmItemReason.EnmItemChangeMotiveBreak));
        var next = Motives.Get(motiveUniq)!.BreakLevel;
        return new MotiveBreakOutcome(Code: 0, oldBreak, next);
    }

    public MotiveRefineOutcome RefineMotive(ulong motiveUniq, IReadOnlyList<ulong> feeds)
    {
        using var operationTime = BeginOperation();
        var (code, oldRefine, newRefine) = Motives.RefineUp(motiveUniq, feeds);
        if (code == 0) RetryStoredMotives(UtcNow);
        if (code == 0) Gameplay.Publish(new BagChanged(EnmItemReason.EnmItemChangeMotiveRefineUp));
        return new MotiveRefineOutcome(code, oldRefine, newRefine);
    }

    public MotiveDecomposeOutcome DecomposeMotives(IReadOnlyList<ulong> uniqs)
    {
        using var operationTime = BeginOperation();
        var (code, removed) = Motives.Decompose(uniqs);

        if (code != 0)
            return MotiveDecomposeOutcome.Rejected(code);

        ulong total = 0;

        foreach (var state in removed)
        {
            total += Motives.FedValue(state);
        }
        var exp = total > uint.MaxValue ? uint.MaxValue : (uint)total;
        var recycle = Motives.ExpToMaterials(exp);
        RetryStoredMotives(UtcNow);
        if (recycle.Count == 0) Gameplay.Publish(new BagChanged(EnmItemReason.EnmItemChangeDecomposeMotives));
        return new MotiveDecomposeOutcome(Code: 0, recycle) {
            Delivery = recycle.Count > 0 ? GrantRewards(recycle, EnmItemReason.EnmItemChangeDecomposeMotives) : RewardDelivery.Empty
        };
    }
}

public sealed record MotiveLevelOutcome(int Code, uint OldLevel, uint NewLevel, IReadOnlyList<ItemGrant> Recycle)
{
    public RewardDelivery Delivery { get; init; } = RewardDelivery.Empty;
    public bool Ok => Code == 0;
    public static MotiveLevelOutcome Rejected(int code) => new(code, OldLevel: 0, NewLevel: 0, []);
}

public sealed record MotiveBreakOutcome(int Code, uint OldBreakLevel, uint NewBreakLevel)
{
    public bool Ok => Code == 0;
}

public sealed record MotiveRefineOutcome(int Code, uint OldRefine, uint NewRefine)
{
    public bool Ok => Code == 0;
}

public sealed record MotiveDecomposeOutcome(int Code, IReadOnlyList<ItemGrant> Recycle)
{
    public RewardDelivery Delivery { get; init; } = RewardDelivery.Empty;
    public bool Ok => Code == 0;
    public static MotiveDecomposeOutcome Rejected(int code) => new(code, []);
}
