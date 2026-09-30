using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace Lunaria.QueryGateway.Controllers;

[ApiController]
public sealed class InstancesController(InstanceRegistry registry) : ControllerBase
{
    [HttpPost("/register")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    public IActionResult Register([FromBody] RegisterRequest request)
    {
        var instanceId = registry.Register(request);
        return StatusCode(StatusCodes.Status201Created, new RegisterResponse(instanceId));
    }

    [HttpPost("/heartbeat")]
    public IActionResult Heartbeat([FromBody] HeartbeatRequest request) =>
        registry.Heartbeat(request) ? Ok() : NotFound();


    [HttpGet("/allocate")]
    public IActionResult Allocate([FromQuery] string? region)
    {
        var isLoopback = HttpContext.Connection.RemoteIpAddress is {} ip
                         && IPAddress.IsLoopback(ip);

        var result = isLoopback ? registry.AllocateLocal()
            : region is null ? null : registry.Allocate(region);

        return result is {} response ? Ok(response) : NotFound(new { error = "no available instances" });
    }

    [HttpGet("/instances")]
    public IActionResult Instances() => Ok(registry.AllAvailable());

    [HttpGet("/health")]
    public IActionResult Health() => Ok();
}
