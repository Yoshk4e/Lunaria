using Lunaria.Game.Resources;
using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleCharacterLevelUp
{
    [GameHandler(EClientServerCmds.CsReqCharacterLevelUp)]
    public async Task<SCCharacterLevelUp> OnPacket(NetContext ctx, CSCharacterLevelUp req)
    {
        SCCharacterLevelUp Reject(int code)
        {
            return new SCCharacterLevelUp {
                Result = code,
                CurrentData = ctx.Player.Characters.Get(req.InstId) is {} before ? ctx.Player.Characters.ToCharacterData(before) : null
            };
        }

        if (!ctx.Player.HasActiveRole)
            return Reject((int)EnmTextCode.EnmTextNotAccLogin);

        var items = req.Items
            .Where(item => item.Count > 0)
            .Select(item => new ItemGrant(item.ItemId, item.Count))
            .ToList();

        var outcome = ctx.Player.LevelUpCharacter(req.InstId, items);

        if (outcome.Code != 0)
            return Reject(outcome.Code);

        var after = ctx.Player.Characters.Get(req.InstId);

        return new SCCharacterLevelUp {
            Result = 0,
            CurrentData = after is not null ? ctx.Player.Characters.ToCharacterData(after) : null
        };
    }
}
