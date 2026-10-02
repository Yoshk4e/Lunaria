using System.Globalization;
using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

/// <summary>
/// Catalyst rules as the client applies them (s_CSM_PDD_GemData): a character holds up to MaxGemPerCharacter gems,
/// its n-th gem costs GemCost[n], and a team's total cost may not exceed the world level's MaxGemCost.
/// </summary>
public sealed class GemAssets
{
    private readonly Dictionary<uint, PGemTable> _gems = [];
    private readonly uint[] _costs;

    public GemAssets(IReadOnlyDictionary<string, PGemTable> gems, IReadOnlyDictionary<string, PGemGlobalConfig> config)
    {
        foreach (var row in gems.Values)
        {
            _gems[row.Id] = row;
        }

        var settings = config.Values.ToDictionary(r => r.Key, r => r.Value);

        MaxPerCharacter = settings.TryGetValue("MaxGemPerCharacter", out var max)
                          && decimal.TryParse(max, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? (int)parsed
            : 3;

        _costs = settings.TryGetValue("GemCost", out var costs)
            ? costs.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(text => uint.TryParse(text, CultureInfo.InvariantCulture, out var cost) ? cost : 0)
                .ToArray()
            : [];
    }

    public int MaxPerCharacter { get; }

    public bool Exists(uint gemId) => _gems.ContainsKey(gemId);

    public bool MeetsElementRequirements(uint gemId, IReadOnlyDictionary<uint, int> teamElements)
    {
        if (!_gems.TryGetValue(gemId, out var gem) || gem.ElementLimitType.Count != gem.ElementLimitNum.Count)
            return false;

        for (var i = 0; i < gem.ElementLimitType.Count; i++)
        {
            if (teamElements.GetValueOrDefault(gem.ElementLimitType[i]) < gem.ElementLimitNum[i])
                return false;
        }

        return true;
    }

    /// <summary>Cost of one character carrying <paramref name="count"/> gems: GemCost[1] + ... + GemCost[count].</summary>
    public uint CharacterCost(int count)
    {
        uint total = 0;

        for (var i = 0; i < count && i < _costs.Length; i++)
        {
            total += _costs[i];
        }

        return total;
    }
}
