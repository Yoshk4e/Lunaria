using Lunaria.SdkServer.Services;
using Lunaria.SdkServer.Wire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Lunaria.SdkServer.Controllers;

[ApiController]
public sealed class StatusController(
    IHttpClientFactory httpClientFactory,
    IOptions<SdkServerOptions> options,
    QueueState queue,
    LastLoginTracker lastLogin,
    HotupdateStore hotupdate,
    ILogger<StatusController> logger
) : ControllerBase
{

    [HttpGet("/queryversionres")]
    public IActionResult QueryVersionRes([FromQuery] string? ver)
    {
        if (hotupdate.TryReadManifest(out var scanned, out var source))
        {
            logger.LogDebug("serving hotupdate manifest from {Source}", source);
            return File(scanned, "application/json");
        }

        var legacy = options.Value.HotupdateManifestPath;

        foreach (var candidate in new[] { legacy, Path.Combine(AppContext.BaseDirectory, legacy) })
        {
            if (System.IO.File.Exists(candidate))
                return File(System.IO.File.ReadAllBytes(candidate), "application/json");
        }

        var target = string.IsNullOrWhiteSpace(ver) ? "0.09.70.4" : ver;
        logger.LogDebug("no hotupdate manifest on disk, returning version-pass stub for ver {Ver}", target);

        return Ok(new {
            ReqResult = "OK",
            ErrorCode = "",
            Ext = "",
            Data = new[] {
                new { ver = target, addr = "", totalsize = 0, fileinfo = Array.Empty<object>() }
            }
        });
    }

    [HttpGet("/hotupdate/{**path}")]
    public IActionResult HotupdateFile(string? path)
    {
        var full = hotupdate.Resolve(path);

        if (full is null)
            return NotFound();

        return PhysicalFile(full, "application/octet-stream", enableRangeProcessing: true);
    }

    [HttpGet("/AddQueue")]
    public IActionResult AddQueue()
    {
        var (index, indexoffset, waitMinutes, interval) = queue.NextPosition();

        return Ok(new QueueResponse {
            Statuscode = waitMinutes == 0 ? 0 : 1,
            Result = "success",
            Index = index,
            Indexoffset = indexoffset,
            Waitminutes = waitMinutes,
            TimeInterval = interval
        });
    }


    [HttpGet("/GetIndex")]
    public IActionResult GetIndex()
    {
        var (index, indexoffset, waitMinutes, interval) = queue.CurrentPosition();

        return Ok(new QueueResponse {
            Statuscode = 0,
            Result = "success",
            Index = index,
            Indexoffset = indexoffset,
            Waitminutes = waitMinutes,
            TimeInterval = interval
        });
    }

    [HttpGet("/CleanPlayer")]
    public IActionResult CleanPlayer() =>
        Ok(new QueueResponse {
            Statuscode = 0,
            Result = "success",
            Index = 0,
            Indexoffset = 0,
            Waitminutes = 0,
            TimeInterval = 5
        });


    [HttpGet("/collect")]
    [HttpPost("/collect")]
    public IActionResult Collect() => Ok(new { code = 0 });

    [HttpGet("/server_info")]
    public async Task<IActionResult> ServerInfo()
    {
        var gatewayUrl = options.Value.GatewayUrl.TrimEnd('/');
        List<GatewayInstance> instances = [];

        try
        {
            using var client = httpClientFactory.CreateClient("gateway");

            instances = await client.GetFromJsonAsync<List<GatewayInstance>>($"{gatewayUrl}/instances")
                        ?? [];
        }
        catch (Exception ex)
        {
            logger.LogInformation("failed to fetch instances from gateway: {Message}", ex.Message);
        }

        logger.LogInformation("server info response from gateway: {Count} entries", instances.Count);

        var serverInfo = instances.Select(inst => {
            var addrs = inst.Addrs.Count == 0 ?
                [new ServerAddr { Ip = inst.Ip, Port = inst.Port }] :
                inst.Addrs.Select(a => new ServerAddr { Ip = a.Ip, Port = a.Port }).ToList();

            return new ServerInfoEntry {
                Id = 0,
                WorldId = 0,
                Name = $"{inst.Ip}:{inst.Port}",
                Addr = new ServerAddr { Ip = inst.Ip, Port = inst.Port },
                Addrs = addrs,
                Branch = "0.09",
                Private = 0,
                Tag = "Release",
                Status = inst.State is "Ready" or "Allocated" ? 1 : 0
            };
        }).ToList();

        return Ok(new ServerInfoResponse {
            Code = 200,
            ErrorMsg = "",
            ServerInfo = serverInfo,
            AuthInfo = new AuthInfo { Phone = "", Mail = lastLogin.Email }
        });
    }


    // ReSharper disable PropertyCanBeMadeGetOnly.Global
    // ReSharper disable PropertyCanBeMadeGetOnly.Local

    /// <summary>
    /// Keep setters for JSON deserialization. Empty state or IP values cause maintenance status or an invalid host.
    /// </summary>
    private sealed record GatewayInstance
    {
        public string Id { get; init; } = "";
        public string Region { get; init; } = "";
        public string Ip { get; init; } = "";
        public int Port { get; init; }
        public List<GatewayAddr> Addrs { get; init; } = [];
        public string State { get; init; } = "";
        public int PlayerCount { get; init; }
        public int MaxPlayers { get; init; }
    }

    private sealed record GatewayAddr
    {
        public string Ip { get; init; } = "";
        public int Port { get; init; }
    }

    // ReSharper restore PropertyCanBeMadeGetOnly.Local
    // ReSharper restore PropertyCanBeMadeGetOnly.Global
}
