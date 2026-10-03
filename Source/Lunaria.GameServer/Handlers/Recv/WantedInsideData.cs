using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;


public sealed class HandleWantedInsideData
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsWantedInsideData)]
    public Task<SCWantedInsideData> OnPacket(NetContext ctx, CSWantedInsideData req)
    {
        return Task.FromResult(ctx.Player.Wanted.ToInsideData()
                               ?? new SCWantedInsideData { Result = (int)EnmTextCode.EnmTextWantedNotInWanted });
    }
}
