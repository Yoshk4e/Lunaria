using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleEnterWanted
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsEnterWanted)]
    public Task<SCEnterWanted> OnPacket(NetContext ctx, CSEnterWanted req)
    {
        return Task.FromResult(new SCEnterWanted {
            Result = ctx.Player.EnterWanted(req.Id, req.CharacterIds)
        });
    }
}
