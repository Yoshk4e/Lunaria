using Lunaria.Common.Tracking;
using Lunaria.Game.Logging;
using Msg;

namespace Lunaria.Game.Inventory;

public sealed partial class WalletManager : TrackedObject
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
        if (long.MaxValue - balance < amount)
            Log.Flag("wallet credit capped for currency {MoneyType}, balance {Balance}, requested amount {Amount}", moneyType, balance, amount);
        Log.Event("wallet currency {MoneyType} credit requested {Amount}, balance changed from {Before} to {After}", moneyType, amount, balance, _balances[moneyType]);

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
        {
            Log.Stage("wallet debit refused for currency {MoneyType}, balance {Balance}, requested amount {Amount}", moneyType, balance, amount);
            return (int)EnmTextCode.EnmTextItemNotEnough;
        }

        var remaining = balance - amount;
        Log.Event("wallet currency {MoneyType} debited {Amount}, remaining balance {Balance}", moneyType, amount, remaining);

        if (remaining == 0)
            _balances.Remove(moneyType);
        else
            _balances[moneyType] = remaining;

        MarkChanged(moneyType);
        return 0;
    }

    public int DebitCoin(long amount) => Debit(CoinMoneyType, amount);
}
