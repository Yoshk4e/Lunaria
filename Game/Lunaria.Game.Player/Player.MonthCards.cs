using Google.Protobuf;
using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    public IReadOnlyList<IMessage> SettleMonthCards(DateTimeOffset now)
    {
        var notifications = new List<IMessage>();
        foreach (var (cardId, grant) in MonthCards.DueDailyGrants(now))
            notifications.AddRange(DeliverMonthCardReward(cardId, grant));
        foreach (var id in MonthCards.LapsedCards(now))
            notifications.Add(new SCMonthCardOverdueNtf { Id = id });
        return notifications;
    }

    private IReadOnlyList<IMessage> DeliverMonthCardReward(uint cardId, IReadOnlyList<ItemGrant> grants)
    {
        var notifications = new List<IMessage>();
        foreach (var grant in grants)
        {
            var delivery = GrantRewards([grant], EnmItemReason.EnmItemChangeMonthcardDeliver);
            notifications.AddRange(delivery.Presentation);
            notifications.Add(new SCMonthCardRewardNtf { Id = cardId, ItemId = grant.ItemId, ItemNum = grant.Count });
        }
        return notifications;
    }
}
