namespace Lunaria.Game.Resources.Tables;

[GameTable("P_WantedPosterTable.json", Root = "P_WantedPosterTable")]
public record PWantedPosterTable : TableRow
{
    public uint Id { get; init; }
    public ulong MapId { get; init; }
    public uint OptionalAwardCost { get; init; }
}

[GameTable("P_WantedPosterEntryTable.json", Root = "P_WantedPosterEntryTable")]
public record PWantedPosterEntryTable : TableRow
{
    public uint Id { get; init; }
    public uint WantedPosterId { get; init; }
    public uint Difficulty { get; init; }
    public uint FristRouteId { get; init; }
    public uint NormalRouteId { get; init; }
    /// <summary>Required finish-event ID, or 0 for an unlocked difficulty.</summary>
    public uint UnlockCondition { get; init; }
    public uint FirstAward { get; init; }
    public uint OptionalAward { get; init; }
    public uint ExpAwardDisplay { get; init; }
    public uint RecommendLevel { get; init; }
}

/// <summary>Pool identifies an event pool. Award contains reward definition IDs, not NPC IDs.</summary>
[GameTable("P_WantedPosterProcessTable.json", Root = "P_WantedPosterProcessTable")]
public record PWantedPosterProcessTable : TableRow
{
    public uint Id { get; init; }
    public uint RouteId { get; init; }
    public uint StepCount { get; init; }
    public uint Pool { get; init; }
    public uint WantedEventType { get; init; }
    public uint AwardType { get; init; }
    public List<uint> Award { get; init; } = [];
}

[GameTable("P_WantedPosterEventTable.json", Root = "P_WantedPosterEventTable")]
public record PWantedPosterEventTable : TableRow
{
    public uint Id { get; init; }
    public List<uint> Group { get; init; } = [];
    public uint WantedEventType { get; init; }
    public uint TaskStepOfEventChoose { get; init; }
    public uint TaskStepOfEventFinish { get; init; }
    public List<uint> EventStartAddTask { get; init; } = [];
    public List<uint> EventFinishAddTask { get; init; } = [];
    public List<uint> EventFinishRemoveTask { get; init; } = [];
}

[GameTable("P_WantedPosterStepCountTable.json", Root = "P_WantedPosterStepCountTable")]
public record PWantedPosterStepCountTable : TableRow
{
    public uint Id { get; init; }
    public uint EntryId { get; init; }
    public uint StepCount { get; init; }
    public uint Award { get; init; }
    public uint MonsterLevel { get; init; }
}

[GameTable("P_WantedPosterNPC.json", Root = "P_WantedPosterNPC")]
public record PWantedPosterNPC : TableRow
{
    public uint Id { get; init; }
    public uint NpcType { get; init; }
    /// <summary>Shop, adventure, or battlefield ID, depending on the NPC type.</summary>
    public uint Params { get; init; }
}

[GameTable("P_WantedPosterBlessTable.json", Root = "P_WantedPosterBlessTable")]
public record PWantedPosterBlessTable : TableRow
{
    public uint Id { get; init; }
    public uint UnlockCondition { get; init; }
    /// <summary>1 blue, 2 purple, 3 gold, 4 special.</summary>
    public uint BlessType { get; init; }
    /// <summary>Bond requirements as "bondId=count" strings.</summary>
    public List<string> Bond { get; init; } = [];
    public List<uint> BattleEffect { get; init; } = [];
}

[GameTable("P_WantedPosterBlessBondTable.json", Root = "P_WantedPosterBlessBondTable")]
public record PWantedPosterBlessBondTable : TableRow
{
    public uint Id { get; init; }
    /// <summary>1 main, 2 deputy.</summary>
    public uint BondType { get; init; }
    public List<uint> Bond { get; init; } = [];
    /// <summary>Battle effects by level, in the same order as Bond.</summary>
    public List<string> BattleEffect { get; init; } = [];
}

[GameTable("P_WantedPosterRelicTable.json", Root = "P_WantedPosterRelicTable")]
public record PWantedPosterRelicTable : TableRow
{
    public uint Id { get; init; }
    // Spelling matches the binary schema.
    public uint UnlockContion { get; init; }
    public uint Quality { get; init; }
    public List<uint> RelicEffect { get; init; } = [];
}

[GameTable("P_WantedPosterCreatureTable.json", Root = "P_WantedPosterCreatureTable")]
public record PWantedPosterCreatureTable : TableRow
{
    public uint Id { get; init; }
    public uint SilverCreatureId { get; init; }
    public uint Quality { get; init; }
    /// <summary>The schema defines this as the sale or replacement refund, not the purchase price.</summary>
    public uint Price { get; init; }
    public uint UnlockCondition { get; init; }
    public uint LevelUpId { get; init; }
    public List<uint> AffixIdList { get; init; } = [];
    public List<string> CreatureWeightAmend { get; init; } = [];
}

[GameTable("P_WantedPosterRevive.json", Root = "P_WantedPosterRevive")]
public record PWantedPosterRevive : TableRow
{
    public uint Id { get; init; }
    public uint Type { get; init; }
}

/// <summary>Type selects the shop table. Param contains groupId=count entries.</summary>
[GameTable("P_WantedPosterShop.json", Root = "P_WantedPosterShop")]
public record PWantedPosterShop : TableRow
{
    public uint Id { get; init; }
    public uint Type { get; init; }
    public List<string> Param { get; init; } = [];
}

[GameTable("P_WPBlessShop.json", Root = "P_WPBlessShop")]
public record PWPBlessShop : TableRow
{
    public uint Id { get; init; }
    public uint Group { get; init; }
    public uint BlessId { get; init; }
    public uint LimitNum { get; init; }
    public uint CostNum { get; init; }
}

[GameTable("P_WPRelicShop.json", Root = "P_WPRelicShop")]
public record PWPRelicShop : TableRow
{
    public uint Id { get; init; }
    public uint Group { get; init; }
    public uint RelicId { get; init; }
    public uint LimitNum { get; init; }
    public uint CostNum { get; init; }
}

[GameTable("P_WPCreatureShop.json", Root = "P_WPCreatureShop")]
public record PWPCreatureShop : TableRow
{
    public uint Id { get; init; }
    public uint Group { get; init; }
    public uint CreatureEntryId { get; init; }
    public uint LimitNum { get; init; }
    public uint CostNum { get; init; }
}

[GameTable("P_WPAdventureTable.json", Root = "P_WPAdventureTable")]
public record PWPAdventureTable : TableRow
{
    public uint Id { get; init; }
    public uint ContentHeadId { get; init; }
}

[GameTable("P_WPAdvContentTable.json", Root = "P_WPAdvContentTable")]
public record PWPAdvContentTable : TableRow
{
    public uint Id { get; init; }
    public uint AdventureId { get; init; }
    /// <summary>1 narration, 2 choice.</summary>
    public uint Type { get; init; }
    public List<uint> DialogId { get; init; } = [];
    public List<uint> NextDialogId { get; init; } = [];
}

[GameTable("P_WPAdvDialogTable.json", Root = "P_WPAdvDialogTable")]
public record PWPAdvDialogTable : TableRow
{
    public uint Id { get; init; }
    public uint NextId { get; init; }
    public uint SpeakerType { get; init; }
}

[GameTable("P_WPAdvOptionTable.json", Root = "P_WPAdvOptionTable")]
public record PWPAdvOptionTable : TableRow
{
    public uint Id { get; init; }
    public uint SuccessNextContentId { get; init; }
}

[GameTable("P_WantedPosterConfig.json", Root = "P_WantedPosterConfig")]
public record PWantedPosterConfig : TableRow
{
    public uint Id { get; init; }
    public string Key { get; init; } = "";
    public string Value { get; init; } = "";
}

[GameTable("P_WantedPosterEffect.json", Root = "P_WantedPosterEffect")]
public record PWantedPosterEffect : TableRow
{
    public uint Id { get; init; }
    public uint EffectType { get; init; }
    /// <summary>Attribute modifier as "attrib=value".</summary>
    public string EffectParam { get; init; } = "";
}
