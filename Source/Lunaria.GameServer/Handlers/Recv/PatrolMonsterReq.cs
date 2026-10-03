using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandlePatrolMonsterReq
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsPatrolMonsterReq)]
    public Task<SCPatrolMonsterRes> OnPacket(NetContext ctx, CSPatrolMonsterReq req)
    {
        var reply = new SCPatrolMonsterRes { Result = 0 };
        reply.CdMonsters.AddRange(ctx.Player.PatrolCooldowns.Select(id => (uint)id));
        return Task.FromResult(reply);
    }
}
