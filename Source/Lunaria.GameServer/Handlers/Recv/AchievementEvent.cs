using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleAchievementEvent
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsAchievementEvent)]
    public async Task<SCAchievementEvent> OnPacket(NetContext ctx, CSAchievementEvent req)
    {
        var code = ctx.Player.ReportClientProgress(req.Event, count: 1);
        await ctx.FlushGameplayChangesAsync().ConfigureAwait(false);

        return new SCAchievementEvent {
            Result = code,
            Event = req.Event,
            Args = { req.Args }
        };
    }
}
