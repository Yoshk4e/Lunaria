using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Player.Managers;

public sealed record HouseState(uint HouseId, EnmHouseState State, uint Level, DateTimeOffset IncomeAnchor)
{
    public uint BankedIncome { get; init; }
}

public sealed class HouseManager(GameData assets, HouseRentPolicy? rentPolicy = null)
{
    private readonly HouseRentPolicy _rent = rentPolicy ?? assets.Policy.HouseRent;
    public const MoneyType HouseCurrency = MoneyType.HouseCoins;

    private readonly SortedDictionary<uint, HouseState> _houses = [];

    public bool IsDirty { get; private set; }

    public IReadOnlyDictionary<uint, HouseState> Houses => _houses;

    public void Load(
        IEnumerable<(uint HouseId, EnmHouseState State, uint Level, long IncomeAnchor)> persisted,
        IReadOnlyDictionary<uint, uint>? bankedIncome = null
    )
    {
        _houses.Clear();

        foreach (var row in persisted)
        {
            var house = assets.Houses.Get(row.HouseId);

            if (house is null)
                continue;

            var maxLevel = assets.Houses.MaxLevel(house.LevelGroup);
            var level = Math.Clamp(row.Level, min: 1, Math.Max(maxLevel, val2: 1));
            var state = row.State is EnmHouseState.Purchased or EnmHouseState.Open ? row.State : EnmHouseState.Purchased;

            _houses[row.HouseId] = new HouseState(
                    row.HouseId, state, level, DateTimeOffset.FromUnixTimeSeconds(row.IncomeAnchor))
                { BankedIncome = bankedIncome?.GetValueOrDefault(row.HouseId) ?? 0 };
        }

        IsDirty = false;
    }

    public void ClearDirty() => IsDirty = false;

    public IReadOnlyList<HouseInfo> ToHouseInfoList(DateTimeOffset now)
    {
        var list = new List<HouseInfo>();

        foreach (var state in _houses.Values)
        {
            list.Add(ToHouseInfo(state, now));
        }
        return list;
    }

    /// <summary>Only ownership is checked here. The client checks unlockTaskId.</summary>
    public bool CanBuy(uint houseId) =>
        assets.Houses.Get(houseId) is not null && !_houses.ContainsKey(houseId);

    public HouseInfo MarkBought(uint houseId, DateTimeOffset now)
    {
        _houses[houseId] = new HouseState(
            houseId, EnmHouseState.Purchased, Level: 1, now);
        Dirty();
        return ToHouseInfo(_houses[houseId], now);
    }

    public bool CanOpen(uint houseId) =>
        _houses.TryGetValue(houseId, out var state) && state.State == EnmHouseState.Purchased;

    public HouseInfo? MarkOpened(uint houseId, DateTimeOffset now)
    {
        if (!_houses.TryGetValue(houseId, out var state) || state.State != EnmHouseState.Purchased)
            return null;

        _houses[houseId] = state with { State = EnmHouseState.Open, IncomeAnchor = now };
        Dirty();
        return ToHouseInfo(_houses[houseId], now);
    }

    public uint? NextUpgradeCost(uint houseId)
    {
        if (!_houses.TryGetValue(houseId, out var state) || state.State != EnmHouseState.Open)
            return null;

        var house = assets.Houses.Get(houseId)!;

        if (state.Level >= assets.Houses.MaxLevel(house.LevelGroup))
            return null;

        return assets.Houses.Level(house.LevelGroup, state.Level)?.UpgradeCost;
    }

    public HouseInfo? MarkUpgraded(uint houseId, DateTimeOffset now)
    {
        if (NextUpgradeCost(houseId) is null)
            return null;

        var state = _houses[houseId];

        _houses[houseId] = state with {
            Level = state.Level + 1,
            BankedIncome = AccruedIncome(houseId, now),
            IncomeAnchor = now
        };
        Dirty();
        return ToHouseInfo(_houses[houseId], now);
    }

    public IReadOnlyList<ItemGrant> IncomeDrop(uint income) =>
        income > 0 && assets.Items.CurrencyItemFor((int)HouseCurrency) is {} itemId ? [new ItemGrant(itemId, income)] : [];

    private (uint Interval, uint Max) IncomeShape() => (_rent.IntervalSeconds, _rent.MaxIntervals);

    public uint AccruedIncome(uint houseId, DateTimeOffset now)
    {
        if (!_houses.TryGetValue(houseId, out var state) || state.State != EnmHouseState.Open)
            return 0;

        var (interval, max) = IncomeShape();
        var elapsed = Math.Max(val1: 0, now.ToUnixTimeSeconds() - state.IncomeAnchor.ToUnixTimeSeconds());
        var ticks = (ulong)Math.Min(max, elapsed / interval);
        var rate = assets.Houses.IncomePerInterval(houseId, state.Level);
        return (uint)Math.Min(uint.MaxValue, Math.Min((ulong)max * rate, state.BankedIncome + ticks * rate));
    }

    public IReadOnlyList<HouseInfo> ClaimableIncome(DateTimeOffset now)
    {
        var list = new List<HouseInfo>();

        foreach (var state in _houses.Values)
        {
            if (AccruedIncome(state.HouseId, now) > 0)
                list.Add(ToHouseInfo(state, now));
        }
        return list;
    }

    public void MarkIncomeClaimed(IReadOnlyList<uint> houseIds, DateTimeOffset now)
    {
        foreach (var houseId in houseIds)
        {
            if (!_houses.TryGetValue(houseId, out var state))
                continue;

            var interval = _rent.IntervalSeconds;
            var elapsed = Math.Max(val1: 0, now.ToUnixTimeSeconds() - state.IncomeAnchor.ToUnixTimeSeconds());
            // Banked rent can be claimed while the clock is behind the last upgrade.
            // Clear that payment without moving the accrual clock backward.
            var anchor = now < state.IncomeAnchor ? state.IncomeAnchor : now.AddSeconds(-(elapsed % interval));
            _houses[houseId] = state with { IncomeAnchor = anchor, BankedIncome = 0 };
            Dirty();
        }
    }

    private HouseInfo ToHouseInfo(HouseState state, DateTimeOffset now) => new() {
        HouseId = state.HouseId,
        State = state.State,
        Level = state.Level,
        Income = AccruedIncome(state.HouseId, now)
    };

    private void Dirty() => IsDirty = true;
}
