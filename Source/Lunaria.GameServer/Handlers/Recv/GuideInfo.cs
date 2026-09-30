using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleGuideInfo(ILogger<HandleGuideInfo> logger)
{
    [GameHandler(EClientServerCmds.CsReqGuideInfo)]
    public Task<SCResGuideInfo> OnPacket(NetContext ctx, CSReqGuideInfo req)
    {
        var guideList = ctx.Player.Guides.Infos();
        logger.LogDebug("guide info: {Entries} entries", guideList.Count);
        return Task.FromResult(new SCResGuideInfo { Result = 0, GuideList = { guideList } });
    }
}
