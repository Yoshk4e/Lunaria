using Lunaria.Common.Tracking;
using Lunaria.Game.Logging;
using Lunaria.Game.Resources;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.Game.Player.Managers;

public sealed partial class SignInManager(SignInAssets assets, TimeProvider? timeProvider = null) : TrackedObject
{
    private static readonly ILogger Log = GameLog.Create("Lunaria.Game.Player.SignIn");

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    public SignInManager(GameData assets, TimeProvider? timeProvider = null) : this(assets.SignIn, timeProvider) { }

    // Saves without activity IDs belong to the original calendar, activity 3.
    public const uint LegacyActivityId = 3;
    private readonly TrackedSortedDictionary<uint, Calendar> __tracked_calendars = [];
    [Tracked]
    private partial TrackedSortedDictionary<uint, Calendar> _calendars { get; }

    public sealed record ActivityState(uint ActivityId, IReadOnlyList<uint> SignedDays,
        IReadOnlyList<uint> ClaimedDays, long? LastSignInDay, uint? AttendanceDays = null);

    public IEnumerable<ActivityState> Activities => _calendars.Select(pair => new ActivityState(pair.Key,
        pair.Value.Signed.ToArray(), pair.Value.Claimed.ToArray(), pair.Value.LastDay, pair.Value.AttendanceDays));

    // Lifetime attendance keeps counting after all reward slots are filled.
    public uint AttendanceDays => _calendars.GetValueOrDefault(LegacyActivityId)?.AttendanceDays ?? 0;
    public IReadOnlyCollection<uint> SignedDays => _calendars.GetValueOrDefault(LegacyActivityId)?.Signed ?? [];
    public IReadOnlyCollection<uint> ClaimedDays => _calendars.GetValueOrDefault(LegacyActivityId)?.Claimed ?? [];
    public long? LastSignInDay => _calendars.GetValueOrDefault(LegacyActivityId)?.LastDay;

    public void Load(IEnumerable<uint> signedDays, IEnumerable<uint> claimedDays, long? lastSignInDay = null) =>
        LoadActivities([new(LegacyActivityId, signedDays.ToArray(), claimedDays.ToArray(), lastSignInDay)]);

    public void LoadActivities(IEnumerable<ActivityState> activities)
    {
        _calendars.Clear();
        foreach (var saved in activities)
        {
            if (assets.Activity(saved.ActivityId) is null) continue;
            var calendar = new Calendar();
            calendar.Signed.UnionWith(saved.SignedDays.Intersect(assets.Days(saved.ActivityId)));
            calendar.Claimed.UnionWith(saved.ClaimedDays.Intersect(calendar.Signed));
            calendar.AttendanceDays = Math.Max(saved.AttendanceDays ?? 0, (uint)calendar.Signed.Count);
            // Old saves have no attendance date. Do not award another day on login.
            calendar.LastDay = saved.LastSignInDay ?? (calendar.Signed.Count > 0
                ? _time.GetUtcNow().ToUnixTimeSeconds() / 86400 : null);
            _calendars[saved.ActivityId] = calendar;
        }
        AcceptLoadedState();
    }

    public (int Result, SignInActivityData? Data) Query(uint activityId, DateTimeOffset? at = null)
    {
        var activity = assets.Activity(activityId);
        if (activity is null)
            return ((int)EnmTextCode.EnmTextSigninActivityIdInvalid, null);

        var calendar = _calendars.GetValueOrDefault(activityId) ?? new Calendar();
        var now = (at ?? _time.GetUtcNow()).ToUnixTimeSeconds();
        var open = now >= (long)activity.TimeOffsetStart && now <= (long)activity.TimeOffsetStop;
        var days = assets.Days(activityId);
        if (open && (calendar.LastDay is null || now / 86400 > calendar.LastDay))
        {
            var next = days.FirstOrDefault(day => !calendar.Signed.Contains(day));
            if (next != 0) calendar.Signed.Add(next);
            if (calendar.AttendanceDays < uint.MaxValue) calendar.AttendanceDays++;
            calendar.LastDay = now / 86400;
            _calendars[activityId] = calendar;

            Log.Stage("sign in recorded for activity {ActivityId}, reward day {RewardDay}, attendance days {AttendanceDays}, calendar day {CalendarDay}",
                activityId, next, calendar.AttendanceDays, calendar.LastDay);
        }

        var data = new SignInActivityData {
            ActivityId = activityId, StartTime = activity.TimeOffsetStart, EndTime = activity.TimeOffsetStop
        };
        foreach (var day in days)
            data.SigninDatas.Add(new DaySignInData {
                Day = day, IsSignedIn = calendar.Signed.Contains(day), HasClaimed = calendar.Claimed.Contains(day)
            });
        return (0, data);
    }

    public (int Result, IReadOnlyList<ItemGrant> Items) Claim(uint activityId, uint day)
    {
        if (assets.Activity(activityId) is null)
            return ((int)EnmTextCode.EnmTextSigninActivityIdInvalid, []);
        if (!assets.Days(activityId).Contains(day))
            return ((int)EnmTextCode.EnmTextSigninActivityRewardInvalid, []);
        if (!_calendars.TryGetValue(activityId, out var calendar)
            || !calendar.Signed.Contains(day) || !calendar.Claimed.Add(day))
            return ((int)EnmTextCode.EnmTextSigninActivityCannotClaimReward, []);

        Log.Stage("sign in reward claimed for activity {ActivityId} day {Day}", activityId, day);
        return (0, assets.Items(activityId, day));
    }

    public bool HasClaimableDay(uint activityId) => _calendars.TryGetValue(activityId, out var calendar)
        && assets.Days(activityId).Any(day => calendar.Signed.Contains(day) && !calendar.Claimed.Contains(day));

    private sealed partial class Calendar : TrackedObject
    {
        private readonly TrackedSet<uint> __trackedSigned = [];
        [Tracked]
        public partial TrackedSet<uint> Signed { get; }
        private readonly TrackedSet<uint> __trackedClaimed = [];
        [Tracked]
        public partial TrackedSet<uint> Claimed { get; }
        private long? __trackedLastDay = default!;
        [Tracked]
        public partial long? LastDay { get; set; }
        private uint __trackedAttendanceDays = default!;
        [Tracked]
        public partial uint AttendanceDays { get; set; }
    }
}
