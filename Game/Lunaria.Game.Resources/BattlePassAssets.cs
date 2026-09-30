using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

public sealed class BattlePassAssets
{
    private readonly Dictionary<(uint Pass, uint Level), PBattlePassLevel> _levels = [];
    private readonly Dictionary<uint, uint> _maxLevels = [];
    private readonly Dictionary<uint, PBattlePass> _passes = [];

    public BattlePassAssets(
        IReadOnlyDictionary<string, PBattlePass> passes,
        IReadOnlyDictionary<string, PBattlePassLevel> levels
    )
    {
        foreach (var row in passes.Values)
        {
            _passes[row.Id] = row;
        }

        foreach (var row in levels.Values)
        {
            _levels[(row.Bp, row.Level)] = row;
            _maxLevels[row.Bp] = Math.Max(_maxLevels.GetValueOrDefault(row.Bp), row.Level);
        }

        if (_passes.Count == 0)
            throw new ResourceException("P_BattlePass.json", "p_battlepass has no rows");

        foreach (var pass in _passes.Keys)
        {
            if (!_levels.ContainsKey((pass, 1)))
                throw new ResourceException("P_BattlePassLevel.json", $"battle pass {pass} has no level 1");

            for (uint level = 2; level <= _maxLevels[pass]; level++)
                if (!_levels.ContainsKey((pass, level)))
                    throw new ResourceException("P_BattlePassLevel.json", $"battle pass {pass} is missing level {level}");
        }

        foreach (var row in _levels.Values)
        {
            RewardStrings.Parse(row.Id, row.Award);
        }
    }

    public bool Exists(uint id) => _passes.ContainsKey(id);
    public uint ExpItem(uint id) => _passes.GetValueOrDefault(id)?.BpExpItem ?? 0;
    public uint MaxLevel(uint id) => _maxLevels.GetValueOrDefault(id);
    public uint ExpNeed(uint id, uint level) => _levels.GetValueOrDefault((id, level))?.ExpNeed ?? 0;

    public IReadOnlyList<ItemGrant> Award(uint id, uint level)
    {
        var row = _levels.GetValueOrDefault((id, level));
        return row is null ? [] : RewardStrings.Parse(row.Id, row.Award);
    }
}
