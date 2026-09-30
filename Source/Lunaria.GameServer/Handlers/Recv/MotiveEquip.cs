using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleMotiveEquip(ILogger<HandleMotiveEquip> logger)
{
    [GameHandler(EClientServerCmds.CsMotiveEquip)]
    public Task<SCMotiveEquip> OnPacket(NetContext ctx, CSMotiveEquip req)
    {
        SCMotiveEquip Reject(int code)
        {
            return new SCMotiveEquip {
                Result = code,
                MotiveUniqId = req.MotiveUniqId,
                InstId = req.InstId
            };
        }

        if (!ctx.Player.HasActiveRole)
            return Task.FromResult(Reject((int)EnmTextCode.EnmTextNotAccLogin));

        var code = ctx.Player.EquipMotive(req.MotiveUniqId, req.InstId);

        if (code != 0)
            return Task.FromResult(Reject(code));

        logger.LogDebug("motive {Motive} equipped on character {Inst}", req.MotiveUniqId, req.InstId);

        return Task.FromResult(new SCMotiveEquip {
            Result = 0,
            MotiveUniqId = req.MotiveUniqId,
            InstId = req.InstId
        });
    }
}
