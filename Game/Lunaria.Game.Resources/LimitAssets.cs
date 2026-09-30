using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

public sealed class LimitAssets
{
    private readonly Dictionary<uint, PLimitGroupTable> _groups = [];
    private readonly Dictionary<uint, RefreshPeriod> _periods = [];

    public LimitAssets(
        IReadOnlyDictionary<string, PLimitGroupTable> groups,
        IReadOnlyDictionary<string, PRefreshConfigTable> refreshes
    )
    {
        foreach (var row in refreshes.Values)
        {
            _periods[row.Id] = new RefreshPeriod(row.Id, row.RefreshType, row.Param01, row.Param02);
        }

        foreach (var row in groups.Values)
        {
            _groups[row.Id] = row;

            if (row.RewardLimitRefreshConfigId != 0 && !_periods.ContainsKey(row.RewardLimitRefreshConfigId))
                DanglingRefreshConfigs++;
        }

        if (_groups.Count == 0)
            throw new ResourceException("P_LimitGroupTable.json", "p_limitgrouptable has no rows");
    }

    public int DanglingRefreshConfigs { get; }

    public int GroupCount => _groups.Count;

    public bool GroupExists(uint group) => _groups.ContainsKey(group);

    public uint? Cap(uint group)
    {
        if (!_groups.TryGetValue(group, out var row) || row.RewardLimit == 0)
            return null;

        return row.RewardLimit;
    }

    /// <summary>Quotas never reset if their refresh configuration is missing.</summary>
    public RefreshPeriod Period(uint group)
    {
        if (!_groups.TryGetValue(group, out var row) || row.RewardLimitRefreshConfigId == 0)
            return RefreshPeriod.None;

        return _periods.GetValueOrDefault(row.RewardLimitRefreshConfigId) ?? RefreshPeriod.None;
    }

    public RefreshPeriod PeriodById(uint refreshConfigId) =>
        _periods.GetValueOrDefault(refreshConfigId) ?? RefreshPeriod.None;
}
