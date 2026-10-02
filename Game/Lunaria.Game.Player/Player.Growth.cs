using Lunaria.Game.Characters;
using Lunaria.Game.Logging;
using Lunaria.Game.Player.Gameplay;
using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    public int BreakCharacter(ulong instId)
    {
        using var operationTime = BeginOperation();
        var code = Characters.CheckBreak(instId, Progress.WorldLevel);

        if (code != 0)
            return code;

        var step = Characters.NextBreak(instId)!;
        code = Purchase(step.CostItems, step.CostCurrency, () => Characters.ApplyBreak(instId, Progress.WorldLevel),
            EnmItemReason.EnmItemChangeCharacterBreak);
        if (code == 0)
        {
            Log.State("character {InstId} break succeeded, cost {CostItems}", instId, step.CostItems);
            Gameplay.Publish(new CharactersChanged([instId]));
        }
        return code;
    }

    public int RaiseSkillGroup(uint group)
    {
        using var operationTime = BeginOperation();
        var code = Skills.CheckRaise(group);

        if (code != 0)
            return code;

        var cost = Skills.NextCost(group)!;
        return Purchase(cost.Items, cost.Coin, () => Skills.ApplyRaise(group), EnmItemReason.EnmItemChangeNormal);
    }

    public ExpGrant GrantCharacterExp(ulong instId, uint exp)
    {
        using var operationTime = BeginOperation();
        var grant = Characters.GrantExp(instId, exp);
        if (grant.Code == 0) Gameplay.Publish(new CharacterLeveled(instId));
        return grant;
    }

    public CharacterLevelUpOutcome LevelUpCharacter(ulong instId, IReadOnlyList<ItemGrant> items)
    {
        using var operationTime = BeginOperation();
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
        {
            Log.Flag("character {InstId} level up refused, the bag lacks {ItemsCount} cost lines", instId, items.Count);
            return reject with { Code = (int)EnmTextCode.EnmTextCharacterLevelUpCostItemFailed };
        }

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
        {
            Log.Event("purchase refused, the bag lacks {ItemsCount} cost lines", items.Count);
            return (int)EnmTextCode.EnmTextItemNotEnough;
        }

        foreach (var (moneyType, amount) in prices)
        {
            if (!Wallet.CanAfford(moneyType, amount))
            {
                Log.Event("purchase refused, wallet {MoneyType} lacks {Amount}", moneyType, amount);
                return (int)EnmTextCode.EnmTextItemNotEnough;
            }
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
