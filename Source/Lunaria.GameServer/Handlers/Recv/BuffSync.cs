using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleBuffSync
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsBuffSync)]
    public Task<SCBuffSync> OnPacket(NetContext ctx, CSBuffSync req)
    {
        var reply = new SCBuffSync { Result = 0 };
        reply.Data.AddRange(ctx.Player.Buffs.ToBuffData(ctx.Player.UtcNow));
        return Task.FromResult(reply);
    }
}
