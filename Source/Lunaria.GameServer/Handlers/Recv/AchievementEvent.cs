using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleAchievementEvent
{
    [GameHandler(EClientServerCmds.CsAchievementEvent)]
    public async Task<SCAchievementEvent> OnPacket(NetContext ctx, CSAchievementEvent req)
    {
        if (!ctx.Player.HasActiveRole)
            return new SCAchievementEvent {
                Result = (int)EnmTextCode.EnmTextNotAccLogin,
                Event = req.Event,
                Args = { req.Args }
            };

        // An event report counts one occurrence.
        var code = ctx.Player.ReportClientProgress(req.Event, count: 1);
        await ctx.FlushGameplayChangesAsync().ConfigureAwait(false);

        return new SCAchievementEvent {
            Result = code,
            Event = req.Event,
            Args = { req.Args }
        };
    }
}
