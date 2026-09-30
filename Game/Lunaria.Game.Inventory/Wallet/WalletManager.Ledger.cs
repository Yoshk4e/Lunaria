using Msg;

namespace Lunaria.Game.Inventory;

public sealed partial class WalletManager
{
    public bool CanAfford(int moneyType, long amount) => amount >= 0 && Balance(moneyType) >= amount;

    public int Credit(int moneyType, long amount)
    {
        if (!assets.Items.IsMoneyType(moneyType))
            return (int)EnmTextCode.EnmTextWrongParam;

        if (amount < 0)
            return (int)EnmTextCode.EnmTextInvalidArgs;

        if (amount == 0)
            return 0;

        var balance = _balances.GetValueOrDefault(moneyType);
        _balances[moneyType] = long.MaxValue - balance < amount ? long.MaxValue : balance + amount;
        IsDirty = true;
        MarkChanged(moneyType);
        return 0;
    }

    public int Debit(int moneyType, long amount)
    {
        if (!assets.Items.IsMoneyType(moneyType))
            return (int)EnmTextCode.EnmTextWrongParam;

        if (amount < 0)
            return (int)EnmTextCode.EnmTextInvalidArgs;

        if (amount == 0)
            return 0;

        var balance = _balances.GetValueOrDefault(moneyType);

        if (balance < amount)
            return (int)EnmTextCode.EnmTextItemNotEnough;

        var remaining = balance - amount;

        if (remaining == 0)
            _balances.Remove(moneyType);
        else
            _balances[moneyType] = remaining;

        IsDirty = true;
        MarkChanged(moneyType);
        return 0;
    }

    public int DebitCoin(long amount) => Debit(CoinMoneyType, amount);
}
