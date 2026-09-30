namespace Lunaria.Game.Resources.Tables;

/// <summary>Each award rolls its Probability independently as a percentage.</summary>
[GameTable("S_WantedPosterAwardTable.json", Root = "S_WantedPosterAwardTable")]
public record SWantedPosterAwardTable : TableRow
{
    public uint Id { get; init; }
    public uint Probability { get; init; }
    public EWantedAwardType Behavior { get; init; }
    // See the effect-event explanation sheet for parameter meanings.
    public List<uint> Param1 { get; init; } = [];
    public List<uint> Param2 { get; init; } = [];
    public List<uint> Param3 { get; init; } = [];
    public List<uint> Param4 { get; init; } = [];
}
