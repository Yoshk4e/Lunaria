using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;


public sealed class HandleAchievementAddProgress
{
    [GameHandler(EClientServerCmds.CsAchievementAddProgress)]
    public async Task<SCAchievementAddProgress> OnPacket(NetContext ctx, CSAchievementAddProgress req)
    {
        if (!ctx.Player.HasActiveRole)
            return new SCAchievementAddProgress {
                Result = (int)EnmTextCode.EnmTextNotAccLogin,
                Id = req.Id
            };

        var code = ctx.Player.ReportClientProgress(req.Id, req.Count);
        await ctx.FlushGameplayChangesAsync().ConfigureAwait(false);
        return new SCAchievementAddProgress { Result = code, Id = req.Id };
    }
}
