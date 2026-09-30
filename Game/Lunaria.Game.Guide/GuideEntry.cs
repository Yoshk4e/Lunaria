using System.Text.Json.Serialization;

namespace Lunaria.Game.Guide;

public sealed record GuideEntry
{
    /// <summary>Unlock time in Unix seconds (GuideInfo.unlock_time).</summary>
    [JsonPropertyName("unlocked_at")]
    public long UnlockedAt { get; init; }

    [JsonPropertyName("read")]
    public bool Read { get; init; }
}
