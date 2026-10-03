using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleWantedBionicGiveupReq
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsWantedBionicGiveupReq)]
    public async Task<SCWantedBionicGiveupRsp> OnPacket(NetContext ctx, CSWantedBionicGiveupReq req)
    {
        var result = ctx.Player.GiveUpWantedBionics(req.BionicsUniqid);

        if (result != 0)
            return new SCWantedBionicGiveupRsp { Result = result };

        return new SCWantedBionicGiveupRsp { Result = 0 };
    }
}
