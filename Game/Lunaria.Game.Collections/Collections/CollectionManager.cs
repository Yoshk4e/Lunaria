using Lunaria.Game.Resources;
using Lunaria.Game.Resources.Tables;
using Msg;

namespace Lunaria.Game.Collections;

/// <summary>
/// World objects come from p_worldcollectobjtable; their uniq id is the table row id, which the client binds to the
/// level placement. Only objects the player has touched are stored; every other placed object can be collected.
/// </summary>
public sealed partial class CollectionManager(GameData assets)
{
    public const int MaxNodes = 4096;
    private readonly SortedSet<uint> _gathered = [];

    private readonly SortedDictionary<ulong, CollectionState> _nodes = [];

    public IReadOnlySet<uint> Gathered => _gathered;

    public bool IsDirty { get; private set; }

    public IReadOnlyDictionary<ulong, CollectionState> Entries => _nodes;

    public int Count => _nodes.Count;

    public bool IsEmpty => _nodes.Count == 0;

    public void LoadGathered(IEnumerable<uint> gathered)
    {
        _gathered.Clear();
        _gathered.UnionWith(gathered.Where(assets.Collections.Exists));
        _gathered.UnionWith(_nodes.Values.Where(node => node.Status == EnmCollectionStatus.EcsCollected).Select(node => node.Cfg));
    }

    /// <summary>
    /// Rows that do not match a placed object of the same template are dropped. Older saves planted invented nodes
    /// around the player; those never matched the level, so they are discarded here.
    /// </summary>
    public void Load(
        IEnumerable<(ulong Uniq, uint Cfg, int Status, DateTimeOffset StatusTime, ulong Block, (int X, int Y, int Z) Position)> persisted
    )
    {
        _nodes.Clear();
        _gathered.Clear();

        foreach (var row in persisted
                     .DistinctBy(r => r.Uniq)
                     .OrderBy(r => r.Uniq)
                     .Take(MaxNodes))
        {
            if (assets.Collections.WorldObject(row.Uniq) is not {} placed || placed.TemplateId != row.Cfg)
                continue;

            if (!Enum.IsDefined(typeof(EnmCollectionStatus), row.Status))
                continue;

            _nodes[row.Uniq] = FromPlacement(placed, (EnmCollectionStatus)row.Status, row.StatusTime);
        }

        IsDirty = false;
    }

    public void ClearDirty() => IsDirty = false;

    /// <summary>Current state of a placed object, stored or not; null when the id is not a placed object.</summary>
    public CollectionState? Get(ulong uniq)
    {
        if (_nodes.TryGetValue(uniq, out var node))
            return node;

        return assets.Collections.WorldObject(uniq) is {} placed
            ? FromPlacement(placed, EnmCollectionStatus.EcsCanCollect, DateTimeOffset.UnixEpoch)
            : null;
    }

    public IReadOnlyList<CollectionState> ListBlock(ulong blockId, DateTimeOffset now)
    {
        var placed = assets.Collections.WorldObjects(blockId);
        var list = new List<CollectionState>(placed.Count);

        foreach (var row in placed)
        {
            TryRefreshOne(row.Id, now);
            list.Add(Get(row.Id)!);
        }

        return list;
    }

    /// <summary>Stored objects whose respawn time has passed, switched back to collectable.</summary>
    public IReadOnlyList<CollectionState> RefreshDue(DateTimeOffset now)
    {
        var revived = new List<CollectionState>();

        foreach (var uniq in _nodes.Keys.ToList())
        {
            if (TryRefreshOne(uniq, now))
                revived.Add(_nodes[uniq]);
        }

        return revived;
    }

    public bool TryRefreshOne(ulong uniq, DateTimeOffset now)
    {
        if (!_nodes.TryGetValue(uniq, out var node))
            return false;

        if (node.Status is not (EnmCollectionStatus.EcsCollected or EnmCollectionStatus.EcsDestroyed))
            return false;

        var period = assets.Collections.Respawn(node.Cfg);

        if (!period.ResetsEver || !period.HasReset(node.StatusTime, now))
            return false;

        _nodes[uniq] = node with { Status = EnmCollectionStatus.EcsCanCollect, StatusTime = now };
        Dirty();
        return true;
    }

    public OneCollectionData ToOneCollectionData(CollectionState node)
    {
        var placed = assets.Collections.WorldObject(node.Uniq);

        return new OneCollectionData {
            UniqId = node.Uniq,
            CfgId = node.Cfg,
            Status = node.Status,
            StatusTime = ToStatusTime(node.StatusTime),
            BlockId = node.Block,
            Location = new Vector3Int { X = node.X, Y = node.Y, Z = node.Z },
            Rotation = placed is null ? new Rotator() : new Rotator {
                Yaw = (int)MathF.Round(placed.Direction),
                Pitch = (int)MathF.Round(placed.Pitch),
                Roll = (int)MathF.Round(placed.Roll)
            },
            FromType = EnmCollectionFromType.EcollectFromTable,
            // The client files table objects under StaticServerLocateItemMap[from_id] and matches the level placement.
            FromId = node.Uniq,
            FromLocation = new Vector3Int { X = node.X, Y = node.Y, Z = node.Z }
        };
    }

    private static CollectionState FromPlacement(PWorldCollectObjTable placed, EnmCollectionStatus status, DateTimeOffset time) =>
        new(placed.Id, placed.TemplateId, status, time, placed.BlockId,
            (int)MathF.Round(placed.PosX), (int)MathF.Round(placed.PosY), (int)MathF.Round(placed.PosZ));

    private static uint ToStatusTime(DateTimeOffset moment)
    {
        var unix = moment.ToUnixTimeSeconds();
        return unix < 0 ? 0 : unix > uint.MaxValue ? uint.MaxValue : (uint)unix;
    }

    private void Dirty() => IsDirty = true;
}
