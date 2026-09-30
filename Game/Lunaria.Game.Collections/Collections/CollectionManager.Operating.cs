using Msg;

namespace Lunaria.Game.Collections;

public sealed partial class CollectionManager
{
    public (int Code, CollectionState? Updated) ApplyCollected(ulong uniq, DateTimeOffset now)
    {
        if (!TryGetOperable(uniq, out var node, out var code))
            return (code, null);

        var status = assets.Collections.AutoDestroys(node.Cfg) ? EnmCollectionStatus.EcsDestroyed : EnmCollectionStatus.EcsCollected;
        var next = node with { Status = status, StatusTime = now };
        _nodes[uniq] = next;
        _gathered.Add(node.Cfg);
        Dirty();
        return (0, next);
    }

    public (int Code, CollectionState? Updated) ApplyDestroyed(ulong uniq, DateTimeOffset now)
    {
        if (!TryGetOperable(uniq, out var node, out var code))
            return (code, null);

        var next = node with { Status = EnmCollectionStatus.EcsDestroyed, StatusTime = now };
        _nodes[uniq] = next;
        Dirty();
        return (0, next);
    }

    private bool TryGetOperable(ulong uniq, out CollectionState node, out int code)
    {
        if (!_nodes.TryGetValue(uniq, out node!))
        {
            code = (int)EnmTextCode.EnmTextCollectionNoData;
            return false;
        }

        if (node.Status != EnmCollectionStatus.EcsCanCollect)
        {
            code = (int)EnmTextCode.EnmTextCollectionAlreadyOp;
            return false;
        }

        code = 0;
        return true;
    }
}
