using System.Text.Json.Serialization;

namespace Lunaria.SdkServer.Wire;

public sealed record ServerInfoEntry
{
    public int Id { get; init; }

    [JsonPropertyName("world_id")]
    public int WorldId { get; init; }

    public required string Name { get; init; }
    public required ServerAddr Addr { get; init; }
    public required List<ServerAddr> Addrs { get; init; }
    public required string Branch { get; init; }
    public int Private { get; init; }
    public required string Tag { get; init; }
    public int Status { get; init; }
}
