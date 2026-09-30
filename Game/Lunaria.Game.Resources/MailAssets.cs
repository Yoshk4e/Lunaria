using System.Collections.Frozen;
using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

public sealed class MailAssets
{
    private readonly FrozenDictionary<uint, MailTemplate> _templates;

    public MailAssets(IReadOnlyDictionary<string, PTemplateMailTable> rows)
    {
        var templates = new Dictionary<uint, MailTemplate>();

        foreach (var row in rows.Values)
        {
            templates[row.Id] = new MailTemplate(
                row.Id,
                row.Title,
                row.From,
                row.Content,
                row.Important,
                row.Expiration,
                ParseAttachments(row.Items));
        }

        if (templates.Count == 0)
            throw new ResourceException("P_TemplateMailTable.json", "p_templatemailtable has no rows");

        _templates = templates.ToFrozenDictionary();
    }

    public int Count => _templates.Count;

    public bool Exists(uint templateId) => _templates.ContainsKey(templateId);

    public MailTemplate? Template(uint templateId) => _templates.GetValueOrDefault(templateId);

    private static IReadOnlyList<ItemGrant> ParseAttachments(List<string> raw)
    {
        var grants = new List<ItemGrant>(raw.Count);

        foreach (var entry in raw)
        {
            var parts = entry.Split(separator: ':', count: 2, StringSplitOptions.TrimEntries);

            if (parts.Length != 2
                || !uint.TryParse(parts[0], out var itemId)
                || !uint.TryParse(parts[1], out var count)
                || itemId == 0
                || count == 0)
                continue;

            grants.Add(new ItemGrant(itemId, count));
        }

        return grants;
    }
}
