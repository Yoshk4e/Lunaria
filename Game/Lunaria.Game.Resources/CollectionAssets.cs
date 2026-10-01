using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

/// <summary>
/// Drop_ID selects item rewards. A chest's CollectionDropId names weighted groups; each group draws one collection
/// whose Drop_ID is granted. World objects are the placed instances the client binds to by id.
/// </summary>
public sealed class CollectionAssets
{
    private readonly Dictionary<uint, PCollectionTable> _collections = [];
    private readonly DropAssets _drops;
    private readonly LimitAssets _limits;
    private readonly Dictionary<(uint DropId, uint GroupId), (uint CollectionId, uint Weight)[]> _spawnGroups = [];
    private readonly Dictionary<ulong, PWorldCollectObjTable> _worldObjects = [];
    private readonly Dictionary<ulong, PWorldCollectObjTable[]> _worldObjectsByBlock = [];

    public CollectionAssets(
        IReadOnlyDictionary<string, PCollectionTable> collections,
        IReadOnlyDictionary<string, PCollectionDropTable> spawns,
        IReadOnlyDictionary<string, PWorldCollectObjTable> worldObjects,
        DropAssets drops,
        LimitAssets limits
    )
    {
        _drops = drops;
        _limits = limits;

        foreach (var row in collections.Values)
        {
            _collections[row.Id] = row;
        }

        // Skip placements whose template is unknown: the client could not collect them either.
        foreach (var row in worldObjects.Values.Where(r => r.Id != 0 && _collections.ContainsKey(r.TemplateId)))
        {
            _worldObjects[row.Id] = row;
        }

        foreach (var block in _worldObjects.Values.GroupBy(r => r.BlockId))
        {
            _worldObjectsByBlock[block.Key] = block.OrderBy(r => r.Id).ToArray();
        }

        foreach (var group in spawns.Values.GroupBy(r => (r.CollectionDropId, r.GroupId)))
        {
            _spawnGroups[group.Key] = group
                .Where(r => r.CollectionId != 0 && r.Weight > 0)
                .OrderBy(r => r.Id)
                .Select(r => (r.CollectionId, r.Weight))
                .ToArray();
        }

        if (_collections.Count == 0)
            throw new ResourceException("P_CollectionTable.json", "p_collectiontable has no rows");
    }

    public int Count => _collections.Count;

    public bool Exists(uint collectionId) => _collections.ContainsKey(collectionId);

    public PCollectionTable? Get(uint collectionId) => _collections.GetValueOrDefault(collectionId);

    public IReadOnlyList<ItemGrant> Rewards(uint collectionId) =>
        _collections.GetValueOrDefault(collectionId) is {} row ? _drops.Bundles(row.DropId) : [];

    /// <summary>Rewards of one opening: the collection's own Drop_ID, then one weighted draw per chest group.</summary>
    public IReadOnlyList<ItemGrant> Rewards(uint collectionId, Random rng)
    {
        if (_collections.GetValueOrDefault(collectionId) is not {} row)
            return [];

        var grants = _drops.Bundles(row.DropId).ToList();

        if (row.CollectionDropId == 0)
            return grants;

        foreach (var group in _spawnGroups.Where(g => g.Key.DropId == row.CollectionDropId).OrderBy(g => g.Key.GroupId))
        {
            if (Draw(group.Value, rng) is {} drawn && drawn != collectionId)
                grants.AddRange(Rewards(drawn));
        }

        return grants;
    }

    private static uint? Draw((uint CollectionId, uint Weight)[] pool, Random rng)
    {
        var total = pool.Sum(entry => (long)entry.Weight);

        if (total <= 0)
            return null;

        var roll = rng.NextInt64(total);

        foreach (var (id, weight) in pool)
        {
            if (roll < weight)
                return id;

            roll -= weight;
        }

        return null;
    }

    public int WorldObjectCount => _worldObjects.Count;

    public PWorldCollectObjTable? WorldObject(ulong id) => _worldObjects.GetValueOrDefault(id);

    /// <summary>Placed objects of one block; block 0 lists every block.</summary>
    public IReadOnlyList<PWorldCollectObjTable> WorldObjects(ulong blockId) => blockId == 0
        ? _worldObjects.Values.OrderBy(r => r.Id).ToArray()
        : _worldObjectsByBlock.GetValueOrDefault(blockId) ?? [];

    public uint RewardLimitGroup(uint collectionId) =>
        _collections.GetValueOrDefault(collectionId)?.RewardLimitId ?? 0;

    public RefreshPeriod Respawn(uint collectionId) =>
        _collections.GetValueOrDefault(collectionId) is {} row ? _limits.PeriodById(row.RefreshConfigId) : RefreshPeriod.None;

    public bool AutoDestroys(uint collectionId) =>
        _collections.GetValueOrDefault(collectionId)?.AutoDestroy ?? false;

    /// <summary>Interaction radius in centimetres. Uses UnlockCollectionRange when unset.</summary>
    public int Radius(uint collectionId, int fallback) =>
        _collections.GetValueOrDefault(collectionId) is { Radius: > 0 } row ? (int)row.Radius : fallback;

    public IReadOnlyList<(uint CollectionId, uint Weight)> SpawnGroup(uint collectionDropId, uint groupId) =>
        _spawnGroups.GetValueOrDefault((collectionDropId, groupId)) ?? [];
}
