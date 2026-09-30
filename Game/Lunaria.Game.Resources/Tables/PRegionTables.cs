namespace Lunaria.Game.Resources.Tables;

[GameTable("P_RegionTable.json", Root = "P_RegionTable")]
public record PRegionTable : TableRow
{
    public uint Id { get; init; }
    public uint RegionName { get; init; }
    public List<ulong> SubRegionId { get; init; } = [];
}

[GameTable("C_SubRegionConfigReadTarget.json", Root = "C_SubRegionConfigReadTarget")]
public record CSubRegionConfigReadTarget : TableRow
{
    public ulong Id { get; init; }
    public string RegionProgressSheet { get; init; } = "";
    public string RegionSequenceSheet { get; init; } = "";
    public string SequenceUiSheet { get; init; } = "";
}

[GameTable("P_RegionProgressTable_Morgue.json", Root = "P_RegionProgressTable_Morgue")]
public record PRegionProgressTableMorgue : TableRow
{
    public ulong Id { get; init; }
    public List<uint> SequenceId { get; init; } = [];
}

[GameTable("P_RegionProgressTable_Four.json", Root = "P_RegionProgressTable_Four")]
public record PRegionProgressTableFour : TableRow
{
    public ulong Id { get; init; }
    public List<uint> SequenceId { get; init; } = [];
}

[GameTable("P_RegionProgressTable_Dayfair.json", Root = "P_RegionProgressTable_Dayfair")]
public record PRegionProgressTableDayfair : TableRow
{
    public ulong Id { get; init; }
    public List<uint> SequenceId { get; init; } = [];
}

[GameTable("P_RegionSequenceTable_Morgue.json", Root = "P_RegionSequenceTable_Morgue")]
public record PRegionSequenceTableMorgue : TableRow
{
    public uint Id { get; init; }
    public string SubRegionName { get; init; } = "";
    public uint Type { get; init; }
    public List<uint> ParamId { get; init; } = [];
    public uint ParamNum { get; init; }
}

[GameTable("P_RegionSequenceTable_Four.json", Root = "P_RegionSequenceTable_Four")]
public record PRegionSequenceTableFour : TableRow
{
    public uint Id { get; init; }
    public string SubRegionName { get; init; } = "";
    public uint Type { get; init; }
    public List<uint> ParamId { get; init; } = [];
    public uint ParamNum { get; init; }
}

[GameTable("P_RegionSequenceTable_Dayfair.json", Root = "P_RegionSequenceTable_Dayfair")]
public record PRegionSequenceTableDayfair : TableRow
{
    public uint Id { get; init; }
    public string SubRegionName { get; init; } = "";
    public uint Type { get; init; }
    public List<uint> ParamId { get; init; } = [];
    public uint ParamNum { get; init; }
}

[GameTable("P_RegionProgressTypeRegistTable.json", Root = "P_RegionProgressTypeRegistTable")]
public record PRegionProgressTypeRegistTable : TableRow
{
    public uint Id { get; init; }
    public uint RegistType { get; init; }
    public uint Weight { get; init; }
    public int IsTask { get; init; }
    public string RegistTableName { get; init; } = "";
}
