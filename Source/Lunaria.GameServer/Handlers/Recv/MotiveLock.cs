using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleMotiveLock(ILogger<HandleMotiveLock> logger)
{
    [RequireLogin]
    [GameHandler(EClientServerCmds.CsMotiveLock)]
    public Task<SCMotiveLock> OnPacket(NetContext ctx, CSMotiveLock req)
    {
        SCMotiveLock Reject(int code)
        {
            return new SCMotiveLock {
                Result = code,
                MotiveUniqId = req.MotiveUniqId,
                LockState = req.LockState
            };
        }

        var code = ctx.Player.SetMotiveLock(req.MotiveUniqId, req.LockState);

        if (code != 0)
            return Task.FromResult(Reject(code));

        logger.LogDebug("motive {Motive} lock {State}", req.MotiveUniqId, req.LockState);

        return Task.FromResult(new SCMotiveLock {
            Result = 0,
            MotiveUniqId = req.MotiveUniqId,
            LockState = req.LockState
        });
    }
}
