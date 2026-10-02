using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

/// <summary>Drop_ID selects item rewards. CollectionDropId selects a weighted node spawn.</summary>
public sealed class CollectionAssets
{
    private readonly Dictionary<uint, PCollectionTable> _collections = [];
    private readonly DropTableAssets _drops;
    private readonly LimitAssets _limits;
    private readonly Dictionary<(uint DropId, uint GroupId), (uint CollectionId, uint Weight)[]> _spawnGroups = [];

    public CollectionAssets(
        IReadOnlyDictionary<string, PCollectionTable> collections,
        IReadOnlyDictionary<string, PCollectionDropTable> spawns,
        DropTableAssets drops,
        LimitAssets limits
    )
    {
        _drops = drops;
        _limits = limits;

        foreach (var row in collections.Values)
        {
            _collections[row.Id] = row;
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

        GatherableIds = _collections.Values
            .Where(r => r.DropId.Count > 0)
            .Select(r => r.Id)
            .OrderBy(id => id)
            .ToList();
    }

    public IReadOnlyList<uint> GatherableIds { get; }

    public int Count => _collections.Count;

    public bool Exists(uint collectionId) => _collections.ContainsKey(collectionId);

    public PCollectionTable? Get(uint collectionId) => _collections.GetValueOrDefault(collectionId);

    public bool CanResolveRewards(uint collectionId) =>
        _collections.GetValueOrDefault(collectionId) is {} row && row.DropId.All(_drops.Exists);

    public IReadOnlyList<ItemGrant> Rewards(uint collectionId, Random random) =>
        _collections.GetValueOrDefault(collectionId) is {} row
            ? row.DropId.SelectMany(id => _drops.Roll(id, random)).ToArray() : [];

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
