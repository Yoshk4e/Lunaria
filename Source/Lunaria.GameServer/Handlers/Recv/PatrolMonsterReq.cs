using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandlePatrolMonsterReq
{
    [GameHandler(EClientServerCmds.CsPatrolMonsterReq)]
    public Task<SCPatrolMonsterRes> OnPacket(NetContext ctx, CSPatrolMonsterReq req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCPatrolMonsterRes { Result = (int)EnmTextCode.EnmTextNotAccLogin });

        var reply = new SCPatrolMonsterRes { Result = 0 };
        reply.CdMonsters.AddRange(ctx.Player.PatrolCooldowns.Select(id => (uint)id));
        return Task.FromResult(reply);
    }
}
