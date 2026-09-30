using System.Collections.Frozen;
using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

/// <summary>
/// Character rows price reaching a level and team rows price leaving it. Both APIs return the cost to advance.
/// </summary>
public sealed class ProgressionAssets
{
    private readonly FrozenDictionary<uint, PLevelUpExpTable> _characterLevels;
    private readonly FrozenDictionary<uint, PTeamLevelTable> _teamLevels;

    private readonly PWorldLevelTable[] _worldLevels;

    public ProgressionAssets(
        IReadOnlyDictionary<string, PLevelUpExpTable> characterLevels,
        IReadOnlyDictionary<string, PTeamLevelTable> teamLevels,
        IReadOnlyDictionary<string, PWorldLevelTable> worldLevels
    )
    {
        var characterLevelMap = new Dictionary<uint, PLevelUpExpTable>();

        foreach (var row in characterLevels.Values)
        {
            characterLevelMap[row.Id] = row;
        }

        _characterLevels = characterLevelMap.ToFrozenDictionary();

        var teamLevelMap = new Dictionary<uint, PTeamLevelTable>();

        foreach (var row in teamLevels.Values)
        {
            teamLevelMap[row.Id] = row;
        }

        _teamLevels = teamLevelMap.ToFrozenDictionary();

        if (_characterLevels.Count == 0)
            throw new ResourceException("P_LevelUpExpTable.json", "p_levelupexptable has no rows");

        if (_teamLevels.Count == 0)
            throw new ResourceException("P_TeamLevelTable.json", "p_teamleveltable has no rows");

        _worldLevels = worldLevels.Values.OrderBy(r => r.RequireTeamLevel).ThenBy(r => r.Id).ToArray();

        if (_worldLevels.Length == 0)
            throw new ResourceException("P_WorldLevelTable.json", "p_worldleveltable has no rows");

        CharacterLevelLadderMax = _characterLevels.Keys.Max();
        TeamLevelLadderMax = _teamLevels.Keys.Max();
    }

    public uint CharacterLevelLadderMax { get; }

    public uint TeamLevelLadderMax { get; }

    public uint? CharacterExpToAdvance(uint level) =>
        _characterLevels.TryGetValue(level + 1, out var next) ? next.Experience : null;

    public uint CharacterStarRequired(uint level) =>
        _characterLevels.TryGetValue(level, out var row) ? row.StarRequired : 0;

    public uint? TeamExpToAdvance(uint level) =>
        _teamLevels.TryGetValue(level, out var row) && row.RequireTeamExp > 0 ? row.RequireTeamExp : null;

    public uint TeamExpCap(uint level) =>
        _teamLevels.TryGetValue(level, out var row) ? row.MaximumExp : 0;

    public uint WorldLevelFor(uint teamLevel, Func<uint, bool>? taskFinished = null)
    {
        var world = _worldLevels[0].Id;

        foreach (var tier in _worldLevels)
        {
            if (tier.RequireTeamLevel <= teamLevel
                && (tier.RequestTaskId == 0 || (taskFinished?.Invoke(tier.RequestTaskId) ?? true)))
                world = tier.Id;
        }
        return world;
    }

    public uint TeamLevelCeiling(uint worldLevel)
    {
        var tier = _worldLevels.FirstOrDefault(r => r.Id == worldLevel) ?? _worldLevels[0];
        return Math.Min(tier.MaxTeamLevel, TeamLevelLadderMax);
    }
}
