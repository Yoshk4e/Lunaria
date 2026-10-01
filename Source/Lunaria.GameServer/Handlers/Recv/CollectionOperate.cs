using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleCollectionOperate(ILogger<HandleCollectionOperate> logger)
{
    [GameHandler(EClientServerCmds.CsCollectionOperate)]
    public async Task<SCCollectionOperate> OnPacket(NetContext ctx, CSCollectionOperate req)
    {
        // Without collection_item the client ignores the answer and the object stays in its opening state.
        SCCollectionOperate Reject(int code) => new() {
            Result = code,
            CollectionItem = (ctx.Player.HasActiveRole ? ctx.Player.CollectionData(req.UniqId) : null)
                             ?? new OneCollectionData { UniqId = req.UniqId }
        };

        if (!ctx.Player.HasActiveRole)
            return Reject((int)EnmTextCode.EnmTextNotAccLogin);

        var now = DateTimeOffset.UtcNow;
        var (code, outcome) = ctx.Player.Collect(req.UniqId, req.Op, now);

        if (code != 0 || outcome is null)
            return Reject(code);

        await ctx.NotifyAsync(outcome.Delivery.Presentation)
            .ConfigureAwait(false);

        var ntf = new SCCollectionDataNtf();
        ntf.NtfList.Add(outcome.Item);

        try
        {
            await ctx.NotifyAsync(ntf).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            logger.LogDebug(ex, "collection notification dropped");
        }

        logger.LogDebug("collection {Uniq} op {Op}: gathered", req.UniqId, req.Op);

        return new SCCollectionOperate {
            Result = 0,
            CollectionItem = outcome.Item
        };
    }
}
