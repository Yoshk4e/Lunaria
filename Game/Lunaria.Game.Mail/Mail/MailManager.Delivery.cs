using Lunaria.Common.Tracking;
using Lunaria.Common;
using Lunaria.Game.Logging;
using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Mail;

public sealed partial class MailManager : TrackedObject
{
    public (MailEntry? Added, uint? EvictedMailId) SendFromTemplate(
        GuidManager guid,
        uint templateId,
        long nowUnixSec,
        IReadOnlyList<string>? templateContentParams = null
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
            ExpireOf(nowUnixSec, template.ExpirationDays), templateContentParams);
    }

    public (MailEntry? Added, uint? EvictedMailId) SendFromTemplate(
        GuidManager guid,
        uint templateId,
        IReadOnlyList<ItemGrant> attachments,
        long nowUnixSec,
        IReadOnlyList<string>? templateContentParams = null
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
            ExpireOf(nowUnixSec, template.ExpirationDays), templateContentParams);
    }

    public (MailEntry? Added, uint? EvictedMailId) SendSystem(
        GuidManager guid,
        IReadOnlyList<ItemGrant> attachments,
        bool important,
        long nowUnixSec,
        IReadOnlyList<MailText> contents,
        uint expireTime = 0
    )
    {
        if (contents.Count == 0) return (null, null);
        return Insert(
            guid,
            templateId: 0,
            (uint)EMailType.EnmMailTypeSys,
            important,
            attachments,
            nowUnixSec,
            expireTime, contents: contents);
    }

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

        Log.Stage("mail sweep removed {Count} expired messages at {Now}", expired.Count, nowUnixSec);
        return expired;
    }

    private (MailEntry? Added, uint? EvictedMailId) Insert(
        GuidManager guid,
        uint templateId,
        uint type,
        bool important,
        IReadOnlyList<ItemGrant> attachments,
        long nowUnixSec,
        uint expireTime,
        IReadOnlyList<string>? templateContentParams = null,
        IReadOnlyList<MailText>? contents = null
    )
    {
        uint? evicted = null;

        if (_mails.Count >= BoxCap)
        {
            if (!TryEvictOldest(out var evictedId))
            {
                Log.Flag("mail delivery refused for template {TemplateId}, mailbox count {Count} at capacity {Capacity} with no evictable mail", templateId, _mails.Count, BoxCap);
                return (null, null);
            }

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
            ExpireTime = expireTime,
            TemplateContentParams = templateContentParams?.ToArray() ?? [],
            Contents = contents?.ToArray() ?? []
        };
        _mails[mailId] = added;

        Log.Stage("mail {MailId} inserted with template {TemplateId} type {MailType}, attachment lines {AttachmentCount}, evicted mail {EvictedMailId}",
            mailId, templateId, type, added.Items.Count, evicted);
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
