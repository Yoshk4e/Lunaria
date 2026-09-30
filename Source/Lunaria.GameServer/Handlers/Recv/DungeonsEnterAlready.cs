using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleDungeonsEnterAlready
{
    [GameHandler(EClientServerCmds.CsDungeonsEnterAlready)]
    public Task<SCDungeonsEnterAlready> OnPacket(NetContext ctx, CSDungeonsEnterAlready req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCDungeonsEnterAlready {
                Result = (int)EnmTextCode.EnmTextNotAccLogin
            });

        return Task.FromResult(new SCDungeonsEnterAlready {
            Result = 0,
            Info = ctx.Player.Dungeons.Current is {} current ?
                new DungeonsCurrentData {
                    DungeonsId = (uint)current.DungeonId,
                    BattleId = current.BattleId
                } :
                null
        });
    }
}
