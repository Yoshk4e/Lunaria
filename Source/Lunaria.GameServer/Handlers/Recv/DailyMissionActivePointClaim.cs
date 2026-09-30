using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleDailyMissionActivePointClaim
{
    [GameHandler(EClientServerCmds.CsDailyMissionActivepointClaim)]
    public Task<SCDailyMissionActivePointClaim> OnPacket(NetContext ctx, CSDailyMissionActivePointClaim req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCDailyMissionActivePointClaim {
                Result = (int)EnmTextCode.EnmTextNotAccLogin,
                MissionItem = new DailyMissionItem { MissionId = req.MissionId }
            });

        var (result, item, activePoint) = ctx.Player.DailyMissions.ClaimActivePoint(req.MissionId);

        return Task.FromResult(new SCDailyMissionActivePointClaim {
            Result = result,
            ActivePoint = activePoint,
            MissionItem = item ?? new DailyMissionItem { MissionId = req.MissionId }
        });
    }
}
