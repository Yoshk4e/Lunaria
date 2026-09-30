using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.World;

public sealed partial class MapManager
{
    /// <summary>proto <c>MAX_SAVEPOINTS</c>.</summary>
    public const int MaxSavepoints = (int)EnmSizeLimit.MaxSavepoints;

    private readonly GameData _assets;
    private readonly List<ulong> _unlockedSavepoints;
    private readonly List<ulong> _unlockedTeleports = [];

    private (int X, int Y, int Z) _position;

    private bool _positionIsSynced;

    public MapManager(GameData assets)
    {
        _assets = assets;
        SpawnMap = assets.Starter.MapId;
        Savepoint = assets.Starter.Savepoint;
        _unlockedSavepoints = [assets.Starter.Savepoint];
        _position = assets.Starter.SpawnPos;
    }

    public bool IsDirty { get; private set; }

    public MapPhase Phase { get; private set; } = MapPhase.Idle;

    public ulong SpawnMap { get; private set; }

    public ulong MapId => SpawnMap;
    public ulong TeleportId { get; private set; }

    public ulong Savepoint { get; private set; }

    public IReadOnlyList<ulong> UnlockedSavepoints => _unlockedSavepoints;
    public IReadOnlyList<ulong> UnlockedTeleports => _unlockedTeleports;
    public (int X, int Y, int Z) Position => _position;
    public MapReturnPoint? ReturnPoint { get; private set; }

    public void Load(
        ulong mapId,
        ulong savepoint,
        IEnumerable<ulong> unlockedSavepoints,
        IEnumerable<ulong> unlockedTeleports,
        (int X, int Y, int Z) position,
        IEnumerable<(ulong MapId, ulong TagId, uint TagType)>? trackedTargets = null,
        MapReturnPoint? returnPoint = null
    )
    {
        SpawnMap = _assets.Maps.MapExists(mapId) ? mapId : _assets.Starter.FallbackMap;

        _unlockedSavepoints.Clear();

        _unlockedSavepoints.AddRange(unlockedSavepoints
            .Where(_assets.Maps.SavepointExists)
            .Distinct()
            .Take(MaxSavepoints));

        if (_unlockedSavepoints.Count == 0)
            _unlockedSavepoints.Add(_assets.Starter.Savepoint);

        Savepoint = _unlockedSavepoints.Contains(savepoint) ? savepoint : _unlockedSavepoints[0];

        _unlockedTeleports.Clear();
        _unlockedTeleports.AddRange(unlockedTeleports.Where(_assets.Maps.TeleportExists).Distinct());

        _position = SpawnMap == mapId ? position : _assets.Maps.SpawnPos(SpawnMap) ?? _assets.Starter.SpawnPos;
        _positionIsSynced = SpawnMap == mapId;
        ReturnPoint = _assets.Maps.IsScriptedWorld(SpawnMap)
                      && returnPoint is not null && _assets.Maps.MapExists(returnPoint.MapId)
                      && !_assets.Maps.IsScriptedWorld(returnPoint.MapId) ? returnPoint : null;

        _trackedTargets.Clear();

        if (trackedTargets is not null)
            foreach (var (pinMap, tagId, tagType) in trackedTargets)
            {
                if (_assets.Maps.MapExists(pinMap) && !IsTracked(pinMap, tagId, tagType))
                    _trackedTargets.Add(new TrackedTargetInfo {
                        MapId = pinMap,
                        TagId = tagId,
                        TagType = tagType
                    });
            }

        TeleportId = 0;
        Phase = MapPhase.Idle;
        IsDirty = false;
    }

    public void ClearDirty() => IsDirty = false;
}
