using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;


public sealed class HandleDailyMissionQuery
{
    [GameHandler(EClientServerCmds.CsDailyMissionQuery)]
    public Task<SCDailyMissionQuery> OnPacket(NetContext ctx, CSDailyMissionQuery req)
    {
        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(new SCDailyMissionQuery {
                Result = (int)EnmTextCode.EnmTextNotAccLogin,
                Data = new DailyMissionData {
                    MissionListData = new DailyMissionListData(),
                    RewardData = new DailyMissionRewardData()
                }
            });

        return Task.FromResult(new SCDailyMissionQuery {
            Result = 0,
            Data = ctx.Player.DailyMissions.ToDailyMissionData()
        });
    }
}
