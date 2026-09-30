namespace Lunaria.Game.Resources.Tables;

[GameTable("P_CaseTable.json", Root = "P_CaseTable")]
public record PCaseTable : TableRow
{
    public uint Id { get; init; }
    public List<ulong> StageIdList { get; init; } = [];
}

[GameTable("P_CaseStageTable.json", Root = "P_CaseStageTable")]
public record PCaseStageTable : TableRow
{
    public ulong Id { get; init; }
    public uint CaseId { get; init; }
    public List<ulong> ClueIdList { get; init; } = [];
}

[GameTable("P_CaseClueTable.json", Root = "P_CaseClueTable")]
public record PCaseClueTable : TableRow
{
    public ulong Id { get; init; }
    public uint CaseId { get; init; }
}

[GameTable("P_CaseEvidenceTable.json", Root = "P_CaseEvidenceTable")]
public record PCaseEvidenceTable : TableRow
{
    public ulong Id { get; init; }
    public uint CaseId { get; init; }
}

[GameTable("P_AchievementTable.json", Root = "P_AchievementTable")]
public record PAchievementTable : TableRow
{
    public uint Id { get; init; }
    public uint FinishId { get; init; }
    public uint DropId { get; init; }
    public uint Point { get; init; }
}

[GameTable("P_BattlePass.json", Root = "P_BattlePass")]
public record PBattlePass : TableRow
{
    public uint Id { get; init; }
    public uint BpExpItem { get; init; }
}

[GameTable("P_BattlePassLevel.json", Root = "P_BattlePassLevel")]
public record PBattlePassLevel : TableRow
{
    public uint Id { get; init; }
    public uint Bp { get; init; }
    public uint Level { get; init; }
    public uint ExpNeed { get; init; }
    public List<string> Award { get; init; } = [];
    public bool LandmarkOrNot { get; init; }
}

[GameTable("P_HouseV2Table.json", Root = "P_HouseV2Table")]
public record PHouseV2Table : TableRow
{
    public uint Id { get; init; }
    public uint HouseType { get; init; }
    public uint LevelGroup { get; init; }
    public uint BaseIncome { get; init; }
    public uint Price { get; init; }
    public uint UnlockTaskId { get; init; }
}

[GameTable("P_HouseLevelTable.json", Root = "P_HouseLevelTable")]
public record PHouseLevelTable : TableRow
{
    public uint Id { get; init; }
    public uint GroupId { get; init; }
    public uint Level { get; init; }
    public uint UpgradeCost { get; init; }
    public uint BaseIncome { get; init; }
    public List<uint> PropertyList { get; init; } = [];
    public List<uint> PropertyIncomeCoef { get; init; } = [];
}

[GameTable("P_HouseIndustryTable.json", Root = "P_HouseIndustryTable")]
public record PHouseIndustryTable : TableRow
{
    public uint Id { get; init; }
    public uint DropId { get; init; }
    public uint DropInterval { get; init; }
    public uint OnlineDropNum { get; init; }
    public uint OfflineDropNum { get; init; }
    public uint MaxDropNum { get; init; }
}

[GameTable("P_DailyMissionTable.json", Root = "P_DailyMissionTable")]
public record PDailyMissionTable : TableRow
{
    public uint Id { get; init; }
    public uint Name { get; init; }
    public uint Event { get; init; }
    public uint ActivePoint { get; init; }
}

[GameTable("P_DailyMissionRewardTable.json", Root = "P_DailyMissionRewardTable")]
public record PDailyMissionRewardTable : TableRow
{
    public uint Id { get; init; }
    public uint NeedActivePoint { get; init; }
    public List<string> Items { get; init; } = [];
}

[GameTable("P_GlobalEventFinishTable.json", Root = "P_GlobalEventFinishTable")]
public record PGlobalEventFinishTable : TableRow
{
    public uint Id { get; init; }
    public uint SubType { get; init; }
    public uint NeedCount { get; init; }
    public List<string> Args { get; init; } = [];
    public uint RecordType { get; init; }
}

[GameTable("P_ActivityTable.json", Root = "P_ActivityTable")]
public record PActivityTable : TableRow
{
    public uint Id { get; init; }
    public uint Type { get; init; }
    public uint TimeType { get; init; }
    public ulong TimeOffsetStart { get; init; }
    public ulong TimeOffsetStop { get; init; }
}

[GameTable("P_SignInActivityRewardTable.json", Root = "P_SignInActivityRewardTable")]
public record PSignInActivityRewardTable : TableRow
{
    public uint Id { get; init; }
    public uint ActivityId { get; init; }
    public uint Day { get; init; }
    public List<string> Items { get; init; } = [];
}

[GameTable("P_RegionProgressTable.json", Root = "P_RegionProgressTable")]
public record PRegionProgressTable : TableRow
{
    public ulong Id { get; init; }
    public List<uint> SequenceId { get; init; } = [];
}

[GameTable("P_RegionRewardTable.json", Root = "P_RegionRewardTable")]
public record PRegionRewardTable : TableRow
{
    public ulong Id { get; init; }
    /// <summary>The dump writes name IDs as decimals, such as "1010500121.0".</summary>
    public double SubRegionName { get; init; }
    public List<uint> Reward { get; init; } = [];
}

[GameTable("P_RegionRewardDataTable.json", Root = "P_RegionRewardDataTable")]
public record PRegionRewardDataTable : TableRow
{
    public uint Id { get; init; }
    public uint PrograssValue { get; init; }
    public uint PrograssName { get; init; }
    public uint DropId { get; init; }
}

[GameTable("P_RegionSequenceTable.json", Root = "P_RegionSequenceTable")]
public record PRegionSequenceTable : TableRow
{
    public uint Id { get; init; }
    /// <summary>The dump writes name IDs as decimals, such as "1010500121.0".</summary>
    public double SubRegionName { get; init; }
    public uint Type { get; init; }
    public List<uint> ParamId { get; init; } = [];
    public List<uint> ParamNum { get; init; } = [];
}

[GameTable("P_SilverCreatureCombineTable.json", Root = "P_SilverCreatureCombineTable")]
public record PSilverCreatureCombineTable : TableRow
{
    public uint Id { get; init; }
    public uint SrcItemNum { get; init; }
    public uint DstItemId { get; init; }
}

[GameTable("P_SilverCreatureGrowthTable.json", Root = "P_SilverCreatureGrowthTable")]
public record PSilverCreatureGrowthTable : TableRow
{
    public uint Id { get; init; }
    public uint ElementType { get; init; }
    public uint Cost { get; init; }
    public uint SummonedId { get; init; }
    public uint Level { get; init; }
    public uint WorldLevelCoef { get; init; }
    public List<uint> Skill { get; init; } = [];
}

/// <summary>The row ID becomes team_src. taskType is the quest namespace, not the wire team type.</summary>
[GameTable("P_TmpTeamTable.json", Root = "P_TmpTeamTable")]
public record PTmpTeamTable : TableRow
{
    public uint Id { get; init; }
    public List<uint> TmpCharacters { get; init; } = [];
    public uint TaskType { get; init; }
    public List<ulong> StepId { get; init; } = [];
    public uint TemporaryLiquidRatio { get; init; }
}
