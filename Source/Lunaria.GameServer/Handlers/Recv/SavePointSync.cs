using Lunaria.GameServer.Net;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleSavePointSync
{
    [GameHandler(EClientServerCmds.CsSavepointSync)]
    public Task<SCSavePointSync> OnPacket(NetContext ctx, CSSavePointSync req) =>
        Task.FromResult(new SCSavePointSync {
            Result = ctx.Player.Map.BindSavepoint(req.SavepointId),
            SavepointId = req.SavepointId
        });
}
