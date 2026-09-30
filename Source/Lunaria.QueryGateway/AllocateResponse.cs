using System.Text.Json.Serialization;

namespace Lunaria.QueryGateway;

public sealed record AllocateResponse(
    [property: JsonPropertyName("instance_id")]
    string InstanceId,
    string Ip,
    int Port,
    List<ServerAddr> Addrs
);
