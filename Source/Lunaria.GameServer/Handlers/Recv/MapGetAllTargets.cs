using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleMapGetAllTargets
{
    [GameHandler(EClientServerCmds.CsMapGetAllTargets)]
    public Task<SCMapGetAllTargets> OnPacket(NetContext ctx, CSMapGetAllTargets req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCMapGetAllTargets { Result = (int)EnmTextCode.EnmTextNotAccLogin });

        var reply = new SCMapGetAllTargets { Result = 0 };
        reply.TrackedTargetList.AddRange(ctx.Player.Map.TrackedTargets);
        return Task.FromResult(reply);
    }
}
