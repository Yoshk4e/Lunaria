using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleBattlePassData
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsBattlePassData)]
    public Task<SCBattlePassData> OnPacket(NetContext ctx, CSBattlePassData req)
    {
        return Task.FromResult(ctx.Player.BattlePasses.ToBattlePassData(req.BattlePassId));
    }
}
