using System.Threading.Channels;

namespace Lunaria.GameServer.Net;

internal static class SessionChannelExtensions
{
    public static async ValueTask<bool> TryWriteAsync<T>(this ChannelWriter<T> writer, T item)
    {
        try
        {
            await writer.WriteAsync(item).ConfigureAwait(false);
            return true;
        }
        catch (ChannelClosedException)
        {
            return false;
        }
    }
}
