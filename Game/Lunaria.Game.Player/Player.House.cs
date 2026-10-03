using Lunaria.Common.Tracking;
using Lunaria.Game.Player.Gameplay;
using Lunaria.Game.Player.Managers;
using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    [Untracked]
    private readonly Dictionary<uint, uint> _announcedIncome = [];

    public (int Code, HouseInfo? Info) BuyHouse(uint houseId)
    {
        using var operationTime = BeginOperation();
        var now = UtcNow;

        if (!Houses.CanBuy(houseId))
            return ((int)EnmTextCode.EnmTextHouseAlreadyOwn, null);

        var price = assets.Houses.Get(houseId)!.Price;
        HouseInfo? info = null;

        var code = Purchase(
            [],
            [((int)HouseManager.HouseCurrency, price)],
            () => {
                info = Houses.MarkBought(houseId, now);
                return 0;
            });

        if (code == 0) Gameplay.Publish(new HousePurchased(houseId));
        return code != 0 ? (code, null) : (0, info);
    }

    public (int Code, HouseInfo? Info) OpenHouse(uint houseId)
    {
        using var operationTime = BeginOperation();
        var now = UtcNow;

        if (!Houses.CanOpen(houseId))
            return ((int)EnmTextCode.EnmTextHouseNotBuy, null);

        return (0, Houses.MarkOpened(houseId, now));
    }

    public (int Code, HouseInfo? Info) UpgradeHouse(uint houseId)
    {
        using var operationTime = BeginOperation();
        var now = UtcNow;
        var cost = Houses.NextUpgradeCost(houseId);

        if (cost is null)
            return ((int)EnmTextCode.EnmTextHouseSurroundCannotUpgrade, null);

        HouseInfo? info = null;

        var code = Purchase(
            [],
            [((int)HouseManager.HouseCurrency, (long)cost)],
            () => {
                info = Houses.MarkUpgraded(houseId, now);
                return info is null ? (int)EnmTextCode.EnmTextHouseSurroundCannotUpgrade : 0;
            });

        if (code == 0) Gameplay.Publish(new HouseUpgraded(houseId));
        return code != 0 ? (code, null) : (0, info);
    }

    public RewardDelivery ClaimHouseIncome()
    {
        using var operationTime = BeginOperation();
        var now = UtcNow;
        var claimable = Houses.ClaimableIncome(now);

        if (claimable.Count == 0)
            return RewardDelivery.Empty;

        var grants = new List<ItemGrant>();

        foreach (var info in claimable)
        {
            var income = Houses.IncomeDrop(info.Income);

            if (income.Count == 0)
                continue;

            grants.AddRange(income);
            _announcedIncome[info.HouseId] = 0;
        }

        Houses.MarkIncomeClaimed(claimable.Where(info => Houses.IncomeDrop(info.Income).Count > 0).Select(info => info.HouseId).ToList(), now);
        return grants.Count > 0 ? GrantRewards(grants, EnmItemReason.EnmItemChangeHouseIncome) : RewardDelivery.Empty;
    }

    public IReadOnlyList<HouseInfo> DueHouseIncomeAnnouncements(DateTimeOffset now)
    {
        using var operationTime = BeginOperation(now);
        var due = new List<HouseInfo>();

        foreach (var info in Houses.ToHouseInfoList(now))
        {
            if (info.Income <= _announcedIncome.GetValueOrDefault(info.HouseId))
                continue;

            _announcedIncome[info.HouseId] = info.Income;
            due.Add(info);
        }

        return due;
    }
}
