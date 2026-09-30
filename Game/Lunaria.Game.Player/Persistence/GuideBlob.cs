using System.Text.Json.Serialization;
using Lunaria.Game.Guide;

namespace Lunaria.Game.Player.Persistence;

/// <summary>Keep the entries wrapper so older handbook saves still load.</summary>
public sealed record GuideBlob
{
    [JsonPropertyName("entries")]
    public Dictionary<uint, GuideEntry> Entries { get; init; } = [];
}
