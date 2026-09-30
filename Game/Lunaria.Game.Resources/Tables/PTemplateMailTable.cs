namespace Lunaria.Game.Resources.Tables;

/// <summary>Title, From, and Content are client text IDs. Expiration uses days and Items uses itemId:count.</summary>
[GameTable("P_TemplateMailTable.json", Root = "P_TemplateMailTable")]
public record PTemplateMailTable : TableRow
{
    public uint Id { get; init; }
    public uint Title { get; init; }
    public uint From { get; init; }
    public uint Content { get; init; }
    public bool Important { get; init; }
    public uint Expiration { get; init; }
    public List<string> Items { get; init; } = [];
    public List<uint> UnlockId { get; init; } = [];
    public string Region { get; init; } = "";
}
