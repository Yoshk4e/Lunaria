using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleLocationSync(ILogger<HandleLocationSync> logger)
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsLocationSync)]
    public Task OnPacket(NetContext ctx, CSLocationSync req)
    {
        if (req.Location is {} location)
            ctx.Player.Map.SyncPosition((location.X, location.Y, location.Z));

        if (req.UsingMemberSlot != 0)
        {
            var code = ctx.Player.SetCurrentTeamSlot(req.UsingMemberSlot);

            if (code != 0)
                logger.LogDebug("location sync kept slot {Slot}: result {Result}", req.UsingMemberSlot, code);
        }

        return Task.CompletedTask;
    }
}
