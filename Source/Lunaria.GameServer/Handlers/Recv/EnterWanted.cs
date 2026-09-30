using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleEnterWanted
{
    [GameHandler(EClientServerCmds.CsEnterWanted)]
    public Task<SCEnterWanted> OnPacket(NetContext ctx, CSEnterWanted req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCEnterWanted { Result = (int)EnmTextCode.EnmTextNotAccLogin });

        return Task.FromResult(new SCEnterWanted {
            Result = ctx.Player.EnterWanted(req.Id, req.CharacterIds)
        });
    }
}
