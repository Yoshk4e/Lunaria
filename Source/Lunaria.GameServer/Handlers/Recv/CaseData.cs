using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleCaseData
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsCaseData)]
    public Task<SCCaseData> OnPacket(NetContext ctx, CSCaseData req)
    {
        return Task.FromResult(ctx.Player.Cases.ToCaseData());
    }
}
