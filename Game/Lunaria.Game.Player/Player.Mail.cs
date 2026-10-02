using Lunaria.Game.Logging;
using Lunaria.Game.Mail;
using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    private readonly List<IReadOnlyList<ItemGrant>> _pendingRewardMail = [];
    private bool _pendingRewardMailDirty;

    /// <summary>Save rewards with the operation that earned them while they wait for mailbox space.</summary>
    public IReadOnlyList<IReadOnlyList<ItemGrant>> PendingRewardMail => _pendingRewardMail;

    internal void LoadPendingRewardMail(IEnumerable<IReadOnlyList<ItemGrant>> pending)
    {
        _pendingRewardMail.Clear();
        _pendingRewardMail.AddRange(pending.Select(batch => (IReadOnlyList<ItemGrant>)batch.ToArray()));
        _pendingRewardMailDirty = false;
    }

    public (int Code, RewardDelivery? Delivery) ClaimMailAttachments(uint mailId)
    {
        using var operationTime = BeginOperation();
        ExpireMailBeforeClaim();
        var claim = Mails.BeginClaim(mailId);

        if (!claim.Ok)
            return (claim.Code, null);

        var delivery = GrantWithoutOverflowMail(claim.Attachments, EnmItemReason.EnmItemChangeMail);
        Mails.ResolveClaim(mailId, delivery.Undelivered);
        return (0, PresentRewards(delivery));
    }

    public (IReadOnlyList<uint> ClaimedIds, RewardDelivery Delivery) ClaimAllMailAttachments()
    {
        using var operationTime = BeginOperation();
        ExpireMailBeforeClaim();
        var claimed = new List<uint>();
        var deliveries = new List<RewardDelivery>();

        foreach (var entry in Mails.Claimable())
        {
            var delivery = GrantWithoutOverflowMail(entry.Items, EnmItemReason.EnmItemChangeMail);
            Mails.ResolveClaim(entry.MailId, delivery.Undelivered);
            deliveries.Add(PresentRewards(delivery));
            if (delivery.Undelivered.Count == 0) claimed.Add(entry.MailId);
        }
        return (claimed, RewardDelivery.Combine(deliveries));
    }

    private void ExpireMailBeforeClaim()
    {
        var expired = Mails.SweepExpired(UtcNow.ToUnixTimeSeconds());
        if (expired.Count > 0) _changes.Add(new SCMailAddDelNft { DelMailIds = { expired } });
    }

    public RewardDelivery GrantRewards(IEnumerable<ItemGrant> grants, EnmItemReason reason)
    {
        using var operationTime = BeginOperation();
        var delivery = GrantWithoutOverflowMail(grants, reason);
        if (delivery.Undelivered.Count == 0) return PresentRewards(delivery);

        Log.Flag("grant reason {Reason} left {Count} undelivered items, sending overflow mail", reason, delivery.Undelivered.Count);

        if (!TrySendOverflowMail(delivery.Undelivered, UtcNow))
        {
            _pendingRewardMail.Add(delivery.Undelivered.ToArray());
            _pendingRewardMailDirty = true;
            Log.Flag("overflow mail failed too, deferring {Count} items to the next mailbox sweep", delivery.Undelivered.Count);
            return PresentRewards(delivery with { Deferred = delivery.Undelivered, Undelivered = [] });
        }
        return PresentRewards(delivery with { Mailed = delivery.Undelivered, Undelivered = [] });
    }

    private void RetryPendingRewardMail(DateTimeOffset now)
    {
        while (_pendingRewardMail.Count > 0 && TrySendOverflowMail(_pendingRewardMail[0], now))
        {
            _pendingRewardMail.RemoveAt(0);
            _pendingRewardMailDirty = true;
        }
    }

    private bool TrySendOverflowMail(IReadOnlyList<ItemGrant> grants, DateTimeOffset now)
    {
        var (added, evicted) = Mails.SendFromTemplate(Guid, assets.GlobalConfig.BagFullMailId, grants, now.ToUnixTimeSeconds());
        var update = new SCMailAddDelNft();
        if (added is not null) update.AddMails.Add(Mails.ToMailData(added));
        if (evicted is {} id) update.DelMailIds.Add(id);
        if (update.AddMails.Count + update.DelMailIds.Count > 0) _changes.Add(update);
        return added is not null;
    }
}
