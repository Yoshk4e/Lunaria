namespace Lunaria.Game.Resources;

/// <summary>The client resolves Title, From, and Content as text IDs with substitution parameters.</summary>
public sealed record MailTemplate(
    uint Id,
    uint Title,
    uint From,
    uint Content,
    bool Important,
    uint ExpirationDays,
    IReadOnlyList<ItemGrant> Attachments
)
{
    public bool HasAttachments => Attachments.Count > 0;
}
