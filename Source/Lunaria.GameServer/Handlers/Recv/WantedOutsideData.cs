using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleWantedOutsideData
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsWantedOutsideData)]
    public Task<SCWantedOutsideData> OnPacket(NetContext ctx, CSWantedOutsideData req)
    {
        return Task.FromResult(ctx.Player.Wanted.ToOutsideData());
    }
}
