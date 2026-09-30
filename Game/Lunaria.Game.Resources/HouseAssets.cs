using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

public sealed class HouseAssets
{
    private readonly Dictionary<uint, PHouseV2Table> _houses = [];
    private readonly Dictionary<(uint Group, uint Level), PHouseLevelTable> _levels = [];
    private readonly Dictionary<uint, uint> _maxLevels = [];

    public HouseAssets(
        IReadOnlyDictionary<string, PHouseV2Table> houses,
        IReadOnlyDictionary<string, PHouseLevelTable> levels,
        IReadOnlyDictionary<string, PHouseIndustryTable> industry
    )
    {
        foreach (var row in houses.Values)
        {
            _houses[row.Id] = row;
        }

        foreach (var row in levels.Values)
        {
            _levels[(row.GroupId, row.Level)] = row;
            _maxLevels[row.GroupId] = Math.Max(_maxLevels.GetValueOrDefault(row.GroupId), row.Level);
        }

        if (_houses.Count == 0)
            throw new ResourceException("P_HouseV2Table.json", "p_housev2table has no rows");

        if (industry.Count == 0)
            throw new ResourceException("P_HouseIndustryTable.json", "p_houseindustrytable has no rows");

        Industry = industry.Values.OrderBy(row => row.Id).First();

        if (Industry.DropInterval == 0)
            throw new ResourceException("P_HouseIndustryTable.json", "income interval must be positive");

        foreach (var house in _houses.Values)
        {
            if (!_levels.ContainsKey((house.LevelGroup, 1)))
                throw new ResourceException(
                    "P_HouseLevelTable.json", $"house {house.Id} group {house.LevelGroup} has no level 1");

            for (uint level = 2; level <= _maxLevels[house.LevelGroup]; level++)
                if (!_levels.ContainsKey((house.LevelGroup, level)))
                    throw new ResourceException(
                        "P_HouseLevelTable.json", $"house group {house.LevelGroup} is missing level {level}");
        }
    }

    public PHouseIndustryTable Industry { get; }

    public IReadOnlyList<PHouseV2Table> All => _houses.Values.OrderBy(row => row.Id).ToList();

    public PHouseV2Table? Get(uint id) => _houses.GetValueOrDefault(id);
    public uint MaxLevel(uint group) => _maxLevels.GetValueOrDefault(group);
    public PHouseLevelTable? Level(uint group, uint level) => _levels.GetValueOrDefault((group, level));

    /// <summary>Matches HouseV2.OnEstimateIncome in house coins.</summary>
    public uint IncomePerInterval(uint houseId, uint level)
    {
        if (Get(houseId) is not {} house || Level(house.LevelGroup, level) is not {} row)
            return 0;

        var income = (decimal)house.BaseIncome + row.BaseIncome;

        for (var i = 0; i < Math.Min(row.PropertyList.Count, row.PropertyIncomeCoef.Count); i++)
            income += (decimal)row.PropertyList[i] * row.PropertyIncomeCoef[i] / 10000;
        return (uint)Math.Min(uint.MaxValue, decimal.Floor(income));
    }
}
