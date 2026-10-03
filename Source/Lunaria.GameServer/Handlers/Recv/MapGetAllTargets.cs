using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleMapGetAllTargets
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsMapGetAllTargets)]
    public Task<SCMapGetAllTargets> OnPacket(NetContext ctx, CSMapGetAllTargets req)
    {
        var reply = new SCMapGetAllTargets { Result = 0 };
        reply.TrackedTargetList.AddRange(ctx.Player.Map.TrackedTargets);
        return Task.FromResult(reply);
    }
}
