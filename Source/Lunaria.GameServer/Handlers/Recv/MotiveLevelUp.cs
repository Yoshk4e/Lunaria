using Lunaria.Game.Resources;
using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleMotiveLevelUp(ILogger<HandleMotiveLevelUp> logger)
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsMotiveLeveup)]
    public async Task<SCMotiveLevelUp> OnPacket(NetContext ctx, CSMotiveLevelUp req)
    {
        SCMotiveLevelUp Reject(int code)
        {
            return new SCMotiveLevelUp {
                Result = code,
                MotiveUniqId = req.MotiveUniqId,
                OldLevel = 0,
                NewLevel = 0
            };
        }

        var items = req.Items.Select(g => new ItemGrant(g.ItemId, g.Count)).ToList();
        var feeds = req.MotiveUniqIds.ToList();

        var outcome = ctx.Player.LevelUpMotive(req.MotiveUniqId, items, feeds);

        if (!outcome.Ok)
            return Reject(outcome.Code);

        var recycleWire = outcome.Recycle
            .Select(g => new ItemIdCount { ItemId = g.ItemId, Count = g.Count })
            .ToList();

        await ctx.NotifyAsync(outcome.Delivery.Presentation).ConfigureAwait(false);

        logger.LogDebug(
            "motive {Motive} level {Old} -> {New} ({Recycle} recycle)",
            req.MotiveUniqId, outcome.OldLevel, outcome.NewLevel, recycleWire.Count);

        var res = new SCMotiveLevelUp {
            Result = 0,
            MotiveUniqId = req.MotiveUniqId,
            OldLevel = outcome.OldLevel,
            NewLevel = outcome.NewLevel
        };
        res.RecycleItems.AddRange(recycleWire);
        return res;
    }
}
