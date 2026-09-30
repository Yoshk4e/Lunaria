using Lunaria.Game.Player.Text;
using Lunaria.GameServer.Net;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Handlers.Recv;

public sealed class HandleCheckText(ILogger<HandleCheckText> logger)
{
    [GameHandler(EClientServerCmds.CsCheckText)]
    public Task<SCCheckText> OnPacket(NetContext ctx, CSCheckText req)
    {
        var result = TextValidator.CheckText((int)req.EventType, req.Text.Span) ?? 0;

        logger.LogDebug("check text: event {EventType}, len {Length}, result {Result}",
            req.EventType, req.Text.Length, result);

        return Task.FromResult(new SCCheckText {
            Result = result,
            EventType = req.EventType,
            Text = req.Text
        });
    }
}
