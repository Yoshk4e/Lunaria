using Lunaria.Common.Tracking;
namespace Lunaria.Game.Inventory;

/// <summary>Balance updates stay queued after persistence accepts changes.</summary>
public sealed partial class WalletManager : TrackedObject
{
    [Untracked]
    private readonly HashSet<int> _changedMoney = [];

    private void MarkChanged(int moneyType) => _changedMoney.Add(moneyType);

    /// <summary>Include a zero balance for emptied currencies so the client removes them.</summary>
    public IReadOnlyList<(int MoneyType, long Amount)> ChangedBalances() =>
        _changedMoney.OrderBy(moneyType => moneyType)
            .Select(moneyType => (moneyType, Balance(moneyType)))
            .ToList();

    public void ClearChanged() => _changedMoney.Clear();

    public IReadOnlyList<int> DrainChanged()
    {
        if (_changedMoney.Count == 0)
            return [];

        var types = _changedMoney.ToList();
        _changedMoney.Clear();
        return types;
    }

    public IReadOnlyList<(int MoneyType, long Amount)> DrainChangedBalances()
    {
        var balances = ChangedBalances();
        _changedMoney.Clear();
        return balances;
    }
}
