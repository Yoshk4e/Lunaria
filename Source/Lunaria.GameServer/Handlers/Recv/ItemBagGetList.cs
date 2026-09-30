using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;


public sealed class HandleItemBagGetList(ILogger<HandleItemBagGetList> logger)
{
    [GameHandler(EClientServerCmds.CsItemBagGetList)]
    public Task<SCItemBagGetList> OnPacket(NetContext ctx, CSItemBagGetList req)
    {
        var items = ctx.Player.InventoryItems();
        logger.LogDebug("item bag list: {Cells} cells of {Cap} cap", items.Count, ctx.Player.Bag.CellCap);

        return Task.FromResult(new SCItemBagGetList {
            Items = { items },
            Result = 0,
            ItemCds = {
                ctx.Player.Cooldowns.Active().Select(cd => new CmdItemCD {
                    Type = cd.CdType,
                    CdTime = (uint)cd.ReadyUnix
                })
            }
        });
    }
}
