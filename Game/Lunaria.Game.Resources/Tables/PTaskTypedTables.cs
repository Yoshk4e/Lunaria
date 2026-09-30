namespace Lunaria.Game.Resources.Tables;

/// <summary>
/// Task IDs are unique within each namespace. FailAction and RollbackStepWhenFail control failure recovery.
/// </summary>
public abstract record PTaskStepsTyped : TableRow
{
    public ulong Id { get; init; }

    public uint TaskType { get; init; }

    public uint OriginTaskId { get; init; }

    public List<ulong> Actions { get; init; } = [];

    public List<ulong> PostId { get; init; } = [];

    public List<ulong> PostFailaction { get; init; } = [];

    public List<ulong> FailAction { get; init; } = [];

    public ulong RollbackStepWhenFail { get; init; }

    /// <summary>Applied after step completion. Mode 2 specifies an hour of day.</summary>
    public List<string> PushGameTime { get; init; } = [];

    public uint SetGameWeather { get; init; }
}

[GameTable("P_TaskSteps_QuestMain.json", Root = "P_TaskSteps_QuestMain")]
public sealed record PTaskStepsQuestMain : PTaskStepsTyped;

[GameTable("P_TaskSteps_POIQuest.json", Root = "P_TaskSteps_POIQuest")]
public sealed record PTaskStepsPOIQuest : PTaskStepsTyped;

[GameTable("P_TaskSteps_Wanted.json", Root = "P_TaskSteps_Wanted")]
public sealed record PTaskStepsWanted : PTaskStepsTyped;

[GameTable("P_TaskSteps_DailyTask.json", Root = "P_TaskSteps_DailyTask")]
public sealed record PTaskStepsDailyTask : PTaskStepsTyped;

public abstract record PTaskActionsTyped : TableRow
{
    public ulong Id { get; init; }

    public uint TaskType { get; init; }

    public ulong OriginStep { get; init; }

    public bool Necessary { get; init; }

    public bool ServerSave { get; init; }

    public int TargetType { get; init; }

    public int ServerTargetType { get; init; }

    public string ClientParam1 { get; init; } = "";

    public string ClientParam2 { get; init; } = "";

    public string ServerParam1 { get; init; } = "";

    public string ServerParam2 { get; init; } = "";

    public ulong MapId { get; init; }
}

[GameTable("P_TaskActions_QuestMain.json", Root = "P_TaskActions_QuestMain")]
public sealed record PTaskActionsQuestMain : PTaskActionsTyped;

[GameTable("P_TaskActions_POIQuest.json", Root = "P_TaskActions_POIQuest")]
public sealed record PTaskActionsPOIQuest : PTaskActionsTyped;

[GameTable("P_TaskActions_Wanted.json", Root = "P_TaskActions_Wanted")]
public sealed record PTaskActionsWanted : PTaskActionsTyped;

[GameTable("P_TaskActions_DailyTask.json", Root = "P_TaskActions_DailyTask")]
public sealed record PTaskActionsDailyTask : PTaskActionsTyped;
