using System.Diagnostics;
using Lunaria.Game.Player;
using Microsoft.Extensions.Logging;

namespace Lunaria.GameServer.Net;

public sealed partial class ClientSession
{
    private async Task FlushAsync(Player player)
    {
        if (!player.IsDirty) return;
        var started = Stopwatch.GetTimestamp();
        try
        {
            await roleStore.SaveAsync(player).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "role snapshot flush failed, retaining dirty state for retry");
        }
        finally
        {
            metrics.FlushCompleted(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}
