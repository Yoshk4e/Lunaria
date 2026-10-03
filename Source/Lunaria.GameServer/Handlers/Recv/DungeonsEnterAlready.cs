using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleDungeonsEnterAlready
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsDungeonsEnterAlready)]
    public Task<SCDungeonsEnterAlready> OnPacket(NetContext ctx, CSDungeonsEnterAlready req)
    {
        return Task.FromResult(new SCDungeonsEnterAlready {
            Result = 0,
            Info = ctx.Player.Dungeons.Current is {} current ?
                new DungeonsCurrentData {
                    DungeonsId = (uint)current.DungeonId,
                    BattleId = ctx.Player.Dungeons.CompletedBattle()
                } :
                null
        });
    }
}
