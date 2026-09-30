using System.Text.Json.Serialization;

namespace Lunaria.QueryGateway;

public sealed record RegisterResponse(
    [property: JsonPropertyName("instance_id")]
    string InstanceId
);
