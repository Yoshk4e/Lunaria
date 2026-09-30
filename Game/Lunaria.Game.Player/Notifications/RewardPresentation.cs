using Google.Protobuf;
using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Player.Notifications;

internal static class RewardPresentation
{
    public static IReadOnlyList<IMessage> Capture(Player player, RewardDelivery delivery)
    {
        var messages = new List<IMessage>();
        var awarded = delivery.Credited.Concat(delivery.Stored).ToArray();
        if (awarded.Length > 0)
        {
            var show = new SCAwardShowNtf { Type = EnmAwardShowType.EnmAstLeftDown, Source = delivery.Reason };
            show.Items.AddRange(awarded.Select(g => new ShowAwardInfo { ItemId = g.ItemId, ItemCount = g.Count, IsNew = true }));
            messages.Add(show);
        }

        var precious = new SCPreciousAwardShowNtf { Source = delivery.Reason };
        foreach (var grant in awarded)
            if (player.Assets.Items.ItemTypeOf(grant.ItemId) is ItemAssets.CharacterCardItemType or ItemAssets.MotiveItemType)
                precious.Items.Add(new PreciousAward { ItemId = grant.ItemId, IsNew = player.Bag.IsNew(grant.ItemId) });
        if (precious.Items.Count > 0) messages.Add(precious);

        foreach (var grant in delivery.Undelivered)
        {
            var full = player.Bag.CountOf(grant.ItemId) == 0 && player.Bag.IsFull;
            messages.Add(new SCItemAddErrorNtf {
                ItemId = grant.ItemId,
                Result = (int)(full ? EnmTextCode.EnmTextPackageFull : EnmTextCode.EnmTextItemHoldMax)
            });
        }

        if (delivery.CreatureFailures.Count > 0)
        {
            var failed = new SCSilverCreatureCollectFail();
            failed.Items.AddRange(delivery.CreatureFailures.Select(g => new ItemIdCount { ItemId = g.ItemId, Count = g.Count }));
            messages.Add(failed);
        }
        return messages;
    }
}
