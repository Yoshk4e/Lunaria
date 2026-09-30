namespace Lunaria.Game.Resources.Tables;

[GameTable("P_CollectionTable.json", Root = "P_CollectionTable")]
public record PCollectionTable : TableRow
{
    public uint Id { get; init; }
    public int CollectionType { get; init; }
    public uint Uuid { get; init; }
    public List<uint> DropId { get; init; } = [];
    public uint CollectionDropId { get; init; }
    public uint RefreshConfigId { get; init; }
    public uint RewardLimitId { get; init; }
    public bool AutoDestroy { get; init; }
    public float Angle { get; init; }
    public float Radius { get; init; }
    public uint RadarId { get; init; }
    public bool WantedPosterUsed { get; init; }
}
