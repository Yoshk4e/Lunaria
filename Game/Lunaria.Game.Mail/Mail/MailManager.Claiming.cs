using Lunaria.Game.Resources;

namespace Lunaria.Game.Mail;

/// <summary>BeginClaim returns the rewards. ResolveClaim keeps anything that could not be delivered.</summary>
public sealed partial class MailManager
{
    public bool MarkRead(uint mailId)
    {
        if (!_mails.TryGetValue(mailId, out var entry))
            return false;

        if (entry.Open)
            return true;

        _mails[mailId] = entry with { Open = true };
        IsDirty = true;
        return true;
    }

    public MailClaim BeginClaim(uint mailId)
    {
        if (!_mails.TryGetValue(mailId, out var entry))
            return MailClaim.Unknown(mailId);

        if (!entry.HasUnclaimed)
            return MailClaim.AlreadyReceived(mailId);

        return MailClaim.Granted(mailId, entry.Items.ToList());
    }

    public bool ResolveClaim(uint mailId, IReadOnlyList<ItemGrant> remaining)
    {
        if (!_mails.TryGetValue(mailId, out var entry))
            return false;

        var next = remaining.Where(grant => grant.ItemId != 0 && grant.Count != 0).ToList();

        if (next.SequenceEqual(entry.Items))
        {
            if (!entry.Open)
            {
                _mails[mailId] = entry with { Open = true };
                IsDirty = true;
            }

            return true;
        }

        _mails[mailId] = entry with { Items = next, Open = true };
        IsDirty = true;
        return true;
    }

    public bool TryDelete(uint mailId)
    {
        if (!_mails.Remove(mailId))
            return false;

        IsDirty = true;
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
        IsDirty = true;
        return doomed;
    }

    public IReadOnlyList<MailEntry> Claimable() =>
        _mails.Values.Where(mail => mail.HasUnclaimed).OrderBy(mail => mail.MailId).ToList();
}
