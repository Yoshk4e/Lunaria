using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleAchievementQuery
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsAchievementQuery)]
    public Task<SCAchievementQuery> OnPacket(NetContext ctx, CSAchievementQuery req)
    {
        return Task.FromResult(new SCAchievementQuery {
            Result = 0,
            Data = ctx.Player.Achievements.ToAchievementData()
        });
    }
}
