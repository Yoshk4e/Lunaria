using System.Net.Http.Json;
using Lunaria.GameServer.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lunaria.GameServer.Gateway;

public sealed class GatewayRegistrationService(
    IHttpClientFactory httpClientFactory,
    GameServerOptions options,
    ConnectionGate gate,
    ILogger<GatewayRegistrationService> logger
) : BackgroundService
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var request = new RegisterRequest {
            Region = options.ServerRegion,
            Ip = options.ServerIp,
            Port = options.Port,
            Addrs = [
                new ServerAddress { Ip = options.ServerIp, Port = options.Port },
                new ServerAddress { Ip = options.ServerIp, Port = options.ScenePort }
            ],
            MaxPlayers = options.MaxPlayers
        };

        string instanceId;

        using (var client = httpClientFactory.CreateClient("gateway"))
        {
            instanceId = await RegisterAsync(client, request, stoppingToken).ConfigureAwait(false)
                         ?? throw new InvalidOperationException(
                             $"failed to register with gateway {options.GatewayUrl}");
        }

        logger.LogInformation("registered with gateway: instance {InstanceId}, port {Port}, region {Region}",
            instanceId, options.Port, options.ServerRegion);

        using var timer = new PeriodicTimer(HeartbeatInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await BeatAsync(instanceId, "Ready", stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }

        await BeatAsync(instanceId, "Shutdown", CancellationToken.None).ConfigureAwait(false);
        logger.LogInformation("game server instance {InstanceId} stopped", instanceId);
    }

    private async Task<string?> RegisterAsync(HttpClient client, RegisterRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.PostAsJsonAsync($"{options.GatewayUrl.TrimEnd('/')}/register", request, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<RegisterResponse>(cancellationToken)
                .ConfigureAwait(false);
            return body?.InstanceId;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "gateway registration failed: {Url}", options.GatewayUrl);
            return null;
        }
    }

    private async Task BeatAsync(string instanceId, string state, CancellationToken cancellationToken)
    {
        var request = new HeartbeatRequest {
            InstanceId = instanceId,
            State = state,
            PlayerCount = gate.Sessions
        };

        var client = httpClientFactory.CreateClient("gateway");

        try
        {
            var response = await client.PostAsJsonAsync($"{options.GatewayUrl.TrimEnd('/')}/heartbeat", request, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning("heartbeat failed: {Message}", ex.Message);
        }
    }
}
