using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleDailyMissionActivePointClaim
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsDailyMissionActivepointClaim)]
    public Task<SCDailyMissionActivePointClaim> OnPacket(NetContext ctx, CSDailyMissionActivePointClaim req)
    {
        var (result, item, activePoint) = ctx.Player.DailyMissions.ClaimActivePoint(req.MissionId);

        return Task.FromResult(new SCDailyMissionActivePointClaim {
            Result = result,
            ActivePoint = activePoint,
            MissionItem = item ?? new DailyMissionItem { MissionId = req.MissionId }
        });
    }
}
