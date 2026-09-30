using System.Text.Json.Serialization;

namespace Lunaria.SdkServer.Wire;

public sealed record AccountCheckResult
{
    [JsonPropertyName("accountStatus")]
    public int AccountStatus { get; init; }

    [JsonPropertyName("firstBinding")]
    public bool FirstBinding { get; init; }

    [JsonPropertyName("matching")]
    public bool Matching { get; init; }
}
