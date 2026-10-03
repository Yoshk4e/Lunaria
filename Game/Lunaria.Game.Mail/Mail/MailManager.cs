using Lunaria.Common.Tracking;
using Google.Protobuf;
using Lunaria.Game.Logging;
using Lunaria.Game.Resources;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.Game.Mail;

public sealed partial class MailManager(GameData assets) : TrackedObject
{
    private static readonly ILogger Log = GameLog.Create("Lunaria.Game.Mail");

    private readonly TrackedSortedDictionary<uint, MailEntry> __tracked_mails = [];
    [Tracked]
    private partial TrackedSortedDictionary<uint, MailEntry> _mails { get; }
    public IReadOnlyList<uint> ChangedIds => _mails.Changes.ChangedKeys.Cast<uint>().ToArray();

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

        AcceptLoadedState();
    }

    public MailEntry? Get(uint mailId) => _mails.GetValueOrDefault(mailId);

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
            ExpireTime = entry.ExpireTime,
            TemplateContentParams = { entry.TemplateContentParams.Select(param => new MailTemplateContentParam {
                Param = ByteString.CopyFromUtf8(param)
            }) },
            Contents = { entry.Contents.Select(content => new MailContent {
                Language = ByteString.CopyFromUtf8(content.Language),
                Title = ByteString.CopyFromUtf8(content.Title),
                From = ByteString.CopyFromUtf8(content.From),
                Content = ByteString.CopyFromUtf8(content.Content)
            }) }
        };

        foreach (var grant in entry.Items)
        {
            data.Items.Add(new ItemIdCount { ItemId = grant.ItemId, Count = grant.Count });
        }
        return data;
    }
}
