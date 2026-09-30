using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Player.Managers;

public sealed record MonthCardState(uint CardId, DateTimeOffset OverdueAt, DateTimeOffset RewardAt);

public sealed record MonthCardPurchase(
    int Result, MonthCardState? State, IReadOnlyList<ItemGrant> Immediate, IReadOnlyList<ItemGrant> Accrued);

public sealed class MonthCardManager(GameData assets)
{
    private readonly SortedDictionary<uint, MonthCardState> _cards = [];

    private readonly HashSet<uint> _overdueNotified = [];

    public bool IsDirty { get; private set; }

    public IReadOnlyDictionary<uint, MonthCardState> Cards => _cards;

    /// <summary>Keep expired cards until the client receives the removal notification.</summary>
    public void Load(IEnumerable<(uint CardId, long OverdueUnix, long RewardUnix)> persisted)
    {
        _cards.Clear();
        _overdueNotified.Clear();

        foreach (var row in persisted)
        {
            if (assets.Charge.MonthCardOf(row.CardId) is null)
                continue;

            _cards[row.CardId] = new MonthCardState(
                row.CardId,
                DateTimeOffset.FromUnixTimeSeconds(row.OverdueUnix),
                DateTimeOffset.FromUnixTimeSeconds(row.RewardUnix));
        }

        IsDirty = false;
    }

    public void ClearDirty() => IsDirty = false;

    public CSMonthCardData ToMonthCardData()
    {
        var data = new CSMonthCardData {
            RewardTs = _cards.Count == 0 ? 0u : (uint)_cards.Values.Min(card => card.RewardAt).ToUnixTimeSeconds()
        };

        foreach (var card in _cards.Values)
        {
            data.MonthCardElems.Add(new CSMonthCardElem {
                Id = card.CardId,
                OverdueTs = (uint)card.OverdueAt.ToUnixTimeSeconds()
            });
        }
        return data;
    }

    /// <summary>Renewal must also pay any rewards owed from the previous subscription.</summary>
    public MonthCardPurchase Buy(uint cardId, DateTimeOffset now)
    {
        var code = CanBuy(cardId, now);
        if (code != 0) return new(code, null, [], []);

        var card = assets.Charge.MonthCardOf(cardId)!;
        var state = _cards.GetValueOrDefault(cardId);
        var accrued = state is null ? [] : CollectDailyGrant(state, now);
        state = _cards.GetValueOrDefault(cardId);
        var baseTime = state is not null && state.OverdueAt > now ? state.OverdueAt : now;
        var overdue = baseTime.AddDays(card.ChargeDays);

        var rewardAt = state is not null && state.OverdueAt > now ? state.RewardAt : now;
        _cards[cardId] = new MonthCardState(cardId, overdue, rewardAt);
        _overdueNotified.Remove(cardId);
        Dirty();
        return new(0, _cards[cardId], [new ItemGrant(card.NowItemId, card.NowNum)], accrued);
    }

    public int CanBuy(uint cardId, DateTimeOffset now)
    {
        var card = assets.Charge.MonthCardOf(cardId);
        if (card is null) return (int)EnmTextCode.EnmTextMonthCardIdInvalid;
        var remaining = _cards.TryGetValue(cardId, out var state) && state.OverdueAt > now
            ? (state.OverdueAt - now).TotalDays : 0;
        // The client allows purchase only if the renewed duration stays below DaysUplimit.
        return remaining + card.ChargeDays >= card.DaysUplimit
            ? (int)EnmTextCode.EnmTextMonthCardDayUplimit : 0;
    }

    public IReadOnlyList<(uint CardId, IReadOnlyList<ItemGrant> Grant)> DueDailyGrants(DateTimeOffset now)
    {
        var due = new List<(uint, IReadOnlyList<ItemGrant>)>();

        foreach (var state in _cards.Values.ToList())
        {
            var grant = CollectDailyGrant(state, now);
            if (grant.Count > 0) due.Add((state.CardId, grant));
        }

        return due;
    }

    private IReadOnlyList<ItemGrant> CollectDailyGrant(MonthCardState state, DateTimeOffset now)
    {
        var card = assets.Charge.MonthCardOf(state.CardId);
        if (card is null) return [];
        var effectiveEnd = now < state.OverdueAt ? now : state.OverdueAt;
        var payable = DaysBetween(state.RewardAt, effectiveEnd);
        if (payable == 0) return [];

        var amount = (ulong)card.DayNum * (uint)payable;
        var grants = new List<ItemGrant>();
        while (amount > 0)
        {
            var count = (uint)Math.Min(amount, uint.MaxValue);
            grants.Add(new ItemGrant(card.DayItemId, count));
            amount -= count;
        }
        _cards[state.CardId] = state with { RewardAt = effectiveEnd };
        Dirty();
        return grants;
    }

    public IReadOnlyList<uint> LapsedCards(DateTimeOffset now)
    {
        var lapsed = new List<uint>();

        foreach (var card in _cards.Values)
        {
            if (card.OverdueAt > now || _overdueNotified.Contains(card.CardId))
                continue;

            _overdueNotified.Add(card.CardId);
            lapsed.Add(card.CardId);
        }

        return lapsed;
    }

    private static int DaysBetween(DateTimeOffset from, DateTimeOffset to)
    {
        if (to <= from)
            return 0;

        return (int)(to.UtcDateTime.Date - from.UtcDateTime.Date).TotalDays;
    }

    private void Dirty() => IsDirty = true;
}
