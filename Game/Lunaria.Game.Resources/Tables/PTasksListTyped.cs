namespace Lunaria.Game.Resources.Tables;

public abstract record PTasksListTyped : TableRow
{
    public uint Id { get; init; }

    public uint TaskType { get; init; }

    public int LoadingType { get; init; }

    public List<ulong> Steps { get; init; } = [];

    public List<uint> NextTasks { get; init; } = [];

    public uint RewardId { get; init; }
}

[GameTable("P_TasksList_QuestMain.json", Root = "P_TasksList_QuestMain")]
public sealed record PTasksListQuestMain : PTasksListTyped;

[GameTable("P_TasksList_POIQuest.json", Root = "P_TasksList_POIQuest")]
public sealed record PTasksListPOIQuest : PTasksListTyped;

[GameTable("P_TasksList_Wanted.json", Root = "P_TasksList_Wanted")]
public sealed record PTasksListWanted : PTasksListTyped;

[GameTable("P_TasksList_DailyTask.json", Root = "P_TasksList_DailyTask")]
public sealed record PTasksListDailyTask : PTasksListTyped;
