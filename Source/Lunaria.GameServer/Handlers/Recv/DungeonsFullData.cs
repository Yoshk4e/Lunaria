using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleDungeonsFullData
{
    [GameHandler(EClientServerCmds.CsDungeonsFullData)]
    public Task<SCDungeonsFullData> OnPacket(NetContext ctx, CSDungeonsFullData req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCDungeonsFullData {
                Result = (int)EnmTextCode.EnmTextNotAccLogin,
                Data = new CSDungeonsData {
                    CommonData = new CSDungeonsCommonData(),
                    HordeData = new CSHordeData()
                }
            });

        return Task.FromResult(new SCDungeonsFullData {
            Result = 0,
            Data = ctx.Player.Dungeons.ToFullData(ctx.Player.UtcNow)
        });
    }
}
