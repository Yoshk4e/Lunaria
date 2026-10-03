using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;


public sealed class HandleDailyMissionQuery
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsDailyMissionQuery)]
    public Task<SCDailyMissionQuery> OnPacket(NetContext ctx, CSDailyMissionQuery req)
    {
        return Task.FromResult(new SCDailyMissionQuery {
            Result = 0,
            Data = ctx.Player.DailyMissions.ToDailyMissionData()
        });
    }
}
