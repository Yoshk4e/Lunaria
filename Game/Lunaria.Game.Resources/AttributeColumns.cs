using System.Reflection;
using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

/// <summary>
/// Reads tables whose columns are attribute names (P_DevelopAttributeTable, P_MotiveAttributeTable): "ATK" adds to
/// ATK, and "ATK_Add_Rate", the AttrIncreaseEnum of ATK, raises it by a permyriad. Columns that name no inside
/// attribute (ATK_Pursue_BreakDEF, ATK_AbnElem_Master) are skipped, as their scale on the wire is unknown.
/// </summary>
public sealed class AttributeColumns
{
    private readonly Dictionary<string, int> _flat;
    private readonly Dictionary<string, int> _increase;
    private readonly Dictionary<Type, (PropertyInfo Property, string Key)[]> _columns = [];

    public AttributeColumns(InsideAttributeAssets inside, IReadOnlyDictionary<string, POutsideAttributeTable> outside)
    {
        // Some enum names differ only by an underscore (AggressiveRadius_Player); attribute tables never use them.
        _flat = inside.Names.GroupBy(pair => Normalize(pair.Name))
            .ToDictionary(group => group.Key, group => group.First().Id);
        _increase = outside.Values.Where(row => !string.IsNullOrEmpty(row.AttrIncreaseEnum))
            .GroupBy(row => Normalize(row.AttrIncreaseEnum))
            .ToDictionary(group => group.Key, group => group.First().AttrEnum);
    }

    public AttributeModifier[] Of<T>(T row) where T : class
    {
        if (!_columns.TryGetValue(typeof(T), out var columns))
        {
            columns = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(int))
                .Select(p => (Property: p, Key: Normalize(p.Name)))
                .ToArray();
            _columns[typeof(T)] = columns;
        }

        var modifiers = new List<AttributeModifier>();

        foreach (var (property, key) in columns)
        {
            if (property.GetValue(row) is not int value || value == 0)
                continue;

            if (_flat.TryGetValue(key, out var id))
                modifiers.Add(new AttributeModifier(id, value, 0));
            else if (_increase.TryGetValue(key, out var raised))
                modifiers.Add(new AttributeModifier(raised, 0, value));
        }

        return [.. modifiers];
    }

    private static string Normalize(string name) => name.Replace("_", "").ToLowerInvariant();
}
