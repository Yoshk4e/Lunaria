using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleWantedOutsideData
{
    [GameHandler(EClientServerCmds.CsWantedOutsideData)]
    public Task<SCWantedOutsideData> OnPacket(NetContext ctx, CSWantedOutsideData req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCWantedOutsideData { Result = (int)EnmTextCode.EnmTextNotAccLogin });

        return Task.FromResult(ctx.Player.Wanted.ToOutsideData());
    }
}
