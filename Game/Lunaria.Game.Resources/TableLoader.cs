using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace Lunaria.Game.Resources;

internal static class TableLoader
{
    public static void Initialize(GameData output)
    {
        var tables = typeof(TableLoader).Assembly.GetTypes()
            .SelectMany(t => t.GetCustomAttributes<GameTable>().Select(attr => (Type: t, Attr: attr)))
            .OrderByDescending(t => t.Attr.Priority)
            .ThenBy(t => t.Attr.File, StringComparer.Ordinal)
            .ToList();

        Task.WaitAll(tables
            .Select(table => Task.Run(() => LoadTable(output, table.Type, table.Attr)))
            .ToArray());
    }

    private static void LoadTable(GameData output, Type rowType, GameTable table)
    {
        var dictionary = typeof(GameData)
                             .GetField(rowType.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?
                             .GetValue(output) as IDictionary
                         ?? throw new ResourceException(table.File, $"GameData has no dictionary for {rowType.Name}");

        var envelopeType = typeof(Dictionary<,>)
            .MakeGenericType(typeof(string), typeof(Dictionary<,>).MakeGenericType(typeof(string), rowType));

        byte[] raw;

        try
        {
            raw = Resources.Loader.ReadRaw(table.File);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new ResourceException(table.File,
                $"data table not found: {table.File}. The game data dump (assets/tables) is required to run; " +
                "set the AssetsDir option or LUNARIA_GameServer__AssetsDir to the folder containing it.", ex);
        }

        IDictionary? envelope;

        try
        {
            envelope = JsonSerializer.Deserialize(raw, envelopeType, ResourceJson.Options) as IDictionary;
        }
        catch (JsonException ex)
        {
            throw new ResourceException(table.File, $"malformed table JSON in {table.File}: {ex.Message}", ex);
        }

        if (envelope is null || !envelope.Contains(table.Root))
            throw new ResourceException(table.File, $"table {table.File} has no {table.Root} root object");

        foreach (DictionaryEntry entry in (IDictionary)envelope[table.Root]!)
        {
            dictionary[entry.Key] = entry.Value;
        }
    }
}
