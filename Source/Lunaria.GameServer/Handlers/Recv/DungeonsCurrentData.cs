using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleDungeonsCurrentData
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsDungeonsCurrentData)]
    public async Task<SCDungeonsCurrentData> OnPacket(NetContext ctx, CSDungeonsCurrentData req)
    {
        if (req.Info is {} info)
        {
            var outcome = ctx.Player.AdoptDungeonCurrent(info.DungeonsId, info.BattleId);

            if (outcome.Code != 0)
                return new SCDungeonsCurrentData { Result = outcome.Code };
        }

        return new SCDungeonsCurrentData {
            Result = 0,
            Info = req.Info
        };
    }
}
