using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleSilverCreatureList
{
    [GameHandler(EClientServerCmds.CsReqSilverCreatureList)]
    public Task<SCSilverCreatureList> OnPacket(NetContext ctx, CSReqSilverCreatureList req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCSilverCreatureList { Result = (int)EnmTextCode.EnmTextNotAccLogin });

        return Task.FromResult(ctx.Player.SilverCreatures.ToList());
    }
}
