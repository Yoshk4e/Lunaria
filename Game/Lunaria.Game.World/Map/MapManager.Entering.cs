using Lunaria.Common.Tracking;
using Lunaria.Game.Logging;
using Msg;

namespace Lunaria.Game.World;

public sealed partial class MapManager : TrackedObject
{
    /// <summary>
    /// Returns use saved coordinates and teleports use their selected point. New arrivals use savepoints to avoid
    /// unloaded sublevels.
    /// </summary>
    public EnmBornPosType BornPosType
    {
        get
        {
            if (TeleportId != 0)
                return EnmBornPosType.EnmBornTeleport;

            return _positionIsSynced ? EnmBornPosType.EnmBornPosition : EnmBornPosType.EnmBornSavePoint;
        }
    }

    /// <summary>
    /// Map ID 0 keeps the current map. Otherwise use the saved story return, bound savepoint, or map default.
    /// </summary>
    public EnterMapResult BeginEnter(ulong mapId, ulong teleportId)
    {
        var target = mapId == 0 ? SpawnMap : mapId;

        if (!_assets.Maps.MapExists(target))
            return new EnterMapResult((int)EnmTextCode.EnmTextMapConfNotFound, SpawnMap);

        // Allow re-entry into the current scenario map even when it disables pawn control.
        if (!_assets.Maps.IsPlayable(target) && target != SpawnMap)
            return new EnterMapResult((int)EnmTextCode.EnmTextMapCondition, SpawnMap);

        if (teleportId != 0)
        {
            if (!_assets.Maps.TeleportExists(teleportId))
                return new EnterMapResult((int)EnmTextCode.EnmTextMapConfNotFound, SpawnMap);

            if (!_unlockedTeleports.Contains(teleportId))
                return new EnterMapResult((int)EnmTextCode.EnmTextMapCondition, SpawnMap);

            if (_assets.Maps.Teleport(teleportId)?.MapId != target)
                return new EnterMapResult((int)EnmTextCode.EnmTextMapCondition, SpawnMap);
        }

        if (target != SpawnMap)
        {
            if (!_assets.Maps.IsScriptedWorld(SpawnMap) && _assets.Maps.IsScriptedWorld(target))
                ReturnPoint = new MapReturnPoint(SpawnMap, _position.X, _position.Y, _position.Z, _positionIsSynced);

            SpawnMap = target;
            if (teleportId == 0 && ReturnPoint is {} origin && origin.MapId == target)
            {
                _position = (origin.X, origin.Y, origin.Z);
                _positionIsSynced = origin.IsSynced;
            }
            else
            {
                _position = DefaultSpawnPos(target);
                _positionIsSynced = false;
            }

            if (!_assets.Maps.IsScriptedWorld(target))
                ReturnPoint = null;
        }

        Log.Stage("map entry prepared for map {MapId} teleport {TeleportId} savepoint {Savepoint}, saved position {PositionSynced}, return map {ReturnMapId}",
            target, teleportId, Savepoint, _positionIsSynced, ReturnPoint?.MapId);
        TeleportId = teleportId;
        Phase = MapPhase.Entering;

        return new EnterMapResult(Code: 0, SpawnMap);
    }

    private (int X, int Y, int Z) DefaultSpawnPos(ulong mapId)
    {
        if (_assets.Maps.Savepoint(Savepoint) is {} npc && npc.MapId == mapId
                                                        && _assets.Maps.SavepointPos(Savepoint) is {} pos)
            return pos;

        return _assets.Maps.SpawnPos(mapId) ?? _position;
    }

    public int FinishEnter()
    {
        if (Phase != MapPhase.Entering)
        {
            Log.Stage("map finish entry refused for map {MapId} in phase {Phase}", SpawnMap, Phase);
            return (int)EnmTextCode.EnmTextMapCondition;
        }

        Phase = MapPhase.Loaded;
        Log.Stage("map entry completed for map {MapId} teleport {TeleportId}", SpawnMap, TeleportId);
        return 0;
    }

    public Vector3Int SpawnPosition() => new() {
        X = _position.X,
        Y = _position.Y,
        Z = _position.Z
    };

    public bool RestoreReturnPosition(ulong mapId, (int X, int Y, int Z) position)
    {
        if (Phase != MapPhase.Idle || !_assets.Maps.IsPlayable(mapId))
            return false;

        SpawnMap = mapId;
        _position = position;
        _positionIsSynced = true;
        ReturnPoint = null;
        TeleportId = 0;

        return true;
    }

    /// <summary>Ignore position syncs during loading because they belong to the previous map.</summary>
    public bool SyncPosition((int X, int Y, int Z) position)
    {
        if (Phase != MapPhase.Loaded)
            return false;

        if (_position == position && _positionIsSynced)
            return true;

        _position = position;
        _positionIsSynced = true;

        return true;
    }

    public (ulong MapId, ulong Savepoint) RebornPoint()
    {
        if (IsOnLevelOf(Savepoint, SpawnMap))
            return (SpawnMap, Savepoint);

        for (var i = _unlockedSavepoints.Count - 1; i >= 0; i--)
        {
            if (IsOnLevelOf(_unlockedSavepoints[i], SpawnMap))
                return (SpawnMap, _unlockedSavepoints[i]);
        }

        return (SpawnMap, Savepoint);
    }

    private bool IsOnLevelOf(ulong savepoint, ulong mapId)
    {
        if (_assets.Maps.Savepoint(savepoint) is not {} npc)
            return false;

        if (npc.MapId == mapId)
            return true;

        return _assets.Maps.Map(npc.MapId)?.LevelPath is { Length: > 0 } level
               && level == _assets.Maps.Map(mapId)?.LevelPath;
    }

    public void Respawn()
    {
        if (_assets.Maps.SavepointPos(Savepoint) is not {} pos)
            return;

        _position = pos;
        _positionIsSynced = false;
        ReturnPoint = null;
        TeleportId = 0;

        if (_assets.Maps.Savepoint(Savepoint) is {} npc && npc.MapId != SpawnMap)
            SpawnMap = npc.MapId;

    }
}
