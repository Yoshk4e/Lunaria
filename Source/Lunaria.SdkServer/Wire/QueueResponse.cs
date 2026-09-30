using System.Text.Json.Serialization;

namespace Lunaria.SdkServer.Wire;

public sealed record QueueResponse
{
    [JsonPropertyName("statuscode")]
    public int Statuscode { get; init; }

    [JsonPropertyName("result")]
    public string Result { get; init; } = "";

    [JsonPropertyName("index")]
    public ulong Index { get; init; }

    [JsonPropertyName("indexoffset")]
    public ulong Indexoffset { get; init; }

    [JsonPropertyName("waitminutes")]
    public ulong Waitminutes { get; init; }

    [JsonPropertyName("timeInterval")]
    public ulong TimeInterval { get; init; }
}
