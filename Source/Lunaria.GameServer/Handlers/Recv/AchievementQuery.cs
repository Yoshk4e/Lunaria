using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleAchievementQuery
{
    [GameHandler(EClientServerCmds.CsAchievementQuery)]
    public Task<SCAchievementQuery> OnPacket(NetContext ctx, CSAchievementQuery req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCAchievementQuery {
                Result = (int)EnmTextCode.EnmTextNotAccLogin,
                Data = new CmdAchievementData()
            });

        return Task.FromResult(new SCAchievementQuery {
            Result = 0,
            Data = ctx.Player.Achievements.ToAchievementData()
        });
    }
}
