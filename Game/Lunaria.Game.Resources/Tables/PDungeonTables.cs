namespace Lunaria.Game.Resources.Tables;

[GameTable("P_RepeatableDungeonsTable.json", Root = "P_RepeatableDungeonsTable")]
public record PRepeatableDungeonsTable : TableRow
{
    public ulong Id { get; init; }
    public uint DungeonType { get; init; }
    public List<uint> BattleId { get; init; } = [];
    public ulong MapId { get; init; }
    /// <summary>Seconds before the dungeon times out.</summary>
    public uint DungeonTime { get; init; }
    public uint MonsterLevel { get; init; }
    /// <summary>Victory reward group in s_droptable, or 0 for no reward.</summary>
    public uint RewardDrop { get; init; }
    /// <summary>Reward group in s_droptable for the first victory only (Abyss stages), or 0.</summary>
    public uint FirstPassRewardDrop { get; init; }
}

[GameTable("P_DungeonsTypeTable.json", Root = "P_DungeonsTypeTable")]
public record PDungeonsTypeTable : TableRow
{
    public uint Id { get; init; }
    /// <summary>DungeonLimitType: 1 none, 2 weekly, 3 daily.</summary>
    public uint LimitType { get; init; }
    /// <summary>Attempts per reset period, or 0 for unlimited attempts.</summary>
    public uint LimitParam { get; init; }
    public uint VitalityCost { get; init; }
    public bool Exit { get; init; }
    /// <summary>Entry HP in basis points. 10000 keeps the current HP.</summary>
    public uint CharacterHpRatio { get; init; }
    public uint CharacterPermanentLiquidRatio { get; init; }
    public uint TemporaryLiquidRatio { get; init; }
    /// <summary>Maximum victory time in minutes.</summary>
    public uint MinPassTime { get; init; }
}

/// <summary>Horde IDs match dungeon IDs. Each star has separate first-clear and repeat rewards.</summary>
[GameTable("P_HordeTable.json", Root = "P_HordeTable")]
public record PHordeTable : TableRow
{
    public uint Id { get; init; }
    public List<uint> KillConditon { get; init; } = [];
    public List<uint> FirstDrop { get; init; } = [];
    public List<uint> CommonDrop { get; init; } = [];
}

[GameTable("S_DropTable.json", Root = "S_DropTable")]
public record SDropTable : TableRow
{
    public uint Id { get; init; }
    public uint DropId { get; init; }
    public uint GroupId { get; init; }
    public uint ItemId { get; init; }
    public uint ItemCount { get; init; }
    /// <summary>Chance in basis points. 10000 guarantees a drop and null marks a weighted row.</summary>
    public uint? Odds { get; init; }
    /// <summary>Pick weight within the group. Null on fixed rows.</summary>
    public uint? Weight { get; init; }
}
