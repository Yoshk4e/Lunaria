using Lunaria.Common.Tracking;
using Lunaria.Game.Logging;
using Lunaria.Game.Resources;

namespace Lunaria.Game.Mail;

/// <summary>BeginClaim returns the rewards. ResolveClaim keeps anything that could not be delivered.</summary>
public sealed partial class MailManager : TrackedObject
{
    public bool MarkRead(uint mailId)
    {
        if (!_mails.TryGetValue(mailId, out var entry))
            return false;

        if (entry.Open)
            return true;

        _mails[mailId] = entry with { Open = true };

        return true;
    }

    public MailClaim BeginClaim(uint mailId)
    {
        if (!_mails.TryGetValue(mailId, out var entry))
        {
            Log.Stage("mail claim refused for missing mail {MailId}", mailId);
            return MailClaim.Unknown(mailId);
        }

        if (!entry.HasUnclaimed)
        {
            Log.Stage("mail claim ignored for mail {MailId} with no remaining attachments", mailId);
            return MailClaim.AlreadyReceived(mailId);
        }

        return MailClaim.Granted(mailId, entry.Items.ToList());
    }

    public bool ResolveClaim(uint mailId, IReadOnlyList<ItemGrant> remaining)
    {
        if (!_mails.TryGetValue(mailId, out var entry))
            return false;

        var next = remaining.Where(grant => grant.ItemId != 0 && grant.Count != 0).ToList();

        if (next.SequenceEqual(entry.Items))
        {
            Log.Stage("mail claim for mail {MailId} delivered no attachments, retaining {Count} lines", mailId, next.Count);
            if (!entry.Open)
            {
                _mails[mailId] = entry with { Open = true };

            }

            return true;
        }

        _mails[mailId] = entry with { Items = next, Open = true };

        Log.Stage("mail claim resolved for mail {MailId}, attachment lines before {BeforeCount}, remaining {RemainingCount}", mailId, entry.Items.Count, next.Count);
        return true;
    }

    public bool TryDelete(uint mailId)
    {
        if (!_mails.Remove(mailId))
            return false;

        return true;
    }

    public IReadOnlyList<uint> DeleteAllRead()
    {
        var doomed = _mails.Values
            .Where(mail => mail.Open && !mail.HasUnclaimed)
            .Select(mail => mail.MailId)
            .Order()
            .ToList();

        if (doomed.Count == 0)
            return doomed;

        foreach (var mailId in doomed)
        {
            _mails.Remove(mailId);
        }

        return doomed;
    }

    public IReadOnlyList<MailEntry> Claimable() =>
        _mails.Values.Where(mail => mail.HasUnclaimed).OrderBy(mail => mail.MailId).ToList();
}
