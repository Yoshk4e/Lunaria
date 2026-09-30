using Lunaria.Game.Characters;
using Lunaria.Game.Player.Gameplay;
using Lunaria.Game.Player.Notifications;
using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    public int BreakCharacter(ulong instId)
    {
        var code = Characters.CheckBreak(instId, Progress.WorldLevel);

        if (code != 0)
            return code;

        var step = Characters.NextBreak(instId)!;
        code = Purchase(step.CostItems, step.CostCurrency, () => Characters.ApplyBreak(instId, Progress.WorldLevel),
            EnmItemReason.EnmItemChangeCharacterBreak);
        if (code == 0) Gameplay.Publish(new CharactersChanged([instId]));
        return code;
    }

    public int RaiseSkillGroup(uint group)
    {
        var code = Skills.CheckRaise(group);

        if (code != 0)
            return code;

        var cost = Skills.NextCost(group)!;
        return Purchase(cost.Items, cost.Coin, () => Skills.ApplyRaise(group), EnmItemReason.EnmItemChangeNormal);
    }

    private RewardDelivery GrantWithoutOverflowMail(IEnumerable<ItemGrant> grants, EnmItemReason reason)
    {
        var credited = new List<ItemGrant>();
        var stored = new List<ItemGrant>();
        var undelivered = new List<ItemGrant>();
        var collectedCreatures = new List<CmdSilverCreatureItem>();
        var creatureFailures = new List<ItemGrant>();
        var newcomers = new List<CharacterData>();
        var newMotives = new List<CSMotiveElem>();
        var acquired = new Dictionary<uint, uint>();

        void Acquired(uint id, uint count)
        {
            acquired[id] = (uint)Math.Min((ulong)acquired.GetValueOrDefault(id) + count, uint.MaxValue);
        }

        var changedPasses = new List<uint>();
        uint teamExpFromItems = 0;
        int? stamina = null;
        var claimTime = unchecked((ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        var pending = new Queue<(ItemGrant Grant, uint[] Ancestors)>(grants.Select(grant => (grant, Array.Empty<uint>())));

        while (pending.TryDequeue(out var entry))
        {
            var grant = entry.Grant;

            if (grant.Count == 0)
                continue;

            var useType = assets.Items.Get(grant.ItemId) is { AutoUse: true } row ? (ItemUseType)row.UseType : ItemUseType.None;

            switch (useType)
            {
                case ItemUseType.AddCoin when assets.Items.MoneyTypeOf(grant.ItemId) is {} moneyType:
                    if (Wallet.Credit(moneyType, grant.Count) != 0)
                        undelivered.Add(grant);
                    else
                    {
                        credited.Add(grant);
                        Acquired(grant.ItemId, grant.Count);
                    }
                    continue;

                case ItemUseType.AddTeamExp when assets.Items.TeamExpOf(grant.ItemId) is {} perItem:
                    var experience = (ulong)perItem * grant.Count + teamExpFromItems;

                    if (experience > uint.MaxValue)
                        undelivered.Add(grant);
                    else
                    {
                        teamExpFromItems = (uint)experience;
                        Acquired(grant.ItemId, grant.Count);
                    }
                    continue;

                case ItemUseType.AddBpExp when assets.Items.BattlePassOf(grant.ItemId) is {} passId:
                    if (BattlePasses.AddExp(passId, grant.Count).Changed && !changedPasses.Contains(passId))
                        changedPasses.Add(passId);
                    Acquired(grant.ItemId, grant.Count);
                    continue;

                // Duplicate character cards go to the bag. Only gacha converts them.
                case ItemUseType.AddCharacter when assets.Items.Get(grant.ItemId)!.Param.Count > 0: {
                    var character = Characters.Add(Guid, assets.Items.Get(grant.ItemId)!.Param[0]);

                    if (character.Ok)
                    {
                        newcomers.Add(Characters.ToCharacterData(Characters.Get(character.InstId)!));
                        Acquired(grant.ItemId, count: 1);

                        if (grant.Count == 1)
                            continue;

                        grant = grant with { Count = grant.Count - 1 };
                    }
                    break;
                }

                case ItemUseType.AddMotive when assets.Items.Get(grant.ItemId)!.Param.Count > 0: {
                    uint added = 0;
                    var motiveId = assets.Items.Get(grant.ItemId)!.Param[0];

                    while (added < grant.Count)
                    {
                        var addition = Motives.Add(Guid, motiveId, claimTime);
                        if (!addition.Ok) break;

                        newMotives.Add(Motives.ToMotiveElem(Motives.Get(addition.UniqId)!));
                        added++;
                    }

                    if (added > 0)
                    {
                        Acquired(grant.ItemId, added);
                        Gameplay.Publish(new MotiveAcquired(motiveId, added));
                    }

                    if (added == grant.Count)
                        continue;

                    grant = grant with { Count = grant.Count - added };
                    break;
                }

                case ItemUseType.AddStamina when assets.Items.StaminaOf(grant.ItemId) is {} perPoint:
                    var staminaAmount = perPoint * grant.Count;

                    if (staminaAmount <= int.MaxValue && Progress.AddStamina((int)staminaAmount, DateTimeOffset.UtcNow) == 0)
                    {
                        stamina = Progress.Stamina;
                        Acquired(grant.ItemId, grant.Count);
                    } else
                        undelivered.Add(grant);
                    continue;

                case ItemUseType.AddDrop when assets.Items.Get(grant.ItemId)!.Param.Count > 0:
                    var contents = assets.Drops.Bundle(assets.Items.Get(grant.ItemId)!.Param[0]);

                    if (entry.Ancestors.Contains(grant.ItemId) || contents.Count == 0)
                    {
                        undelivered.Add(grant);
                        continue;
                    }
                    uint[] ancestors = [.. entry.Ancestors, grant.ItemId];

                    foreach (var bundled in contents)
                    {
                        var remaining = (ulong)bundled.Count * grant.Count;

                        while (remaining > 0)
                        {
                            var chunk = (uint)Math.Min(remaining, uint.MaxValue);
                            pending.Enqueue((bundled with { Count = chunk }, ancestors));
                            remaining -= chunk;
                        }
                    }
                    continue;

                case ItemUseType.AddSilverCreature: {
                    uint added = 0;

                    while (added < grant.Count)
                    {
                        var (collected, creature) = SilverCreatures.TryCollect(grant.ItemId);

                        if (!collected || creature is null)
                            break;

                        collectedCreatures.Add(creature);
                        added++;
                    }

                    if (added > 0) Acquired(grant.ItemId, added);

                    if (added == grant.Count)
                        continue;

                    grant = grant with { Count = grant.Count - added };

                    creatureFailures.Add(grant);
                    break;
                }
            }

            var result = Bag.Add(grant.ItemId, grant.Count);

            if (result.AnyStored)
            {
                stored.Add(new ItemGrant(grant.ItemId, result.Stored));
                Acquired(grant.ItemId, result.Stored);
            }
            var missed = result.AnyStored ? result.Overflow : grant.Count;

            if (missed > 0)
                undelivered.Add(new ItemGrant(grant.ItemId, missed));
        }

        foreach (var (id, count) in acquired)
        {
            Gameplay.Publish(new ItemAcquired(id, count));
        }

        foreach (var group in collectedCreatures.GroupBy(creature => creature.ItemId))
        {
            Gameplay.Publish(new CreatureAcquired(group.Key, (uint)group.Count()));
        }
        GrantTeamExp((ulong)teamExpFromItems + TeamExpFor(reason));
        Gameplay.Publish(new WalletChanged());

        Gameplay.Publish(new BagChanged(reason));

        if (stamina is {} grantedStamina)
            Gameplay.Publish(new StaminaChanged(grantedStamina));

        if (newcomers.Count > 0) Gameplay.Publish(new CharactersAcquired(newcomers));
        foreach (var passId in changedPasses.Distinct()) Gameplay.Publish(new BattlePassChanged(passId));
        if (collectedCreatures.Count > 0) Gameplay.Publish(new CreatureRosterChanged(collectedCreatures));

        var delivery = new RewardDelivery {
            Reason = reason,
            Credited = credited, Stored = stored, Undelivered = undelivered,
            CollectedCreatures = collectedCreatures, CreatureFailures = creatureFailures,
            TeamExpFromItems = teamExpFromItems, Newcomers = newcomers, NewMotives = newMotives,
            TeamExpFromReason = TeamExpFor(reason),
            ChangedBattlePasses = changedPasses, Stamina = stamina
        };
        return delivery with { Presentation = RewardPresentation.Capture(this, delivery) };
    }

    public ExpGrant GrantCharacterExp(ulong instId, uint exp)
    {
        var grant = Characters.GrantExp(instId, exp);
        if (grant.Code == 0) Gameplay.Publish(new CharacterLeveled(instId));
        return grant;
    }

    public CharacterLevelUpOutcome LevelUpCharacter(ulong instId, IReadOnlyList<ItemGrant> items)
    {
        var reject = new CharacterLevelUpOutcome(
            Code: 0, instId, [], ExpGrant.Rejected(0));

        if (items.Count == 0)
            return reject with { Code = (int)EnmTextCode.EnmTextCharacterLevelUpItemsEmpty };

        ulong totalExp = 0;

        foreach (var (itemId, count) in items)
        {
            if (assets.Items.ExpOf(itemId) is not {} exp)
                return reject with { Code = (int)EnmTextCode.EnmTextCharacterLevelUpItemInvalid };

            totalExp += (ulong)exp * count;

            if (totalExp > uint.MaxValue)
                return reject with { Code = (int)EnmTextCode.EnmTextCharacterLevelUpItemInvalid };
        }

        if (totalExp == 0)
            return reject with { Code = (int)EnmTextCode.EnmTextCharacterLevelUpItemInvalid };

        if (!Bag.CanAfford(items))
            return reject with { Code = (int)EnmTextCode.EnmTextCharacterLevelUpCostItemFailed };

        // Charge for banked XP even when it does not gain a level.
        var grant = Characters.GrantExp(instId, (uint)totalExp);

        if (grant.Code != 0)
            return reject with { Grant = grant, Code = grant.Code };

        foreach (var (itemId, count) in items)
        {
            Bag.Remove(itemId, count);
        }

        Gameplay.Publish(new CharacterLeveled(instId));
        Gameplay.Publish(new BagChanged(EnmItemReason.EnmItemChangeCharacterLevelUp));
        return new CharacterLevelUpOutcome(Code: 0, instId, items, grant);
    }

    private int Purchase(IReadOnlyList<ItemGrant> items, uint coin, Func<int> commit, EnmItemReason bagReason = EnmItemReason.EnmItemChangeNormal) =>
        Purchase(items, coin == 0 ? [] : [(Wallet.CoinMoneyType, coin)], commit, bagReason);

    private int Purchase(
        IReadOnlyList<ItemGrant> items,
        IReadOnlyList<(int MoneyType, long Amount)> prices,
        Func<int> commit,
        EnmItemReason bagReason = EnmItemReason.EnmItemChangeNormal
    )
    {
        if (!Bag.CanAfford(items))
            return (int)EnmTextCode.EnmTextItemNotEnough;

        foreach (var (moneyType, amount) in prices)
        {
            if (!Wallet.CanAfford(moneyType, amount))
                return (int)EnmTextCode.EnmTextItemNotEnough;
        }

        var paid = Bag.RemoveAll(items);

        if (paid != 0)
            return paid;

        var debited = new List<(int MoneyType, long Amount)>(prices.Count);

        foreach (var (moneyType, amount) in prices)
        {
            paid = Wallet.Debit(moneyType, amount);

            if (paid != 0)
            {
                foreach (var prev in debited)
                {
                    Wallet.Credit(prev.MoneyType, prev.Amount);
                }
                Bag.AddAll(items);
                return paid;
            }

            debited.Add((moneyType, amount));
        }

        var code = commit();

        if (code != 0)
        {
            foreach (var prev in debited)
            {
                Wallet.Credit(prev.MoneyType, prev.Amount);
            }
            Bag.AddAll(items);
        }

        if (code == 0)
        {
            Gameplay.Publish(new WalletChanged());

            if (items.Count > 0)
                Gameplay.Publish(new BagChanged(bagReason));
        }

        return code;
    }

    public readonly record struct CharacterLevelUpOutcome(
        int Code,
        ulong InstId,
        IReadOnlyList<ItemGrant> Spent,
        ExpGrant Grant
    );
}
