using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;


public sealed class HandleWantedInsideData
{
    [GameHandler(EClientServerCmds.CsWantedInsideData)]
    public Task<SCWantedInsideData> OnPacket(NetContext ctx, CSWantedInsideData req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCWantedInsideData { Result = (int)EnmTextCode.EnmTextNotAccLogin });

        return Task.FromResult(ctx.Player.Wanted.ToInsideData()
                               ?? new SCWantedInsideData { Result = (int)EnmTextCode.EnmTextWantedNotInWanted });
    }
}
