namespace Lunaria.Game.Resources.Tables;

/// <summary>Attributes use [attribute, value, ratio]. Temporary liquid uses [element, basis points].</summary>
[GameTable("P_OutsideBuffTable.json", Root = "P_OutsideBuffTable")]
public record POutsideBuffTable : TableRow
{
    public uint Id { get; init; }
    /// <summary>Duration in seconds, or -1 for no time limit.</summary>
    public int DurationTime { get; init; }
    /// <summary>Duration in battles, or -1 for no battle limit.</summary>
    public int DurationBattle { get; init; }
    public int EffectType { get; init; }
    /// <summary>BuffTargetType value 1 means the whole team.</summary>
    public int TargetType { get; init; }
    /// <summary>Buffs in the same group replace each other. Group 0 never conflicts.</summary>
    public uint MutexGroup { get; init; }
    public bool RefreshOnReuse { get; init; }
    public List<int> ImmediateAttribute1 { get; init; } = [];
    public List<int> ImmediateAttribute2 { get; init; } = [];
    public List<int> TempAttribute1 { get; init; } = [];
    public List<int> TempAttribute2 { get; init; } = [];
    public List<int> TemporaryLiquid { get; init; } = [];
}
