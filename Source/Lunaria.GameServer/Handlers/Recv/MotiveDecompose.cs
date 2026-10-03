using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleMotiveDecompose(ILogger<HandleMotiveDecompose> logger)
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsMotiveDecompose)]
    public async Task<SCMotiveDecompose> OnPacket(NetContext ctx, CSMotiveDecompose req)
    {
        SCMotiveDecompose Reject(int code)
        {
            return new SCMotiveDecompose { Result = code };
        }

        var outcome = ctx.Player.DecomposeMotives(req.MotiveUniqIds.ToList());

        if (!outcome.Ok)
            return Reject(outcome.Code);

        var recycleWire = outcome.Recycle
            .Select(g => new ItemIdCount { ItemId = g.ItemId, Count = g.Count })
            .ToList();

        await ctx.NotifyAsync(outcome.Delivery.Presentation).ConfigureAwait(false);

        logger.LogDebug("motives decomposed: {Count} instances", req.MotiveUniqIds.Count);
        var res = new SCMotiveDecompose { Result = 0 };
        res.RecycleItems.AddRange(recycleWire);
        return res;
    }
}
