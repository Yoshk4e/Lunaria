using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleSilverCreatureList
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsReqSilverCreatureList)]
    public Task<SCSilverCreatureList> OnPacket(NetContext ctx, CSReqSilverCreatureList req)
    {
        return Task.FromResult(ctx.Player.SilverCreatures.ToList());
    }
}
