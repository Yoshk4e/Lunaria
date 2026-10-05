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
        // Enrolled items belong here too: the client filters by C_ItemAwardShow, which shows the obtain popup for
        // motives (ShowType 7) and leaves character cards (ShowType 6) to the acquisition screen.
        var shown = awarded.Concat(delivery.Enrolled).ToArray();
        if (shown.Length > 0)
        {
            var show = new SCAwardShowNtf { Type = EnmAwardShowType.EnmAstLeftDown, Source = delivery.Reason };
            show.Items.AddRange(shown.Select(g => new ShowAwardInfo { ItemId = g.ItemId, ItemCount = g.Count, IsNew = true }));
            messages.Add(show);
        }

        // New characters and motives skip the bag, so only Enrolled lists them. The client opens its acquisition
        // screens from this notification (s_CSM_PAS_Dispatcher), one entry per unit.
        var precious = new SCPreciousAwardShowNtf { Source = delivery.Reason };
        foreach (var grant in delivery.Enrolled)
            for (var i = 0u; i < grant.Count; i++)
                precious.Items.Add(new PreciousAward { ItemId = grant.ItemId, IsNew = true });
        foreach (var grant in awarded)
            if (player.Assets.Items.ItemTypeOf(grant.ItemId) is ItemAssets.CharacterCardItemType or ItemAssets.MotiveItemType)
                precious.Items.Add(new PreciousAward { ItemId = grant.ItemId, IsNew = player.Bag.IsNew(grant.ItemId) });
        if (precious.Items.Count > 0) messages.Add(precious);

        foreach (var grant in delivery.DirectFailures)
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
