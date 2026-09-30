using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Lunaria.GameServer.Net;

public sealed class UdpSender(UdpClient socket, ILogger logger)
{
    private readonly SemaphoreSlim _gate = new(initialCount: 1, maxCount: 1);

    public async Task SendAsync(byte[] datagram, IPEndPoint peer)
    {
        await _gate.WaitAsync().ConfigureAwait(false);

        try
        {
            await socket.SendAsync(datagram, peer).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
            logger.LogDebug("UDP send to {Peer} failed: {Message}", peer, ex.Message);
        }
        finally
        {
            _gate.Release();
        }
    }
}
