namespace Lunaria.Game.Resources.Tables;

[GameTable("P_CollectionSubRegionMapping.json", Root = "P_CollectionSubRegionMapping")]
public record PCollectionSubRegionMapping : TableRow
{
    public ulong Id { get; init; }
    public List<ulong> SubRegionList { get; init; } = [];
}
