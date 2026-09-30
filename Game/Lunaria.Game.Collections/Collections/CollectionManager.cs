using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Collections;

/// <summary>Tables omit spawn positions. Save fixed offsets around the first request position.</summary>
public sealed partial class CollectionManager(GameData assets)
{
    public const int MaxNodes = 4096;
    private readonly SortedSet<uint> _gathered = [];

    private readonly SortedDictionary<ulong, CollectionState> _nodes = [];

    public IReadOnlySet<uint> Gathered => _gathered;

    public bool IsDirty { get; private set; }

    public IReadOnlyDictionary<ulong, CollectionState> Entries => _nodes;

    public ulong MaxUniq => _nodes.Count == 0 ? 0 : _nodes.Keys.Max();

    public int Count => _nodes.Count;

    public bool IsEmpty => _nodes.Count == 0;

    public void LoadGathered(IEnumerable<uint> gathered)
    {
        _gathered.Clear();
        _gathered.UnionWith(gathered.Where(assets.Collections.Exists));
        _gathered.UnionWith(_nodes.Values.Where(node => node.Status == EnmCollectionStatus.EcsCollected).Select(node => node.Cfg));
    }

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
            if (row.Uniq == 0 || !assets.Collections.Exists(row.Cfg))
                continue;

            if (!Enum.IsDefined(typeof(EnmCollectionStatus), row.Status))
                continue;

            _nodes[row.Uniq] = new CollectionState(
                row.Uniq,
                row.Cfg,
                (EnmCollectionStatus)row.Status,
                row.StatusTime,
                row.Block,
                row.Position.X,
                row.Position.Y,
                row.Position.Z);
        }

        IsDirty = false;
    }

    public void ClearDirty() => IsDirty = false;

    public CollectionState? Get(ulong uniq) => _nodes.GetValueOrDefault(uniq);

    public int EnsureBlock(ulong blockId, DateTimeOffset now, (int X, int Y, int Z) at, Func<ulong> mint)
    {
        if (_nodes.Count >= MaxNodes)
            return 0;

        var known = new HashSet<uint>();

        foreach (var node in _nodes.Values)
        {
            if (node.Block == blockId)
                known.Add(node.Cfg);
        }

        var planted = 0;

        foreach (var cfg in assets.Collections.GatherableIds)
        {
            if (_nodes.Count >= MaxNodes)
                break;

            if (!known.Add(cfg))
                continue;

            var uniq = mint();

            if (uniq == 0 || _nodes.ContainsKey(uniq))
            {
                known.Remove(cfg);
                continue;
            }

            var (x, y, z) = SpreadAround(at, cfg);

            _nodes[uniq] = new CollectionState(
                uniq, cfg, EnmCollectionStatus.EcsCanCollect, now, blockId, x, y, z);
            planted++;
        }

        if (planted > 0)
            Dirty();
        return planted;
    }

    public IReadOnlyList<CollectionState> ListBlock(ulong blockId, DateTimeOffset now)
    {
        RefreshBlock(blockId, now);
        return _nodes.Values.Where(node => node.Block == blockId).ToList();
    }

    public int RefreshBlock(ulong blockId, DateTimeOffset now)
    {
        var revived = 0;

        foreach (var node in _nodes.Values.Where(node => node.Block == blockId).ToList())
        {
            if (TryRefreshOne(node.Uniq, now))
                revived++;
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

    public static OneCollectionData ToOneCollectionData(CollectionState node) => new() {
        UniqId = node.Uniq,
        CfgId = node.Cfg,
        Status = node.Status,
        StatusTime = ToStatusTime(node.StatusTime),
        BlockId = node.Block,
        Location = new Vector3Int { X = node.X, Y = node.Y, Z = node.Z },
        Rotation = new Rotator(),
        FromType = EnmCollectionFromType.EcollectFromTable,
        FromId = node.Cfg,
        FromLocation = new Vector3Int { X = node.X, Y = node.Y, Z = node.Z }
    };

    private static (int X, int Y, int Z) SpreadAround((int X, int Y, int Z) at, uint cfg)
    {
        var dx = ((long)(cfg % 11) - 5) * 120;
        var dz = ((long)(cfg / 11 % 11) - 5) * 120;

        return (
            (int)Math.Clamp(at.X + dx, int.MinValue, int.MaxValue),
            at.Y,
            (int)Math.Clamp(at.Z + dz, int.MinValue, int.MaxValue));
    }

    private static uint ToStatusTime(DateTimeOffset moment)
    {
        var unix = moment.ToUnixTimeSeconds();
        return unix < 0 ? 0 : unix > uint.MaxValue ? uint.MaxValue : (uint)unix;
    }

    private void Dirty() => IsDirty = true;
}
