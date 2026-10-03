using System.Text.Json;

namespace Lunaria.Game.Resources;

public sealed record NoticeEntry(
    uint NoticeId,
    uint NoticeType,
    string ShortTitle,
    string LongTitle,
    string Content,
    long PushTime,
    long EndTime,
    bool LoginForcePopup
)
{
    public long CreateTime { get; init; } = PushTime;
}

public sealed class NoticeAssets
{
    private readonly List<NoticeEntry> _notices = [];

    public NoticeAssets(string noticesFile)
    {
        if (!File.Exists(noticesFile))
            return;

        List<NoticeEntry> notices;

        try
        {
            notices = JsonSerializer.Deserialize<List<NoticeEntry>>(File.ReadAllText(noticesFile),
                ResourceJson.Options) ?? [];
        }
        catch (JsonException ex)
        {
            throw new ResourceException("notices.json", $"malformed notice JSON in {noticesFile}: {ex.Message}", ex);
        }

        var seen = new HashSet<uint>();

        foreach (var notice in notices)
        {
            if (notice.NoticeId == 0)
                throw new ResourceException("notices.json", "notices.json has a notice with id 0");

            if (!seen.Add(notice.NoticeId))
                throw new ResourceException("notices.json", $"notices.json names notice {notice.NoticeId} twice");

            _notices.Add(notice);
        }

        _notices.Sort((a, b) => a.NoticeId.CompareTo(b.NoticeId));
    }

    public int Count => _notices.Count;

    public IReadOnlyList<NoticeEntry> Published(DateTimeOffset now) =>
        _notices
            .Where(notice => notice.PushTime <= now.ToUnixTimeSeconds()
                             && (notice.EndTime <= 0 || notice.EndTime >= now.ToUnixTimeSeconds()))
            .ToList();
}
