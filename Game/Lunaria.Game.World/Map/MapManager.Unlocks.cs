using Lunaria.Common.Tracking;
using Lunaria.Game.Logging;
using Msg;

namespace Lunaria.Game.World;

public sealed partial class MapManager : TrackedObject
{
    public bool IsSavepointUnlocked(ulong id) => _unlockedSavepoints.Contains(id);

    public bool IsTeleportUnlocked(ulong id) => _unlockedTeleports.Contains(id);

    public int UnlockSavepoint(ulong id)
    {
        if (!_assets.Maps.SavepointExists(id))
            return (int)EnmTextCode.EnmTextMapConfNotFound;

        if (_unlockedSavepoints.Contains(id))
            return (int)EnmTextCode.EnmTextCollectionAlreadyOp;

        if (_unlockedSavepoints.Count >= MaxSavepoints)
            return (int)EnmTextCode.EnmTextCountGroupLimit;

        _unlockedSavepoints.Add(id);
        Log.Stage("savepoint {SavepointId} unlocked on map {MapId}", id, SpawnMap);

        return 0;
    }

    public int BindSavepoint(ulong id)
    {
        if (!_assets.Maps.SavepointExists(id))
            return (int)EnmTextCode.EnmTextMapConfNotFound;

        if (!_unlockedSavepoints.Contains(id))
            return (int)EnmTextCode.EnmTextMapCondition;

        if (Savepoint == id)
            return 0;

        Savepoint = id;

        return 0;
    }

    public int Activate(ulong id)
    {
        var code = UnlockSavepoint(id);

        if (code != 0 && code != (int)EnmTextCode.EnmTextCollectionAlreadyOp)
            return code;

        return BindSavepoint(id);
    }

    public int UnlockTeleport(ulong id)
    {
        if (!_assets.Maps.TeleportExists(id))
            return (int)EnmTextCode.EnmTextMapConfNotFound;

        if (_unlockedTeleports.Contains(id))
            return (int)EnmTextCode.EnmTextCollectionAlreadyOp;

        _unlockedTeleports.Add(id);
        Log.Stage("teleport {TeleportId} unlocked on map {MapId}", id, SpawnMap);

        return 0;
    }
}
