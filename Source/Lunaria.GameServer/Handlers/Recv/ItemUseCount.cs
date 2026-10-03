using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;
public sealed class HandleItemUseCount
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsItemUseCount)]
    public async Task<SCItemUseCount> OnPacket(NetContext ctx, CSItemUseCount req)
    {
        SCItemUseCount Reject(int code)
        {
            return new SCItemUseCount {
                Result = code,
                ItemId = req.ItemId,
                Success = 0,
                Total = ctx.Player.InventoryCount(req.ItemId)
            };
        }

        var outcome = ctx.Player.UseItem(req.ItemId, req.ItemNum, req.Params);

        if (outcome.Code != 0)
            return Reject(outcome.Code);

        return new SCItemUseCount {
            Result = 0,
            ItemId = req.ItemId,
            Success = outcome.Used,
            Total = outcome.Remaining
        };
    }
}
