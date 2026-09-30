using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleHouseUpgrade
{
    [GameHandler(EClientServerCmds.CsReqUpgradeHouse)]
    public async Task<SCResUpgradeHouse> OnPacket(NetContext ctx, CSReqUpgradeHouse req)
    {
        if (!ctx.Player.HasActiveRole)
            return new SCResUpgradeHouse { Result = (int)EnmTextCode.EnmTextNotAccLogin };

        var (code, info) = ctx.Player.UpgradeHouse(req.HouseId);

        if (code != 0)
            return new SCResUpgradeHouse { Result = (uint)code };

        return new SCResUpgradeHouse { Result = 0, HouseInfo = info };
    }
}
