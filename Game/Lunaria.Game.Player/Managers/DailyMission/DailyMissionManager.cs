using Lunaria.Common.Tracking;
using Lunaria.Game.Logging;
using Lunaria.Game.Resources.Tables;
using Lunaria.Game.Resources;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.Game.Player.Managers;

public sealed record DailyMissionState(uint MissionId, uint Progress, bool Claimed);

public sealed partial class DailyMissionManager(GameData assets, TimeProvider? timeProvider = null) : TrackedObject
{
    private static readonly ILogger Log = GameLog.Create("Lunaria.Game.Player.DailyMission");

    private readonly TrackedSet<uint> __tracked_claimedRewards = [];
    [Tracked]
    private partial TrackedSet<uint> _claimedRewards { get; }
    private readonly TrackedDictionary<uint, DailyMissionState> __tracked_missions = [];
    [Tracked]
    private partial TrackedDictionary<uint, DailyMissionState> _missions { get; }
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private DateTimeOffset __tracked_dayAnchor = DateTimeOffset.UnixEpoch;
    [Tracked]
    private partial DateTimeOffset _dayAnchor { get; set; }

    public IReadOnlyDictionary<uint, DailyMissionState> Missions => _missions;

    public DateTimeOffset DayAnchor => _dayAnchor;

    public IReadOnlyCollection<uint> ClaimedRewards => _claimedRewards;

    private uint __trackedActivePoint = default!;
    [Tracked]
    public partial uint ActivePoint { get; private set; }

    public void Load(
        long dayAnchor,
        IEnumerable<(uint MissionId, uint Progress, bool Claimed)> persisted,
        uint activePoint,
        IEnumerable<uint> claimedRewards
    )
    {
        _missions.Clear();
        _claimedRewards.Clear();
        _dayAnchor = DateTimeOffset.FromUnixTimeSeconds(dayAnchor);
        ActivePoint = activePoint;

        foreach (var row in persisted)
        {
            var mission = assets.DailyMissions.Mission(row.MissionId);

            if (mission is null)
                continue;

            _missions[row.MissionId] = new DailyMissionState(
                row.MissionId, Math.Min(row.Progress, assets.DailyMissions.NeedCount(mission.Event)), row.Claimed);
        }

        foreach (var rewardId in claimedRewards)
        {
            if (assets.DailyMissions.Rewards.Any(reward => reward.Id == rewardId))
                _claimedRewards.Add(rewardId);
        }

        AcceptLoadedState();
        RollDay(_time.GetUtcNow());
    }

    public DailyMissionData ToDailyMissionData(DateTimeOffset? now = null)
    {
        RollDay(now ?? _time.GetUtcNow());
        var data = new DailyMissionData { ActivePoint = ActivePoint };
        var list = new DailyMissionListData();

        foreach (var mission in assets.DailyMissions.Missions)
        {
            var state = _missions.GetValueOrDefault(mission.Id);

            list.MissionItems.Add(new DailyMissionItem {
                MissionId = mission.Id,
                Progress = state?.Progress ?? 0,
                IsClaimed = state?.Claimed ?? false
            });
        }
        data.MissionListData = list;
        var rewards = new DailyMissionRewardData();
        rewards.ClaimedRewardids.AddRange(_claimedRewards);
        data.RewardData = rewards;
        return data;
    }

    public (int Result, DailyMissionItem? Item, uint ActivePoint) ClaimActivePoint(uint missionId)
    {
        RollDay(_time.GetUtcNow());

        var mission = assets.DailyMissions.Mission(missionId);

        if (mission is null)
            return ((int)EnmTextCode.EnmTextDailyMissionIdInvalid, null, ActivePoint);

        var state = _missions.GetValueOrDefault(missionId);

        if (state is null || state.Progress < assets.DailyMissions.NeedCount(mission.Event))
            return ((int)EnmTextCode.EnmTextDailyMissionIdNotFinish, ToItem(missionId, state), ActivePoint);

        if (state.Claimed)
            return ((int)EnmTextCode.EnmTextDailyMissionIdHasClaimed, ToItem(missionId, state), ActivePoint);

        state = new DailyMissionState(missionId, state.Progress, Claimed: true);
        _missions[missionId] = state;
        ActivePoint = (uint)Math.Min((ulong)ActivePoint + mission.ActivePoint, uint.MaxValue);

        Log.Stage("daily mission {MissionId} claimed for {AddedPoints} points, total active points {ActivePoint}", missionId, mission.ActivePoint, ActivePoint);

        return (0, ToItem(missionId, state), ActivePoint);
    }

    public IReadOnlyList<PDailyMissionRewardTable> EligibleRewards()
    {
        RollDay(_time.GetUtcNow());

        return assets.DailyMissions.Rewards
            .Where(reward => reward.NeedActivePoint <= ActivePoint && !_claimedRewards.Contains(reward.Id))
            .ToList();
    }

    public void MarkRewardClaimed(uint rewardId)
    {
        if (_claimedRewards.Add(rewardId))
        {

            Log.Stage("daily mission reward {RewardId} claim recorded at {ActivePoint} points", rewardId, ActivePoint);
        }
    }

    public bool AddEventProgress(uint eventId, uint count)
    {
        // Publish daily resets even when the event matches no mission.
        var changed = RollDay(_time.GetUtcNow());
        if (count == 0) return changed;

        foreach (var mission in assets.DailyMissions.Missions)
        {
            if (mission.Event != eventId)
                continue;

            var state = _missions.GetValueOrDefault(mission.Id);
            var threshold = assets.DailyMissions.NeedCount(mission.Event);
            var progress = (uint)Math.Min((ulong)(state?.Progress ?? 0) + count, threshold);

            if (state is not null && state.Progress == progress)
                continue;

            _missions[mission.Id] = new DailyMissionState(mission.Id, progress, state?.Claimed ?? false);
            changed = true;
        }

        return changed;
    }

    private DailyMissionItem ToItem(uint missionId, DailyMissionState? state)
    {
        var mission = assets.DailyMissions.Mission(missionId)!;

        return new DailyMissionItem {
            MissionId = missionId,
            Progress = state?.Progress ?? 0,
            IsClaimed = state?.Claimed ?? false
        };
    }

    private bool RollDay(DateTimeOffset now)
    {
        if (_dayAnchor == DateTimeOffset.UnixEpoch)
        {
            _dayAnchor = now;

            return true;
        }

        if (now.UtcDateTime.Date <= _dayAnchor.UtcDateTime.Date)
            return false;

        Log.Stage("daily missions reset from {PreviousDay} to {Day}, clearing {ActivePoint} points and {ClaimCount} reward claims", _dayAnchor, now, ActivePoint, _claimedRewards.Count);
        _dayAnchor = now;
        _missions.Clear();
        _claimedRewards.Clear();
        ActivePoint = 0;

        return true;
    }

    public bool AdvanceTime(DateTimeOffset now)
    {
        return RollDay(now);
    }

}
