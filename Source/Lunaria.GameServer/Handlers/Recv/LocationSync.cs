using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleLocationSync(ILogger<HandleLocationSync> logger)
{
    [GameHandler(EClientServerCmds.CsLocationSync)]
    public Task OnPacket(NetContext ctx, CSLocationSync req)
    {
        // Syncs keep arriving every half second while a map loads, so ignored ones stay at debug level.
        if (req.Location is {} location && !ctx.Player.Map.SyncPosition((location.X, location.Y, location.Z)))
            logger.LogDebug("location sync ignored in map phase {Phase}: {X} {Y} {Z}",
                ctx.Player.Map.Phase, location.X, location.Y, location.Z);

        if (req.UsingMemberSlot != 0)
        {
            var code = ctx.Player.SetCurrentTeamSlot(req.UsingMemberSlot);

            if (code != 0)
                logger.LogDebug("location sync kept slot {Slot}: result {Result}", req.UsingMemberSlot, code);
        }

        return Task.CompletedTask;
    }
}
