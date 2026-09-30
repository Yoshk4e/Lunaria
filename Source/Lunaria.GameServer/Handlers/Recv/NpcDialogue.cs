using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleNpcDialogue
{
    [GameHandler(EClientServerCmds.CsNpcDialogue)]
    public Task OnPacket(NetContext ctx, CSNpcDialogue req) => Task.CompletedTask;
}
