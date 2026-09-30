using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

public sealed class SignInAssets
{
    private readonly Dictionary<uint, PActivityTable> _activities = [];
    private readonly Dictionary<(uint Activity, uint Day), PSignInActivityRewardTable> _rewards = [];

    public SignInAssets(
        IReadOnlyDictionary<string, PActivityTable> activities,
        IReadOnlyDictionary<string, PSignInActivityRewardTable> rewards
    )
    {
        foreach (var row in activities.Values)
        {
            _activities[row.Id] = row;
        }

        foreach (var row in rewards.Values)
        {
            _rewards[(row.ActivityId, row.Day)] = row;
        }

        if (_activities.Count == 0)
            throw new ResourceException("P_ActivityTable.json", "p_activitytable has no rows");

        if (_rewards.Count == 0)
            throw new ResourceException("P_SignInActivityRewardTable.json", "p_signinactivityrewardtable has no rows");

        foreach (var row in _rewards.Values)
        {
            if (!_activities.TryGetValue(row.ActivityId, out var activity)
                || activity.TimeOffsetStart == 0
                || activity.TimeOffsetStop <= activity.TimeOffsetStart)
                throw new ResourceException(
                    "P_ActivityTable.json", $"sign-in reward {row.Id} references invalid activity {row.ActivityId}");

            RewardStrings.Parse(row.Id, row.Items);
        }
    }

    public PActivityTable? Activity(uint id) =>
        _rewards.Keys.Any(key => key.Activity == id) ? _activities.GetValueOrDefault(id) : null;

    public IReadOnlyList<uint> Days(uint activityId) =>
        _rewards.Keys.Where(key => key.Activity == activityId).Select(key => key.Day).OrderBy(day => day).ToList();

    public IReadOnlyList<ItemGrant> Items(uint activityId, uint day)
    {
        var row = _rewards.GetValueOrDefault((activityId, day));
        return row is null ? [] : RewardStrings.Parse(row.Id, row.Items);
    }
}
