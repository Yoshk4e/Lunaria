using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleCaseData
{
    [GameHandler(EClientServerCmds.CsCaseData)]
    public Task<SCCaseData> OnPacket(NetContext ctx, CSCaseData req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCCaseData());

        return Task.FromResult(ctx.Player.Cases.ToCaseData());
    }
}
