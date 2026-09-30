using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleCharacterLevelBreak
{
    [GameHandler(EClientServerCmds.CsCharacterLevelBreak)]
    public async Task<SCCharacterLevelBreak> OnPacket(NetContext ctx, CSCharacterLevelBreak req)
    {
        SCCharacterLevelBreak Reject(int code)
        {
            return new SCCharacterLevelBreak {
                Result = code,
                CurrentData = ctx.Player.Characters.Get(req.InstId) is {} before ? ctx.Player.Characters.ToCharacterData(before) : null
            };
        }

        if (!ctx.Player.HasActiveRole)
            return Reject((int)EnmTextCode.EnmTextNotAccLogin);

        var code = ctx.Player.BreakCharacter(req.InstId);

        if (code != 0)
            return Reject(code);

        return new SCCharacterLevelBreak {
            Result = 0,
            CurrentData = ctx.Player.Characters.ToCharacterData(ctx.Player.Characters.Get(req.InstId)!)
        };
    }
}
