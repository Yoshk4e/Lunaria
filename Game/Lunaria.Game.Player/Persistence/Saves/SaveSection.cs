using System.Text.Json;

namespace Lunaria.Game.Player.Persistence.Saves;

internal sealed record SaveSection(string Name, Func<Player, string> Capture, Func<Player, bool> Changed);

/// <summary>Committed section values for the current role session.</summary>
internal sealed class RoleSaveBaseline
{
    public long? RoleId { get; set; }
    public bool HasSections { get; set; }
    public Dictionary<string, string> Sections { get; } = [];
    public HashSet<string> Repairs { get; } = [];

    public Dictionary<string, string> CaptureChanges(Player player)
    {
        var changed = new Dictionary<string, string>();
        foreach (var section in RoleSaveMapper.Sections)
        {
            if (HasSections && !Repairs.Contains(section.Name) && !section.Changed(player)) continue;
            var value = section.Capture(player);
            if (!HasSections || !Sections.TryGetValue(section.Name, out var before) || !JsonEqual(before, value))
                changed.Add(section.Name, value);
        }
        return changed;
    }

    public void Accept(IReadOnlyDictionary<string, string> changes)
    {
        foreach (var (key, value) in changes) { Sections[key] = value; Repairs.Remove(key); }
        HasSections = true;
    }

    internal static bool JsonEqual(string left, string right)
    {
        if (left == right) return true;
        using var a = JsonDocument.Parse(left);
        using var b = JsonDocument.Parse(right);
        return JsonElement.DeepEquals(a.RootElement, b.RootElement);
    }
}
