using Lunaria.Game.Resources;

namespace Lunaria.Game.Mail;

public sealed record MailText(string Language, string Title, string From, string Content);

/// <summary>Items holds unclaimed attachments. An empty list sets has_rcv_attach.</summary>
public sealed record MailEntry
{
    public uint MailId { get; init; }

    /// <summary>Mail template ID, or 0 for system mail.</summary>
    public uint TemplateId { get; init; }

    public uint Type { get; init; }

    public bool Important { get; init; }

    public bool Open { get; init; }

    public IReadOnlyList<ItemGrant> Items { get; init; } = [];

    public IReadOnlyList<string> TemplateContentParams { get; init; } = [];

    public IReadOnlyList<MailText> Contents { get; init; } = [];

    /// <summary>Arrival time in Unix seconds.</summary>
    public uint Time { get; init; }

    /// <summary>Expiry time in Unix seconds, or 0 for no expiry.</summary>
    public uint ExpireTime { get; init; }

    public bool HasUnclaimed => Items.Count > 0;

    public bool HasRcvAttach => Items.Count == 0;
}
