using System.Text.Json.Serialization;

namespace Lunaria.GameServer.Gateway;

public sealed record RegisterResponse
{
    [JsonPropertyName("instance_id")]
    public required string InstanceId { get; init; }
}
