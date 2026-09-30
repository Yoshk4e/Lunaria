using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Mail;

public sealed partial class MailManager(GameData assets)
{
    private readonly SortedDictionary<uint, MailEntry> _mails = [];

    public bool IsDirty { get; private set; }

    public IReadOnlyList<MailEntry> Entries => _mails.Values.ToList();

    public int Count => _mails.Count;
    public bool IsEmpty => _mails.Count == 0;

    public int BoxCap => assets.GlobalConfig.MailMaxSaveCount;

    public void Load(IEnumerable<MailEntry> persisted)
    {
        _mails.Clear();

        foreach (var mail in persisted)
        {
            if (mail.MailId == 0)
                continue;

            if (mail.TemplateId != 0 && !assets.Mail.Exists(mail.TemplateId))
                continue;

            _mails[mail.MailId] = mail with {
                Items = mail.Items.Where(grant => grant.ItemId != 0 && grant.Count != 0).ToList()
            };
        }

        IsDirty = false;
    }

    public MailEntry? Get(uint mailId) => _mails.GetValueOrDefault(mailId);

    public void ClearDirty() => IsDirty = false;

    /// <summary>A count of 0 or less requests all remaining mail, up to MAX_MAIL_LEN.</summary>
    public IReadOnlyList<MailEntry> List(uint fromMailId, int count)
    {
        var take = count <= 0 ? int.MaxValue : count;
        take = Math.Min(take, (int)EnmSizeLimit.MaxMailLen);
        return _mails.Values.Where(mail => mail.MailId > fromMailId).Take(take).ToList();
    }

    public IReadOnlyList<MailData> ListData(uint fromMailId, int count) =>
        List(fromMailId, count).Select(ToMailData).ToList();

    public MailData ToMailData(MailEntry entry)
    {
        var data = new MailData {
            MailId = entry.MailId,
            Important = entry.Important,
            Open = entry.Open,
            Time = entry.Time,
            TemplateId = entry.TemplateId,
            HasRcvAttach = entry.HasRcvAttach,
            Type = entry.Type,
            ExpireTime = entry.ExpireTime
        };

        foreach (var grant in entry.Items)
        {
            data.Items.Add(new ItemIdCount { ItemId = grant.ItemId, Count = grant.Count });
        }
        return data;
    }
}
