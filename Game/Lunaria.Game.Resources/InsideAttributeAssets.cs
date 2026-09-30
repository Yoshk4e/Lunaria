using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

public sealed class InsideAttributeAssets
{
    private readonly Dictionary<string, int> _byEnum = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, bool> _permyriad = [];

    public InsideAttributeAssets(IReadOnlyDictionary<string, PInsideAttributeTable> rows)
    {
        foreach (var row in rows.Values)
        {
            _permyriad[(int)row.Id] = row.AllowPermyriad;
            _byEnum[row.AttrEnum] = (int)row.Id;
        }

        Attr = new AttributeIds(
            RequireId("MAXHP"),
            RequireId("HP"),
            RequireId("ATK"),
            RequireId("DEF"),
            RequireId("ATK_CRITICALCHANCE"),
            RequireId("ATK_CRITICALDAMAGE"),
            RequireId("ATK_BODYPARTBREAK_ADD_RATE"),
            RequireId("SHIELD"),
            RequireId("PERMANENT_LIQUID"),
            RequireId("PERMANENT_LIQUID_MAX"));
    }

    public AttributeIds Attr { get; }

    public bool IsPermyriad(int id) => _permyriad.GetValueOrDefault(id);

    public int Id(string enumName) =>
        _byEnum.TryGetValue(enumName, out var id) ?
            id :
            throw new ResourceException("P_InsideAttributeTable.json", $"p_insideattributetable: missing attribute {enumName}");

    private int RequireId(string name) => Id(name);

    /// <summary>GetValueForBattle divides flat stats by 10000 but uses permyriad rates directly.</summary>
    public int Scale(int attrId, int value) => IsPermyriad(attrId) ? value : value * 10_000;
}
