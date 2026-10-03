using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleGameTimeSetup
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsGametimeSetupReq)]
    public Task<SCGameTimeSetupRes> OnPacket(NetContext ctx, CSGameTimeSetupReq req)
    {
        return Task.FromResult(ctx.Player.SetupGameTime(req.PassTime, req.Weather));
    }
}
