using Lunaria.Common;
using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Mail;

public sealed partial class MailManager
{
    public (MailEntry? Added, uint? EvictedMailId) SendFromTemplate(
        GuidManager guid,
        uint templateId,
        long nowUnixSec
    )
    {
        var template = assets.Mail.Template(templateId);

        if (template is null)
            return (null, null);

        return Insert(
            guid,
            templateId,
            (uint)EMailType.EnmMailTypeTemplate,
            template.Important,
            template.Attachments,
            nowUnixSec,
            ExpireOf(nowUnixSec, template.ExpirationDays));
    }

    public (MailEntry? Added, uint? EvictedMailId) SendFromTemplate(
        GuidManager guid,
        uint templateId,
        IReadOnlyList<ItemGrant> attachments,
        long nowUnixSec
    )
    {
        var template = assets.Mail.Template(templateId);

        if (template is null)
            return (null, null);

        return Insert(
            guid,
            templateId,
            (uint)EMailType.EnmMailTypeTemplate,
            template.Important,
            attachments,
            nowUnixSec,
            ExpireOf(nowUnixSec, template.ExpirationDays));
    }

    public (MailEntry? Added, uint? EvictedMailId) SendSystem(
        GuidManager guid,
        IReadOnlyList<ItemGrant> attachments,
        bool important,
        long nowUnixSec,
        uint expireTime = 0
    ) =>
        Insert(
            guid,
            templateId: 0,
            (uint)EMailType.EnmMailTypeSys,
            important,
            attachments,
            nowUnixSec,
            expireTime);

    public IReadOnlyList<uint> SweepExpired(long nowUnixSec)
    {
        var expired = _mails.Values
            .Where(mail => mail.ExpireTime != 0 && nowUnixSec >= mail.ExpireTime)
            .Select(mail => mail.MailId)
            .Order()
            .ToList();

        if (expired.Count == 0)
            return expired;

        foreach (var mailId in expired)
        {
            _mails.Remove(mailId);
        }
        IsDirty = true;
        return expired;
    }

    private (MailEntry? Added, uint? EvictedMailId) Insert(
        GuidManager guid,
        uint templateId,
        uint type,
        bool important,
        IReadOnlyList<ItemGrant> attachments,
        long nowUnixSec,
        uint expireTime
    )
    {
        uint? evicted = null;

        if (_mails.Count >= BoxCap)
        {
            if (!TryEvictOldest(out var evictedId))
                return (null, null);

            evicted = evictedId;
        }

        var mailId = unchecked((uint)guid.Next());

        while (mailId == 0 || _mails.ContainsKey(mailId))
            mailId = unchecked((uint)guid.Next());

        var added = new MailEntry {
            MailId = mailId,
            TemplateId = templateId,
            Type = type,
            Important = important,
            Open = false,
            Items = attachments.Where(grant => grant.ItemId != 0 && grant.Count != 0).ToList(),
            Time = unchecked((uint)nowUnixSec),
            ExpireTime = expireTime
        };
        _mails[mailId] = added;
        IsDirty = true;
        return (added, evicted);
    }

    private bool TryEvictOldest(out uint evictedMailId)
    {
        var victim = _mails.Values.FirstOrDefault(mail => mail.Open && !mail.HasUnclaimed);

        if (victim is null)
        {
            evictedMailId = 0;
            return false;
        }

        _mails.Remove(victim.MailId);
        evictedMailId = victim.MailId;
        return true;
    }

    private static uint ExpireOf(long nowUnixSec, uint expirationDays) =>
        expirationDays == 0 ? 0 : unchecked((uint)(nowUnixSec + (long)expirationDays * 86400));
}
