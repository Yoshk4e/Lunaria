using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleEntityOperation
{
    [GameHandler(EClientServerCmds.CsEntityOperation)]
    public Task OnPacket(NetContext ctx, CSEntityOperation req) => Task.CompletedTask;
}
