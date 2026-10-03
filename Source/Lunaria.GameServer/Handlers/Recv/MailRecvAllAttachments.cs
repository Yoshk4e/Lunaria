using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleMailRecvAllAttachments(ILogger<HandleMailRecvAllAttachments> logger)
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsMailRecvAllAttachments)]
    public async Task<SCMailRecvAllAttachments> OnPacket(NetContext ctx, CSMailRecvAllAttachments req)
    {
        var (claimedIds, delivery) = ctx.Player.ClaimAllMailAttachments();
        await ctx.NotifyAsync(delivery.Presentation).ConfigureAwait(false);

        var reply = new SCMailRecvAllAttachments {
            Result = 0
        };
        reply.MailIds.AddRange(claimedIds);

        if (claimedIds.Count > 0)
            logger.LogDebug("mails claimed all attachments: {Count} mails", claimedIds.Count);

        return reply;
    }
}
