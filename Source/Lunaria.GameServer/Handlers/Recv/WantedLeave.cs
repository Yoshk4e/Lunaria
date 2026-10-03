using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleWantedLeave
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsWantedLeave)]
    public Task<SCWantedLeave> OnPacket(NetContext ctx, CSWantedLeave req)
    {
        return Task.FromResult(new SCWantedLeave { Result = ctx.Player.LeaveWanted() });
    }
}
