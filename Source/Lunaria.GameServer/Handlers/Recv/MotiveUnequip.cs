using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleMotiveUnequip(ILogger<HandleMotiveUnequip> logger)
{
    [GameHandler(EClientServerCmds.CsMotiveUnequip)]
    public Task<SCMotiveUnequip> OnPacket(NetContext ctx, CSMotiveUnequip req)
    {
        SCMotiveUnequip Reject(int code)
        {
            return new SCMotiveUnequip {
                Result = code,
                MotiveUniqId = req.MotiveUniqId,
                InstId = req.InstId
            };
        }

        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(Reject((int)EnmTextCode.EnmTextNotAccLogin));

        var code = ctx.Player.UnequipMotive(req.MotiveUniqId, req.InstId);

        if (code != 0)
            return Task.FromResult(Reject(code));

        logger.LogDebug("motive {Motive} unequipped from character {Inst}", req.MotiveUniqId, req.InstId);

        return Task.FromResult(new SCMotiveUnequip {
            Result = 0,
            MotiveUniqId = req.MotiveUniqId,
            InstId = req.InstId
        });
    }
}
