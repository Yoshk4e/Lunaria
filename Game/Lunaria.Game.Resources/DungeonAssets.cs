using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

public sealed class DungeonAssets
{
    public const uint LimitNone = 1;

    public const uint LimitWeek = 2;

    public const uint LimitDay = 3;

    public const uint ZombieWaveType = 5;

    private readonly Dictionary<ulong, PRepeatableDungeonsTable> _dungeons = [];
    private readonly Dictionary<uint, PHordeTable> _hordes = [];
    private readonly Dictionary<uint, PDungeonsTypeTable> _types = [];
    private readonly Dictionary<uint, uint> _battleFields = [];

    public DungeonAssets(
        IReadOnlyDictionary<string, PRepeatableDungeonsTable> dungeons,
        IReadOnlyDictionary<string, PDungeonsTypeTable> types,
        IReadOnlyDictionary<string, PHordeTable> hordes,
        IReadOnlyDictionary<string, CRepeatableDungeonsBattleTable> battles,
        DropTableAssets drops
    )
    {
        foreach (var row in dungeons.Values)
        {
            _dungeons[row.Id] = row;
        }

        foreach (var row in types.Values)
        {
            _types[row.Id] = row;
        }

        foreach (var row in hordes.Values)
        {
            _hordes[row.Id] = row;
        }

        foreach (var row in battles.Values)
        {
            _battleFields[row.Id] = row.BattleFieldId;
        }

        if (_dungeons.Count == 0)
            throw new ResourceException("P_RepeatableDungeonsTable.json", "p_repeatabledungeonstable has no rows");

        if (_types.Count == 0)
            throw new ResourceException("P_DungeonsTypeTable.json", "p_dungeonstypestable has no rows");

        foreach (var dungeon in _dungeons.Values)
        {
            if (!_types.ContainsKey(dungeon.DungeonType))
                throw new ResourceException(
                    "P_DungeonsTypeTable.json", $"dungeon {dungeon.Id} references missing type {dungeon.DungeonType}");

            if (dungeon.RewardDrop != 0 && !drops.Exists(dungeon.RewardDrop))
                continue;
        }
    }

    public IReadOnlyList<PRepeatableDungeonsTable> Dungeons =>
        _dungeons.Values.OrderBy(row => row.Id).ToList();

    public IReadOnlyList<PDungeonsTypeTable> Types =>
        _types.Values.OrderBy(row => row.Id).ToList();

    public PRepeatableDungeonsTable? Dungeon(ulong id) => _dungeons.GetValueOrDefault(id);

    public bool IsDungeonMap(ulong mapId) => _dungeons.Values.Any(row => row.MapId == mapId);
    public PDungeonsTypeTable? Type(uint id) => _types.GetValueOrDefault(id);
    public PHordeTable? Horde(uint id) => _hordes.GetValueOrDefault(id);

    /// <summary>
    /// Whether <paramref name="battleFieldId"/> is one of the dungeon's battles, by battle row or by the
    /// BattleFieldID the client fights on (s_CSM_D_DungeonProcessState_Fighting.DungeonStartBattleReq).
    /// </summary>
    public bool FightsOn(ulong dungeonId, uint battleFieldId) =>
        Dungeon(dungeonId)?.BattleId.Any(battle =>
            battle == battleFieldId || _battleFields.TryGetValue(battle, out var field) && field == battleFieldId) == true;
}
