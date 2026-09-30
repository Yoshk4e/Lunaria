using System.Text.Json.Serialization;

namespace Lunaria.SdkServer.Wire;

public sealed record ExtraParamsEmail
{
    [JsonPropertyName("account")]
    public string Account { get; init; } = "";

    [JsonPropertyName("password")]
    public string Password { get; init; } = "";
}
