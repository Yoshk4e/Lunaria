using Lunaria.Common.Tracking;
using Lunaria.Game.Logging;
using Msg;

namespace Lunaria.Game.Collections;

public sealed partial class CollectionManager : TrackedObject
{
    public (int Code, CollectionState? Updated) ApplyCollected(ulong uniq, DateTimeOffset now)
    {
        if (!TryGetOperable(uniq, out var node, out var code))
            return (code, null);

        var status = assets.Collections.AutoDestroys(node.Cfg) ? EnmCollectionStatus.EcsDestroyed : EnmCollectionStatus.EcsCollected;
        var next = node with { Status = status, StatusTime = now };
        _nodes[uniq] = next;
        _gathered.Add(node.Cfg);

        Log.Event("collection {UniqId} with config {ConfigId} collected, new status {Status}", uniq, node.Cfg, status);
        return (0, next);
    }

    public (int Code, CollectionState? Updated) ApplyDestroyed(ulong uniq, DateTimeOffset now)
    {
        if (!TryGetOperable(uniq, out var node, out var code))
            return (code, null);

        var next = node with { Status = EnmCollectionStatus.EcsDestroyed, StatusTime = now };
        _nodes[uniq] = next;

        return (0, next);
    }

    private bool TryGetOperable(ulong uniq, out CollectionState node, out int code)
    {
        if (Get(uniq) is not {} found)
        {
            node = null!;
            Log.Stage("collection operation refused for unknown object {UniqId}", uniq);
            code = (int)EnmTextCode.EnmTextCollectionNoData;
            return false;
        }

        node = found;

        if (node.Status != EnmCollectionStatus.EcsCanCollect)
        {
            Log.Stage("collection operation refused for object {UniqId} in status {Status}", uniq, node.Status);
            code = (int)EnmTextCode.EnmTextCollectionAlreadyOp;
            return false;
        }

        code = 0;
        return true;
    }
}
