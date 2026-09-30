using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleGameTimeSetup
{
    [GameHandler(EClientServerCmds.CsGametimeSetupReq)]
    public Task<SCGameTimeSetupRes> OnPacket(NetContext ctx, CSGameTimeSetupReq req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCGameTimeSetupRes { Result = (int)EnmTextCode.EnmTextNotAccLogin });

        return Task.FromResult(ctx.Player.SetupGameTime(req.PassTime, req.Weather));
    }
}
