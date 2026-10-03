using Lunaria.Common.Tracking;
using Msg;

namespace Lunaria.Game.World;

public sealed partial class MapManager : TrackedObject
{
    private sealed record MapTarget(ulong MapId, ulong TagId, uint TagType);
    private readonly TrackedList<MapTarget> __tracked_trackedTargets = [];
    [Tracked]
    private partial TrackedList<MapTarget> _trackedTargets { get; }

    public IReadOnlyList<TrackedTargetInfo> TrackedTargets => _trackedTargets.Select(t => new TrackedTargetInfo {
        MapId = t.MapId, TagId = t.TagId, TagType = t.TagType }).ToArray();

    public int TrackTarget(ulong mapId, ulong tagId, uint tagType)
    {
        if (!_assets.Maps.MapExists(mapId))
            return (int)EnmTextCode.EnmTextMapConfNotFound;

        if (IsTracked(mapId, tagId, tagType))
            return 0;

        _trackedTargets.Add(new MapTarget(mapId, tagId, tagType));

        return 0;
    }

    public int CancelTrackTarget(ulong mapId, ulong tagId, uint tagType)
    {
        if (!IsTracked(mapId, tagId, tagType))
            return (int)EnmTextCode.EnmTextWrongParam;

        _trackedTargets.RemoveAll(target =>
            target.MapId == mapId && target.TagId == tagId && target.TagType == tagType);

        return 0;
    }

    private bool IsTracked(ulong mapId, ulong tagId, uint tagType) =>
        _trackedTargets.Any(target =>
            target.MapId == mapId && target.TagId == tagId && target.TagType == tagType);
}
