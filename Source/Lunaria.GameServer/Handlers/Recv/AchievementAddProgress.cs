using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;


public sealed class HandleAchievementAddProgress
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsAchievementAddProgress)]
    public async Task<SCAchievementAddProgress> OnPacket(NetContext ctx, CSAchievementAddProgress req)
    {
        var code = ctx.Player.ReportClientProgress(req.Id, req.Count);
        await ctx.FlushGameplayChangesAsync().ConfigureAwait(false);
        return new SCAchievementAddProgress { Result = code, Id = req.Id };
    }
}
