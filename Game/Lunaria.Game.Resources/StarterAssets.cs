using System.Globalization;
using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

public sealed class StarterAssets
{
    private readonly uint[] _characters;
    private readonly ItemGrant[] _currency;
    private readonly GlobalConfigAssets _globalConfig;
    private readonly ItemGrant[] _items;
    private readonly MapAssets _maps;
    private readonly SPlayerIniTable _row;
    private readonly ulong? _storyMap;
    private readonly uint[] _team;

    public StarterAssets(
        IReadOnlyDictionary<string, SPlayerIniTable> players,
        MapAssets maps,
        ItemAssets items,
        CharacterAssets characters,
        GlobalConfigAssets globalConfig,
        TaskAssets tasks
    )
    {
        _row = players.Values.FirstOrDefault(r => r.Id == 1)
               ?? throw new ResourceException("S_PlayerIniTable.json", "s_playerinitable row 1 is missing");

        GameTime = ParseTod(_row.Tod);
        _maps = maps;
        _globalConfig = globalConfig;

        var grants = ParseItemColumn(_row.ItemId);
        _currency = grants.Where(g => items.IsCurrency(g.ItemId)).ToArray();
        _items = grants.Where(g => !items.IsCurrency(g.ItemId)).ToArray();

        if (_currency.Length == 0)
            throw new ResourceException("S_PlayerIniTable.json",
                "s_playerinitable grants no currency; p_itemtable lists none of its items as Use_Type 9");

        CoinMoneyType = items.MoneyTypeOf(_currency[0].ItemId)!.Value;

        _characters = _row.CharacterId
            .Where(id => characters.Exists(id) && Starter.ShippedCharacters.Contains(id))
            .Distinct()
            .ToArray();

        if (_characters.Length == 0)
            throw new ResourceException("S_PlayerIniTable.json",
                "s_playerinitable grants no servable characters; check Starter.ShippedCharacters against the dump");

        _team = _row.TeamInfo.Where(_characters.Contains).Distinct().ToArray();

        // Start in the prologue so quest 11000 sets AfterRain before the client places open-world NPCs.
        MapId = _row.DefaultMap;

        if (!maps.MapExists(MapId) || maps.SpawnPos(MapId) is not {} spawn)
            throw new ResourceException("P_MapDataTable.json",
                $"p_mapdatatable has no server-side row for the fresh spawn map {MapId}");

        SpawnPos = spawn;
        _storyMap = StorySpawnMap(maps, tasks);
    }

    public IReadOnlyList<ItemGrant> Items => _items;

    public IReadOnlyList<ItemGrant> CurrencyGrants => _currency;

    public ItemGrant Currency => _currency[0];

    public int CoinMoneyType { get; }

    public IReadOnlyList<uint> Characters => _characters;

    public IReadOnlyList<uint> Team => _team;

    public int Satiety => (int)_row.Satiety;

    public int Stamina => _globalConfig.StaminaRegenMax;

    /// <summary>Save_Point is an index. Resolve the default savepoint template to an NPC ID instead.</summary>
    public ulong Savepoint => _maps.DefaultSavepoint;

    public ulong MapId { get; }

    public ulong FallbackMap => _storyMap ?? _row.DefaultMap;

    public (int X, int Y, int Z) SpawnPos { get; }

    public uint Weather => _row.Weather;

    public uint GameTime { get; }

    private static uint ParseTod(string raw)
    {
        if (!TimeOnly.TryParseExact(raw, ["H:mm", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            throw new ResourceException("S_PlayerIniTable.json", $"row 1 has a malformed Tod '{raw}'");
        return (uint)(time.Hour * 60 + time.Minute);
    }

    private static ulong? StorySpawnMap(MapAssets maps, TaskAssets tasks)
    {
        var step = tasks.FirstStep(TaskAssets.QuestMain, TaskAssets.OpeningTaskId);

        if (step == 0)
            return null;

        foreach (var action in tasks.Actions(TaskAssets.QuestMain, step))
        {
            var row = tasks.Action(TaskAssets.QuestMain, action);

            if (row is { MapId: > 0 } && maps.SoleMapOfWorld(row.MapId) is {} map)
                return map;
        }

        return null;
    }

    private static ItemGrant[] ParseItemColumn(string raw) =>
        raw.Split(separator: '|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(seg => {
                var parts = seg.Split(separator: '#', count: 2);
                var id = uint.Parse(parts[0].Trim());
                var count = parts.Length > 1 && uint.TryParse(parts[1].Trim(), out var c) ? c : 1u;
                return new ItemGrant(id, count);
            })
            .ToArray();
}
