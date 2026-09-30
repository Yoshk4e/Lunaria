using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleWantedLeave
{
    [GameHandler(EClientServerCmds.CsWantedLeave)]
    public Task<SCWantedLeave> OnPacket(NetContext ctx, CSWantedLeave req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCWantedLeave { Result = (int)EnmTextCode.EnmTextNotAccLogin });

        return Task.FromResult(new SCWantedLeave { Result = ctx.Player.LeaveWanted() });
    }
}
