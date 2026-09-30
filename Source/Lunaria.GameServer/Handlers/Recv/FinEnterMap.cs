using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleFinEnterMap(ILogger<HandleFinEnterMap> logger)
{
    [GameHandler(EClientServerCmds.CsFinEnterMap)]
    public async Task<SCFinEnterMap> OnPacket(NetContext ctx, CSFinEnterMap req)
    {
        if (ctx.Player.Roles.Active() is not {} role)
        {
            logger.LogWarning("fin_enter_map without active role: role {RoleId}", req.RoleId);
            return new SCFinEnterMap { Result = (int)EnmTextCode.EnmTextNotAccLogin, RoleId = req.RoleId };
        }

        if ((ulong)role.Id != req.RoleId)
            return new SCFinEnterMap { Result = (int)EnmTextCode.EnmTextWrongParam, RoleId = req.RoleId };

        var code = ctx.Player.Map.FinishEnter();

        if (code != 0)
        {
            logger.LogWarning("fin_enter_map out of order: phase {Phase}", ctx.Player.Map.Phase);
            return new SCFinEnterMap { Result = code, RoleId = req.RoleId };
        }

        var mapId = ctx.Player.Map.MapId;
        logger.LogInformation("world entered: map {MapId}, role {RoleId}", mapId, role.Id);

        foreach (var outcome in ctx.Player.SettleMapArrival(mapId))
        {
            await ctx.NotifyAsync(outcome.AllNotifications).ConfigureAwait(false);
        }

        return new SCFinEnterMap {
            Result = 0,
            RoleId = req.RoleId,
            MapId = mapId,
            RoleInfo = ctx.Player.RoleInfo(role),
            UnlockedSavepoints = { ctx.Player.Map.UnlockedSavepoints },
            LastLogoutLocation = ctx.Player.Map.SpawnPosition(),
            CurrentSavepoint = ctx.Player.Map.Savepoint,
            BornPosType = ctx.Player.Map.BornPosType,
            TaskData = ctx.Player.Tasks.ToPlayerTaskData(),
            WorldMapId = mapId,
            UnlockedTeleportIdList = { ctx.Player.Map.UnlockedTeleports }
        };
    }
}
