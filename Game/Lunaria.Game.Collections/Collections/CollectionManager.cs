using Lunaria.Common.Tracking;
using Lunaria.Game.Logging;
using Lunaria.Game.Resources.Tables;
using Lunaria.Game.Resources;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.Game.Collections;

/// <summary>
/// The client uses row IDs from p_worldcollectobjtable to find objects in the level.
/// Save only objects the player has touched. Other objects use their default state.
/// </summary>
public sealed partial class CollectionManager(GameData assets) : TrackedObject
{
    private static readonly ILogger Log = GameLog.Create("Lunaria.Game.Collections");

    public const int MaxNodes = 4096;
    private readonly TrackedSet<uint> __tracked_gathered = [];
    [Tracked]
    private partial TrackedSet<uint> _gathered { get; }

    private readonly TrackedSortedDictionary<ulong, CollectionState> __tracked_nodes = [];
    [Tracked]
    private partial TrackedSortedDictionary<ulong, CollectionState> _nodes { get; }

    public IReadOnlySet<uint> Gathered => _gathered;

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
    /// Ignore nodes from older saves that do not match a placed object and its template.
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

        AcceptLoadedState();
    }

    /// <summary>Current state of a placed object, or null if the id has no placement.</summary>
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

        _nodes[uniq] = FromPlacement(assets.Collections.WorldObject(uniq)!, EnmCollectionStatus.EcsCanCollect, now);

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

    private static CollectionState FromPlacement(PWorldCollectObjTable placed, EnmCollectionStatus status, DateTimeOffset time)
    {
        // The table has no unlock parameters, so locked placements must stay locked.
        if (status == EnmCollectionStatus.EcsCanCollect && placed.CollectUnlockType != 0)
            status = EnmCollectionStatus.EcsLock;
        return new(placed.Id, placed.TemplateId, status, time, placed.BlockId,
            (int)MathF.Round(placed.PosX), (int)MathF.Round(placed.PosY), (int)MathF.Round(placed.PosZ));
    }

    private static uint ToStatusTime(DateTimeOffset moment)
    {
        var unix = moment.ToUnixTimeSeconds();
        return unix < 0 ? 0 : unix > uint.MaxValue ? uint.MaxValue : (uint)unix;
    }

}
