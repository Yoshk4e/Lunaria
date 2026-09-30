using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleBattlePassData
{
    [GameHandler(EClientServerCmds.CsBattlePassData)]
    public Task<SCBattlePassData> OnPacket(NetContext ctx, CSBattlePassData req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCBattlePassData {
                Result = (int)EnmTextCode.EnmTextNotAccLogin,
                Datas = { req.BattlePassId.Select(id => new CmdOneBattelPassData { Id = id }) }
            });

        return Task.FromResult(ctx.Player.BattlePasses.ToBattlePassData(req.BattlePassId));
    }
}
