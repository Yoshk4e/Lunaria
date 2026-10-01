namespace Lunaria.Game.Resources.Tables;

/// <summary>Catalysts ("gems") equipped in a team member's gem slots. The id is the bag item id.</summary>
[GameTable("P_GemTable.json", Root = "P_GemTable")]
public record PGemTable : TableRow
{
    public uint Id { get; init; }
    public uint GemType { get; init; }
    public List<uint> ElementLimitType { get; init; } = [];
    public List<uint> ElementLimitNum { get; init; } = [];
    public List<uint> GemAffixId { get; init; } = [];
}

/// <summary>Key/value settings: MaxGemPerCharacter and GemCost ("1|2|4", the cost of the 1st, 2nd and 3rd gem).</summary>
[GameTable("P_GemGlobalConfig.json", Root = "P_GemGlobalConfig")]
public record PGemGlobalConfig : TableRow
{
    public uint Id { get; init; }
    public string Key { get; init; } = "";
    public string Value { get; init; } = "";
}
