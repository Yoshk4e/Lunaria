using Lunaria.Common.Tracking;
using Lunaria.Game.Logging;
using Lunaria.Game.Resources;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.Game.Inventory;

public sealed partial class WalletManager(GameData assets) : TrackedObject
{
    private static readonly ILogger Log = GameLog.Create("Lunaria.Game.Inventory.Wallet");

    private readonly TrackedSortedDictionary<int, long> __tracked_balances = [];
    [Tracked]
    private partial TrackedSortedDictionary<int, long> _balances { get; }

    public bool IsEmpty => _balances.Count == 0;

    public int CoinMoneyType => assets.Starter.CoinMoneyType;

    public long Coin => Balance(CoinMoneyType);

    public bool GrantStarter()
    {
        if (_balances.Count > 0)
            return false;

        foreach (var grant in assets.Starter.CurrencyGrants)
        {
            Credit(assets.Items.MoneyTypeOf(grant.ItemId)!.Value, grant.Count);
        }

        return _balances.Count > 0;
    }

    public void Load(IEnumerable<(int MoneyType, long Amount)> persisted)
    {
        _balances.Clear();

        foreach (var (moneyType, amount) in persisted)
        {
            if (assets.Items.IsMoneyType(moneyType) && amount > 0)
                _balances[moneyType] = amount;
        }
        _changedMoney.Clear();
        AcceptLoadedState();
    }

    public long Balance(int moneyType) => _balances.GetValueOrDefault(moneyType);

    public IEnumerable<(int MoneyType, long Amount)> All() =>
        _balances.Select(kv => (kv.Key, kv.Value));

    public IReadOnlyList<PlayerMoney> MoneyData() =>
        All().Select(pair => new PlayerMoney { Type = pair.MoneyType, Amount = pair.Amount }).ToList();
}
