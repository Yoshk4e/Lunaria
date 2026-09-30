using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lunaria.Game.Resources;

/// <summary>Server-defined rules for gaps in the recovered tables.</summary>
public sealed record GameplayPolicy
{
    public string Provenance { get; init; } = "";
    public HouseRentPolicy HouseRent { get; init; } = new();
    public WantedPolicy Wanted { get; init; } = new();

    public static GameplayPolicy Load(string path)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        GameplayPolicy policy;
        try { policy = JsonSerializer.Deserialize<GameplayPolicy>(File.ReadAllText(path), options) ?? throw new JsonException("null policy"); }
        catch (Exception ex) when (ex is IOException or JsonException)
        { throw new ResourceException(path, $"cannot load gameplay policy: {ex.Message}"); }
        if (policy.HouseRent.IntervalSeconds == 0 || policy.HouseRent.MaxIntervals == 0
            || policy.Wanted.SelectOptions is < 1 or > 16 || policy.Wanted.CreatureBaseWeight == 0)
            throw new ResourceException(path, "invalid interval, cap or selection size");
        foreach (var (id, rule) in policy.Wanted.Awards)
            if (rule.MinCount < 1 || rule.MaxCount < rule.MinCount || rule.MaxCount > 16)
                throw new ResourceException(path, $"invalid count range for award {id}");
        return policy;
    }
}

public sealed record HouseRentPolicy
{
    public uint IntervalSeconds { get; init; }
    public uint MaxIntervals { get; init; }
}

public sealed record WantedPolicy
{
    public int SelectOptions { get; init; } = 3;
    public uint CreatureBaseWeight { get; init; } = 100;
    public bool RedeemOncePerCompletedStep { get; init; } = true;
    public Dictionary<uint, uint[]> EventPools { get; init; } = [];
    public Dictionary<uint, WantedAwardRule> Awards { get; init; } = [];
}

public sealed record WantedAwardRule
{
    public int MinCount { get; init; } = 1;
    public int MaxCount { get; init; } = 1;
    public uint[] Qualities { get; init; } = [];
    public uint[] Candidates { get; init; } = [];
    public uint[] Excluded { get; init; } = [];
    public MoneyType Currency { get; init; } = MoneyType.ThoughtSand;
    public uint Amount { get; init; }
}
