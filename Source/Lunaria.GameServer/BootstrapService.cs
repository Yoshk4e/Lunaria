using System.Net;
using System.Net.Sockets;
using Lunaria.Game.Player.Persistence;
using Lunaria.GameServer.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lunaria.GameServer;

public sealed class BootstrapService(IServiceProvider provider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = provider.GetRequiredService<IOptions<GameServerOptions>>().Value;
        var logger = provider.GetRequiredService<ILogger<BootstrapService>>();
        var factory = provider.GetRequiredService<IDbContextFactory<GameDbContext>>();

        await using (var db = await factory.CreateDbContextAsync(stoppingToken).ConfigureAwait(false))
        {
            await db.Database.MigrateAsync(stoppingToken).ConfigureAwait(false);
            var connection = db.Database.GetDbConnection();
            await connection.OpenAsync(stoppingToken).ConfigureAwait(false);
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode = WAL;";
            await command.ExecuteScalarAsync(stoppingToken).ConfigureAwait(false);
            logger.LogInformation("game database ready at {Path}", options.DatabasePath);
        }

        var session = provider.GetRequiredService<ClientSession>();
        var gate = provider.GetRequiredService<ConnectionGate>();
        var udp = provider.GetRequiredService<UdpListenerService>();
        var runtime = provider.GetRequiredService<GameServerRuntime>();

        _ = Task.Run(() => udp.RunListenerAsync(options.Port, stoppingToken), stoppingToken);
        _ = Task.Run(() => udp.RunListenerAsync(options.ScenePort, stoppingToken), stoppingToken);

        var listeners = new List<TcpListener>();

        foreach (var port in new[] { options.Port, options.ScenePort })
        {
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start(options.ListenBacklog);
            listeners.Add(listener);

            logger.LogInformation(
                "listening on 0.0.0.0:{Port} (backlog {Backlog}, {Loops} accept loops, capacity {Capacity})",
                port, options.ListenBacklog, options.AcceptLoops, options.MaxPlayers);

            for (var i = 0; i < Math.Max(val1: 1, options.AcceptLoops); i++)
                _ = AcceptLoopAsync(listener, session, gate, logger, stoppingToken);
        }

        logger.LogInformation("game server ready (next session id {SessionId})", runtime.AllocateSessionId());

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            foreach (var listener in listeners)
            {
                listener.Stop();
            }
            logger.LogInformation("game server stopped");
        }
    }

    private static async Task AcceptLoopAsync(
        TcpListener listener,
        ClientSession session,
        ConnectionGate gate,
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;

            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (ex is SocketException or ObjectDisposedException or InvalidOperationException)
            {

                if (cancellationToken.IsCancellationRequested)
                    return;

                continue;
            }

            if (!gate.TryEnter())
            {
                client.Close();
                continue;
            }

            _ = Task.Run(() => session.HandleConnectionAsync(client, cancellationToken), CancellationToken.None);
        }
    }
}
