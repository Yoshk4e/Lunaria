using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleEnterMap(ILogger<HandleEnterMap> logger)
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsEnterMap)]
    public async Task OnPacket(NetContext ctx, CSEnterMap req)
    {
        var role = ctx.Player.Roles.Active()!;

        if ((ulong)role.Id != req.RoleId)
        {
            logger.LogWarning("enter_map role mismatch: asked {Asked}, active {Active}", req.RoleId, role.Id);
            return;
        }

        var entry = ctx.Player.Map.BeginEnter(req.MapId, req.TeleportId);

        await ctx.SendAsync(new SCEnterMap {
            Result = entry.Code,
            RoleId = req.RoleId,
            MapId = entry.MapId,
            TeleportId = req.TeleportId
        }).ConfigureAwait(false);

        if (!entry.Ok)
        {
            logger.LogWarning(
                "enter_map refused: map {MapId}, teleport {TeleportId}, result {Result}",
                req.MapId, req.TeleportId, entry.Code);
            return;
        }

        var notification = new SCMapReadyNtf {
            MapId = entry.MapId,
            IsInitedName = role.Initialized,
            LastLogoutLocation = ctx.Player.Map.SpawnPosition(),
            CurrentSavepoint = ctx.Player.Map.Savepoint,
            BornPosType = ctx.Player.Map.BornPosType,
            UnlockedSavepoints = { ctx.Player.Map.UnlockedSavepoints },
            UnlockedTeleportIdList = { ctx.Player.Map.UnlockedTeleports },
            TeleportId = ctx.Player.Map.TeleportId
        };
        await ctx.NotifyAsync(notification).ConfigureAwait(false);
        logger.LogInformation("map ready pushed: map {MapId}, named {Named}", entry.MapId, role.Initialized);
    }
}
