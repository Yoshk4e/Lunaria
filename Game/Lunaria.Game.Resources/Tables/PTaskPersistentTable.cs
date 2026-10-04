namespace Lunaria.Game.Resources.Tables;

/// <summary>Objects a quest places in the level from StepIdStart to StepIdEnd (p_taskpersistenttable_*).</summary>
public abstract record PTaskPersistentTable : TableRow
{
    public ulong Id { get; init; }
    public uint TaskType { get; init; }
    public uint TaskId { get; init; }
    public uint Index { get; init; }
    public ulong StepIdStart { get; init; }
    public ulong StepIdEnd { get; init; }
    public ulong MapId { get; init; }

    /// <summary>ETaskObjectSystemType: 4 (Normal) is a collectable the server places.</summary>
    public uint SystemType { get; init; }

    public uint TemplateId { get; init; }

    /// <summary>"x,y,z" in centimetres.</summary>
    public string Position { get; init; } = "";

    /// <summary>FRotator "pitch,yaw,roll" in degrees.</summary>
    public string Rotation { get; init; } = "";

    public int StartState { get; init; }
}

[GameTable("P_TaskPersistentTable_QuestMain.json", Root = "P_TaskPersistentTable_QuestMain")]
public sealed record PTaskPersistentTableQuestMain : PTaskPersistentTable;

[GameTable("P_TaskPersistentTable_POIQuest.json", Root = "P_TaskPersistentTable_POIQuest")]
public sealed record PTaskPersistentTablePOIQuest : PTaskPersistentTable;
