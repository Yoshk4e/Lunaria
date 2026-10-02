using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleCollectionGetList(ILogger<HandleCollectionGetList> logger)
{
    [GameHandler(EClientServerCmds.CsCollectionGetList)]
    public Task<SCCollectionGetList> OnPacket(NetContext ctx, CSCollectionGetList req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCCollectionGetList {
                Result = (int)EnmTextCode.EnmTextNotAccLogin
            });

        var now = ctx.Player.UtcNow;
        var items = ctx.Player.GetCollections(req.BlockId, now);

        var res = new SCCollectionGetList { Result = 0 };
        res.ItemList.AddRange(items);
        logger.LogDebug("collection list: block {BlockId}, {Count} nodes", req.BlockId, items.Count);
        return Task.FromResult(res);
    }
}
