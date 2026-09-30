namespace Lunaria.Game.Resources.Tables;

[GameTable("P_MapDataTable.json", Root = "P_MapDataTable")]
public record PMapDataTable : TableRow
{
    public ulong Id { get; init; }
    public uint WorldId { get; init; }
    public uint ZoneId { get; init; }
    public uint RegionId { get; init; }
    public uint SubRegionId { get; init; }
    public string LevelPath { get; init; } = "";
    public int ModuleType { get; init; }
    public uint BClientOnly { get; init; }
    public uint BManipulateRole { get; init; }
    public uint Bsubregion { get; init; }
    public string DefaultPos { get; init; } = "";
    public string DefaultRot { get; init; } = "";
    public uint NpcFlowDistributeId { get; init; }
    public int WorldType { get; init; }
    public int BackgroundMusicId { get; init; }
    public int PresetTod { get; init; }
    public List<uint> SubLevelIds { get; init; } = [];
}
