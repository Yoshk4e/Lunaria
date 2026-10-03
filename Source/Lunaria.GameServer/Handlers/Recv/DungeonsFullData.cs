using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleDungeonsFullData
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsDungeonsFullData)]
    public Task<SCDungeonsFullData> OnPacket(NetContext ctx, CSDungeonsFullData req)
    {
        return Task.FromResult(new SCDungeonsFullData {
            Result = 0,
            Data = ctx.Player.Dungeons.ToFullData(ctx.Player.UtcNow)
        });
    }
}
