using System.Reflection;
using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

/// <summary>
/// One attribute change from a Motive or a talent. <see cref="Add"/> is in table units (scaled for the wire like the
/// base stat) and <see cref="Multi"/> is a permyriad increase of that attribute.
/// </summary>
public readonly record struct AttributeModifier(int AttrId, int Add, int Multi);

public readonly record struct TalentContent(
    uint Type, uint Param1, int Param2, IReadOnlyList<ItemGrant> Cost, uint Coin);

/// <summary>
/// Bonuses the server folds into the attributes it sends. Online, the CBT1 client shows and fights with the server's
/// attribute data only (s_CSM_OA_CharacterOutsideAttribute keeps its own Motive map for offline mode), so neither
/// the equipped Motive nor the talent nodes count unless they are added here.
/// </summary>
public sealed class CharacterBonusAssets
{
    public const uint SkillLevelContent = 1;
    public const uint SkillUnlockContent = 2;
    public const uint AttributeContent = 3;

    private readonly Dictionary<uint, AttributeModifier[]> _motiveRows = [];
    private readonly Dictionary<(uint CharacterId, uint NodeId), TalentContent> _talents = [];
    private readonly MotiveAssets _motives;
    private readonly HashSet<int> _scaledAttributes;

    public CharacterBonusAssets(
        IReadOnlyDictionary<string, PMotiveAttributeTable> motiveAttributes,
        IReadOnlyDictionary<string, POutsideAttributeTable> outside,
        IReadOnlyDictionary<string, PTalentContentTable> talents,
        InsideAttributeAssets inside,
        MotiveAssets motives
    )
    {
        _motives = motives;
        _scaledAttributes = inside.Names.Select(pair => pair.Id).ToHashSet();

        // P_MotiveAttributeTable columns are attribute names: "ATK" adds to ATK, "ATK_Add_Rate" (the attribute's
        // AttrIncreaseEnum) raises it by a permyriad. This is how the client's own Motive calculation reads them.
        // Some enum names differ only by an underscore (AggressiveRadius_Player); Motive columns never use them.
        var flat = inside.Names.GroupBy(pair => Normalize(pair.Name))
            .ToDictionary(group => group.Key, group => group.First().Id);
        var increase = outside.Values.Where(row => !string.IsNullOrEmpty(row.AttrIncreaseEnum))
            .GroupBy(row => Normalize(row.AttrIncreaseEnum))
            .ToDictionary(group => group.Key, group => group.First().AttrEnum);
        var columns = typeof(PMotiveAttributeTable).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(int))
            .Select(p => (Property: p, Key: Normalize(p.Name)))
            .ToArray();

        foreach (var row in motiveAttributes.Values)
        {
            var modifiers = new List<AttributeModifier>();

            foreach (var (property, key) in columns)
            {
                if (property.GetValue(row) is not int value || value == 0)
                    continue;

                if (flat.TryGetValue(key, out var id))
                    modifiers.Add(new AttributeModifier(id, value, 0));
                else if (increase.TryGetValue(key, out var raised))
                    modifiers.Add(new AttributeModifier(raised, 0, value));
            }

            _motiveRows[row.Id] = [.. modifiers];
        }

        foreach (var row in talents.Values)
        {
            var cost = row.ItemIdList.Take(Math.Min(row.ItemIdList.Count, row.ItemCountList.Count))
                .Select((id, index) => new ItemGrant(id, row.ItemCountList[index]))
                .Where(grant => grant.ItemId != 0 && grant.Count > 0)
                .ToArray();
            _talents[(row.CharacterId, row.NodeId)] =
                new TalentContent(row.ContentType, row.ContentParam1, row.ContentParam2, cost, row.MoneyCount);
        }
    }

    public TalentContent? Talent(uint characterId, uint nodeId) =>
        _talents.TryGetValue((characterId, nodeId), out var content) ? content : null;

    /// <summary>
    /// Attribute nodes count once each, whatever else is unlocked. Nodes on attributes outside p_insideattributetable (1164, 1165, 1171, 1185) are skipped: their wire scale is unknown.
    /// </summary>
    public IEnumerable<AttributeModifier> TalentModifiers(uint characterId, IEnumerable<uint> unlockedNodes) =>
        unlockedNodes
            .Select(node => Talent(characterId, node))
            .Where(content => content is { Type: AttributeContent } && _scaledAttributes.Contains((int)content.Value.Param1))
            .Select(content => new AttributeModifier((int)content!.Value.Param1, content.Value.Param2, 0));

    /// <summary>The Motive's level row plus its breakthrough row, as the client's _CalcMotiveAttribute adds them.</summary>
    public IEnumerable<AttributeModifier> MotiveModifiers(uint motiveId, uint level, uint breakLevel) =>
        new[] { _motives.AddAttributeId(motiveId, level), _motives.BreakAddAttributeId(motiveId, breakLevel) }
            .Where(id => id != 0)
            .SelectMany(id => _motiveRows.GetValueOrDefault(id) ?? []);

    private static string Normalize(string name) => name.Replace("_", "").ToLowerInvariant();
}
