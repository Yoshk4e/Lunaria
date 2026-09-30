namespace Lunaria.Game.Resources.Tables;

/// <summary>TaskType is a UI category, not a quest namespace.</summary>
public abstract record CTaskTaskUIBase : TableRow
{
    public uint Id { get; init; }

    public uint TaskType { get; init; }

    public List<string> RewardItems { get; init; } = [];
}

[GameTable("C_TaskTaskUITable.json", Root = "C_TaskTaskUITable")]
public sealed record CTaskTaskUITable : CTaskTaskUIBase;

[GameTable("C_TaskTaskUITable_POI.json", Root = "C_TaskTaskUITable_POI")]
public sealed record CTaskTaskUITablePOI : CTaskTaskUIBase;
