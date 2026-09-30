using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleReadGuide(ILogger<HandleReadGuide> logger)
{
    [GameHandler(EClientServerCmds.CsReqReadGuide)]
    public async Task<SCResReadGuide> OnPacket(NetContext ctx, CSReqReadGuide req)
    {
        if (!ctx.Player.HasActiveRole)
            return new SCResReadGuide {
                Result = (int)EnmTextCode.EnmTextNotAccLogin
            };

        var (changed, delivery) = ctx.Player.ReadGuides(req.GuideId.ToList());
        if (changed.Count > 0)
            logger.LogDebug("guides read: {Ids}", string.Join(separator: ',', changed));
        if (delivery.HasChanges)
            await ctx.NotifyAsync(delivery.Presentation).ConfigureAwait(false);

        return new SCResReadGuide {
            Result = 0,
            GuideInfo = { ctx.Player.Guides.InfosOf(req.GuideId.ToList()) }
        };
    }
}
