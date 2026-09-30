using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

public sealed class DailyMissionAssets
{
    private readonly Dictionary<uint, PGlobalEventFinishTable> _events = [];
    private readonly Dictionary<uint, PDailyMissionTable> _missions = [];
    private readonly Dictionary<uint, PDailyMissionRewardTable> _rewards = [];

    public DailyMissionAssets(
        IReadOnlyDictionary<string, PDailyMissionTable> missions,
        IReadOnlyDictionary<string, PDailyMissionRewardTable> rewards,
        IReadOnlyDictionary<string, PGlobalEventFinishTable> events
    )
    {
        foreach (var row in missions.Values)
        {
            _missions[row.Id] = row;
        }

        foreach (var row in rewards.Values)
        {
            _rewards[row.Id] = row;
        }

        foreach (var row in events.Values)
        {
            _events[row.Id] = row;
        }

        if (_missions.Count == 0)
            throw new ResourceException("P_DailyMissionTable.json", "p_dailymissiontable has no rows");

        if (_rewards.Count == 0)
            throw new ResourceException("P_DailyMissionRewardTable.json", "p_dailymissionrewardtable has no rows");

        foreach (var mission in _missions.Values)
        {
            if (!_events.TryGetValue(mission.Event, out var @event) || @event.NeedCount == 0)
                throw new ResourceException(
                    "P_GlobalEventFinishTable.json", $"daily mission {mission.Id} references invalid event {mission.Event}");
        }

        foreach (var reward in _rewards.Values)
        {
            RewardStrings.Parse(reward.Id, reward.Items);
        }
    }

    public IReadOnlyList<PDailyMissionTable> Missions => _missions.Values.OrderBy(row => row.Id).ToList();
    public IReadOnlyList<PDailyMissionRewardTable> Rewards => _rewards.Values.OrderBy(row => row.NeedActivePoint).ToList();

    public PDailyMissionTable? Mission(uint id) => _missions.GetValueOrDefault(id);
    public uint NeedCount(uint eventId) => _events.GetValueOrDefault(eventId)?.NeedCount ?? 0;

    public IReadOnlyList<ItemGrant> RewardItems(uint rewardId)
    {
        var row = _rewards.GetValueOrDefault(rewardId);
        return row is null ? [] : RewardStrings.Parse(row.Id, row.Items);
    }
}
