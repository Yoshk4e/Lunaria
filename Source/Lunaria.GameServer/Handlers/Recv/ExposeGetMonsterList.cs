using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleExposeGetMonsterList
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsExposeGetMonsterList)]
    public Task<SCExposeGetMonsterList> OnPacket(NetContext ctx, CSExposeGetMonsterList req) =>
        Task.FromResult(ctx.Player.Expose.MonsterList(req.Subregion));
}
