using Msg;

namespace Lunaria.Game.World;

public sealed partial class MapManager
{
    private readonly List<TrackedTargetInfo> _trackedTargets = [];

    public IReadOnlyList<TrackedTargetInfo> TrackedTargets => _trackedTargets;

    public int TrackTarget(ulong mapId, ulong tagId, uint tagType)
    {
        if (!_assets.Maps.MapExists(mapId))
            return (int)EnmTextCode.EnmTextMapConfNotFound;

        if (IsTracked(mapId, tagId, tagType))
            return 0;

        _trackedTargets.Add(new TrackedTargetInfo { MapId = mapId, TagId = tagId, TagType = tagType });
        IsDirty = true;
        return 0;
    }

    public int CancelTrackTarget(ulong mapId, ulong tagId, uint tagType)
    {
        if (!IsTracked(mapId, tagId, tagType))
            return (int)EnmTextCode.EnmTextWrongParam;

        _trackedTargets.RemoveAll(target =>
            target.MapId == mapId && target.TagId == tagId && target.TagType == tagType);
        IsDirty = true;
        return 0;
    }

    private bool IsTracked(ulong mapId, ulong tagId, uint tagType) =>
        _trackedTargets.Any(target =>
            target.MapId == mapId && target.TagId == tagId && target.TagType == tagType);
}
