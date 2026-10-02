using Lunaria.Game.Characters;
using Lunaria.Game.Player.Gameplay;
using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    public ItemUseOutcome UseItem(uint itemId, uint count, IReadOnlyList<ulong> parameters)
    {
        using var operationTime = BeginOperation();
        var previousStamina = Progress.Stamina;
        var result = UseItemCore(itemId, count, parameters);

        // Natural regeneration can still apply when item use fails.
        if (Progress.Stamina != previousStamina)
            Gameplay.Publish(new StaminaChanged(Progress.Stamina));

        if (result.Code == 0 && result.Used > 0)
        {
            Gameplay.Publish(new BagChanged(EnmItemReason.EnmItemChangeUse));

            if (result.ChangedCdType != 0 && result.ChangedCdReadyUnix != 0)
                Gameplay.Publish(new CooldownStarted(result.ChangedCdType, result.ChangedCdReadyUnix));
            if (result.ChangedCharacters.Count > 0)
                Gameplay.Publish(new VitalsChanged(result.ChangedCharacters));
            if (result.ChangedTemporaryLiquid)
                Gameplay.Publish(new LiquidChanged());
            if (result.ChangedSatiety is {} satiety)
                Gameplay.Publish(new SatietyChanged(satiety));
            if (result.ChangedBattlePass != 0)
                Gameplay.Publish(new BattlePassChanged(result.ChangedBattlePass));
            if (result.BuffChanges is { Count: > 0 } || result.BuffRemoved is { Count: > 0 })
                Gameplay.Publish(new BuffsChanged(result.BuffChanges ?? [], result.BuffRemoved ?? []));

            // Capture the spend before ItemUsed can trigger task rewards and change its snapshot.
            Gameplay.Publish(new ItemUsed(itemId, result.Used));
        }

        return new ItemUseOutcome(result.Code, result.Used, result.Remaining);
    }

    private ItemUseEffects UseItemCore(uint itemId, uint count, IReadOnlyList<ulong> parameters)
    {
        var held = Bag.CountOf(itemId);
        var reject = new ItemUseEffects(Code: 0, Used: 0, held, [], ChangedTemporaryLiquid: false);

        if (count == 0)
            return reject with { Code = (int)EnmTextCode.EnmTextItemUseZero };

        if (assets.Items.Get(itemId) is not {} item)
            return reject with { Code = (int)EnmTextCode.EnmTextWrongParam };

        // Battle-pass XP items have no effect row. Each gives one XP, and UseLimit 0 means unlimited.
        if (assets.Items.BattlePassOf(itemId) is {} passId)
        {
            if (held < count)
                return reject with { Code = (int)EnmTextCode.EnmTextItemNotEnough };

            if (Bag.Remove(itemId, count) != 0)
                return reject with { Code = (int)EnmTextCode.EnmTextItemNotEnough };

            var (changed, _) = BattlePasses.AddExp(passId, count);
            return new ItemUseEffects(Code: 0, count, Bag.CountOf(itemId), [], ChangedTemporaryLiquid: false, changed ? passId : 0);
        }

        // Stamina items have no effect row and can exceed the natural regeneration cap.
        if (assets.Items.StaminaOf(itemId) is {} staminaPerUse)
        {
            if (staminaPerUse <= 0)
                return reject with { Code = (int)EnmTextCode.EnmTextItemNoUsehandler };

            var staminaNow = UtcNow;
            Progress.Regenerate(staminaNow);
            var missing = Progress.StaminaMax - Progress.Stamina;

            if (missing <= 0)
                return reject with { Code = (int)EnmTextCode.EnmTextStaminaRecoverMax };

            var staminaUses = Math.Min(count, (uint)Math.Ceiling((double)missing / staminaPerUse));

            if (Bag.Remove(itemId, staminaUses) != 0)
                return reject with { Code = (int)EnmTextCode.EnmTextItemNotEnough };

            Progress.AddStamina((int)Math.Min((long)staminaPerUse * staminaUses, int.MaxValue), staminaNow);

            return new ItemUseEffects(Code: 0, staminaUses, Bag.CountOf(itemId), [], ChangedTemporaryLiquid: false,
                ChangedBattlePass: 0, BuffChanges: null, BuffRemoved: null, ChangedCdType: 0, ChangedCdReadyUnix: 0, ChangedSatiety: null,
                Progress.Stamina);
        }

        if (count > item.UseLimit)
            return reject with { Code = (int)EnmTextCode.EnmTextItemUseTooMuch };

        if (held < count)
            return reject with { Code = (int)EnmTextCode.EnmTextItemNotEnough };

        if (assets.ItemEffects.Effect(itemId) is not {} effect)
            return reject with { Code = (int)EnmTextCode.EnmTextItemNoUsehandler };

        var now = UtcNow;
        uint cdType = 0;
        uint cdSeconds = 0;
        uint cdReadyUnix = 0;

        if (assets.ItemEffects.Cooldown(itemId) is {} cooldown)
        {
            if (Cooldowns.ReadyAt(cooldown.TypeId) > now)
                return reject with { Code = (int)EnmTextCode.EnmTextItemCd };

            cdType = cooldown.TypeId;
            cdSeconds = cooldown.Seconds;
        }

        if (!TryResolveItemTargets(effect, parameters, out var targets))
            return reject with { Code = (int)EnmTextCode.EnmTextWrongParam };

        var hasImmediate = TryResolveItemPlan(effect, targets, out var plan);
        var hasTimedBuffs = effect.TimedBuffs.Count > 0;
        if (!hasImmediate && !hasTimedBuffs)
            return reject with { Code = (int)EnmTextCode.EnmTextItemNoUsehandler };

        if (hasImmediate && plan.Kind == ItemEffectAssets.Blood && !effect.Revive)
            targets = targets.Where(id => TeamCharacterHp(id) > 0).ToArray();

        var usable = hasImmediate ? Math.Min(count, plan.Kind switch {
            ItemEffectAssets.Blood => UsesUntilHpFull(targets, plan),
            ItemEffectAssets.PermanentLiquid => UsesUntilPermanentLiquidFull(targets[0], plan),
            _ => UsesUntilTemporaryLiquidFull(plan)
        }) : 0;

        // A timed buff consumes one item even when its healing has nothing to restore.
        // Its immediate effects still apply to eligible targets from that same use.
        if (hasTimedBuffs) usable = 1;

        if (usable == 0)
            return reject;

        if (Bag.Remove(itemId, usable) != 0)
            return reject with { Code = (int)EnmTextCode.EnmTextItemNotEnough };

        cdReadyUnix = Cooldowns.Start(cdType, cdSeconds, now);

        var changedCharacters = new List<ulong>();
        var changedLiquid = false;

        if (hasImmediate && plan.Kind == ItemEffectAssets.Blood)
        {
            foreach (var instId in targets)
            {
                var perUse = HpPerUse(instId, TeamCharacterMaxHp(instId), plan);
                var before = TeamCharacterHp(instId);
                SetTeamCharacterVitals(instId, hp: (int)Math.Min(int.MaxValue, before + usable * perUse));

                if (TeamCharacterHp(instId) != before)
                    changedCharacters.Add(instId);
            }
        } else if (hasImmediate && plan.Kind == ItemEffectAssets.PermanentLiquid)
        {
            var before = TeamCharacterLiquid(targets[0]);
            SetTeamCharacterVitals(targets[0], liquid: before + checked((int)usable * plan.Value));

            if (TeamCharacterLiquid(targets[0]) != before)
                changedCharacters.Add(targets[0]);
        } else if (hasImmediate)
        {
            changedLiquid = AddCurrentTeamLiquid(plan.Element, checked((int)usable * plan.Value));
        }

        var buffChanges = new List<(PBBuffData Data, bool Refreshed)>();
        var buffRemoved = new List<uint>();
        foreach (var buffId in effect.TimedBuffs)
        {
            var (code, refreshed, updated, displaced) = Buffs.Apply(buffId, now);
            if (code != 0) continue;
            if (updated is not null) buffChanges.Add((updated, refreshed));
            buffRemoved.AddRange(displaced);
        }

        var satiety = ApplySatiety(effect.Satiety, usable);

        return new ItemUseEffects(Code: 0, usable, Bag.CountOf(itemId), changedCharacters, changedLiquid,
            ChangedBattlePass: 0, buffChanges, buffRemoved, cdType, cdReadyUnix, satiety);
    }

    private bool TryResolveItemTargets(
        ItemUseEffect effect,
        IReadOnlyList<ulong> parameters,
        out IReadOnlyList<ulong> targets
    )
    {
        if (effect.Effects.Count == 0)
        {
            targets = [];
            return false;
        }

        var targetType = effect.Effects[0].TargetType;

        targets = targetType switch {
            1 or 2 => CurrentTeamMembers(),
            3 => parameters.Count == 1 && (Characters.Owns(parameters[0]) || Trial(parameters[0]) is not null) ? [parameters[0]] : [],
            4 => parameters.Count == 1 && Characters.InstanceOf((uint)parameters[0]) is {} character ? [character.InstId] : [],
            _ => []
        };

        return targets.Count > 0;
    }

    private bool TryResolveItemPlan(
        ItemUseEffect effect,
        IReadOnlyList<ulong> targets,
        out ItemEffectPlan plan
    )
    {
        plan = default;

        if (targets.Count == 0 || effect.Effects.Count == 0)
            return false;

        foreach (var immediate in effect.Effects)
        {
            if (effect.Kind is ItemEffectAssets.Blood or ItemEffectAssets.PermanentLiquid)
            {
                var blood = effect.Kind == ItemEffectAssets.Blood;

                var firstMatches = blood ?
                    immediate.FirstAttributeId is not 0
                    && (immediate.FirstAttributeId == assets.Inside.Attr.Hp
                        || immediate.FirstAttributeId == assets.Inside.Attr.Maxhp) :
                    immediate.FirstAttributeId is not 0
                    && (immediate.FirstAttributeId == assets.Inside.Attr.PermanentLiquid
                        || immediate.FirstAttributeId == assets.Inside.Attr.PermanentLiquidMax);

                var secondMatches = blood ?
                    immediate.SecondAttributeId is not 0
                    && (immediate.SecondAttributeId == assets.Inside.Attr.Hp
                        || immediate.SecondAttributeId == assets.Inside.Attr.Maxhp) :
                    immediate.SecondAttributeId is not 0
                    && (immediate.SecondAttributeId == assets.Inside.Attr.PermanentLiquid
                        || immediate.SecondAttributeId == assets.Inside.Attr.PermanentLiquidMax);

                if (firstMatches || secondMatches)
                {
                    plan = new ItemEffectPlan(
                        effect.Kind,
                        firstMatches ? immediate.FirstValue : immediate.SecondValue,
                        firstMatches ? immediate.FirstRatio : immediate.SecondRatio,
                        Element: 0);
                    return plan.Value > 0 || blood && plan.Ratio > 0;
                }
            } else if (effect.Kind == ItemEffectAssets.LiquidSilver
                       && immediate.TemporaryLiquidElement is >= 1 and <= 7
                       && immediate.TemporaryLiquidAmount > 0)
            {
                plan = new ItemEffectPlan(
                    effect.Kind,
                    immediate.TemporaryLiquidAmount,
                    Ratio: 0,
                    immediate.TemporaryLiquidElement);
                return true;
            }
        }

        return false;
    }

    private int HpPerUse(ulong instId, int maxHp, ItemEffectPlan plan) =>
        (int)Math.Clamp(plan.Value + (long)maxHp * plan.Ratio / 10_000, min: 0, int.MaxValue);

    private int? ApplySatiety(int perUse, uint consumed)
    {
        if (perUse <= 0 || consumed == 0)
            return null;

        var before = Progress.Satiety;
        Progress.SetSatiety(before + perUse * (int)consumed);
        return Progress.Satiety == before ? null : Progress.Satiety;
    }

    private uint UsesUntilHpFull(IReadOnlyList<ulong> targets, ItemEffectPlan plan)
    {
        uint usable = 0;

        foreach (var instId in targets)
        {
            var maxHp = TeamCharacterMaxHp(instId);
            var perUse = HpPerUse(instId, maxHp, plan);

            if (perUse <= 0)
                return 0;

            var missing = maxHp - TeamCharacterHp(instId);

            if (missing > 0)
                usable = Math.Max(usable, (uint)Math.Ceiling((double)missing / perUse));
        }

        return usable;
    }

    private uint UsesUntilPermanentLiquidFull(ulong instId, ItemEffectPlan plan) =>
        plan.Value <= 0 ?
            0 :
            (uint)Math.Ceiling(
                (double)Math.Max(val1: 0, TeamCharacterMaxLiquid(instId) - TeamCharacterLiquid(instId))
                / plan.Value);

    private uint UsesUntilTemporaryLiquidFull(ItemEffectPlan plan)
    {
        if (plan.Value <= 0)
            return 0;

        var liquid = CurrentTeamLiquid;

        var current = plan.Element switch {
            1 => liquid.Fire,
            2 => liquid.Ice,
            3 => liquid.Thunder,
            4 => liquid.Gravity,
            5 => liquid.Radiate,
            6 => liquid.Silver,
            7 => liquid.Blackiron,
            _ => TeamLiquid.MaxBasisPoints
        };
        var missing = TeamLiquid.MaxBasisPoints - current;
        return missing <= 0 ? 0 : (uint)Math.Ceiling((double)missing / plan.Value);
    }

    public readonly record struct ItemUseOutcome(int Code, uint Used, uint Remaining);

    private readonly record struct ItemUseEffects(
        int Code,
        uint Used,
        uint Remaining,
        IReadOnlyList<ulong> ChangedCharacters,
        bool ChangedTemporaryLiquid,
        uint ChangedBattlePass = 0,
        IReadOnlyList<(PBBuffData Data, bool Refreshed)>? BuffChanges = null,
        IReadOnlyList<uint>? BuffRemoved = null,
        uint ChangedCdType = 0,
        uint ChangedCdReadyUnix = 0,
        int? ChangedSatiety = null,
        int? ChangedStamina = null
    );

    private readonly record struct ItemEffectPlan(int Kind, int Value, int Ratio, int Element);
}
