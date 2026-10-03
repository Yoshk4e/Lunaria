using Lunaria.Common.Tracking;
using Lunaria.Game.Logging;
using Lunaria.Game.Resources;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.Game.Player.Managers;

public sealed record FinishEventState(uint EventId, ulong Progress, bool Finish);

public sealed partial class AchievementManager(GameData assets, Random? random = null) : TrackedObject
{
    private static readonly ILogger Log = GameLog.Create("Lunaria.Game.Player.Achievement");

    private readonly Random _random = random ?? new Random();
    private readonly TrackedSet<uint> __tracked_claimed = [];
    [Tracked]
    private partial TrackedSet<uint> _claimed { get; }
    private readonly TrackedDictionary<uint, FinishEventState> __tracked_events = [];
    [Tracked]
    private partial TrackedDictionary<uint, FinishEventState> _events { get; }

    public IReadOnlyDictionary<uint, FinishEventState> Events => _events;

    public IReadOnlyCollection<uint> Claimed => _claimed;

    public void Load(
        IEnumerable<(uint EventId, ulong Progress, bool Finish)> persisted,
        IEnumerable<uint> claimed
    )
    {
        _events.Clear();
        _claimed.Clear();

        foreach (var row in persisted)
        {
            var threshold = assets.Achievements.NeedCount(row.EventId);

            if (threshold == 0)
                continue;

            var progress = Math.Min(row.Progress, threshold);
            _events[row.EventId] = new FinishEventState(row.EventId, progress, progress >= threshold);
        }

        foreach (var achievementId in claimed)
        {
            if (assets.Achievements.Get(achievementId) is not null)
                _claimed.Add(achievementId);
        }

        AcceptLoadedState();
    }

    public CmdAchievementData ToAchievementData()
    {
        var data = new CmdAchievementData();

        foreach (var state in _events.Values.OrderBy(row => row.EventId))
        {
            data.FinishEvents.Add(new CmdOneFinishEvent {
                Id = state.EventId,
                Progress = state.Progress,
                Finish = state.Finish
            });
        }
        data.RewardAchievements.AddRange(_claimed);
        return data;
    }

    public (int Result, bool Changed, FinishEventState? State) AddProgress(uint eventId, uint count)
    {
        var threshold = assets.Achievements.NeedCount(eventId);

        if (threshold == 0)
            return ((int)EnmTextCode.EnmTextAchievementInvalidEvent, false, null);

        var current = _events.GetValueOrDefault(eventId);
        var progress = (uint)Math.Min((current?.Progress ?? 0) + count, threshold);

        if (current is not null && current.Progress == progress && current.Finish == progress >= threshold)
            return (0, false, current);

        var state = new FinishEventState(eventId, progress, progress >= threshold);
        _events[eventId] = state;

        if (state.Finish && current?.Finish != true)
            Log.Stage("achievement event {EventId} completed at progress {Progress} with threshold {Threshold}", eventId, progress, threshold);
        return (0, true, state);
    }

    public int CheckClaim(IReadOnlyList<uint> achievementIds)
    {
        if (achievementIds.Count == 0 || achievementIds.Distinct().Count() != achievementIds.Count)
            return (int)EnmTextCode.EnmTextWrongParam;

        foreach (var id in achievementIds)
        {
            var achievement = assets.Achievements.Get(id);

            if (achievement is null)
                return (int)EnmTextCode.EnmTextAchievementNotFound;

            if (_claimed.Contains(id))
                return (int)EnmTextCode.EnmTextAchievementAlreadyReward;

            if (!IsEventFinished(achievement.FinishId))
                return (int)EnmTextCode.EnmTextAchievementNotFinished;
        }

        return 0;
    }

    public void MarkClaimed(IReadOnlyList<uint> achievementIds)
    {
        foreach (var id in achievementIds)
        {
            if (_claimed.Add(id))
            {

                Log.Stage("achievement {AchievementId} reward claim recorded", id);
            }
        }
    }

    public IReadOnlyList<ItemGrant> RewardOf(uint achievementId)
    {
        var dropId = assets.Achievements.Get(achievementId)?.DropId ?? 0;
        return assets.Drops.Exists(dropId) ? assets.Drops.Bundle(dropId) : assets.DropTable.Roll(dropId, _random);
    }

    public bool IsEventFinished(uint eventId) =>
        _events.GetValueOrDefault(eventId)?.Finish ?? false;

    public ulong ProgressOf(uint eventId) =>
        _events.GetValueOrDefault(eventId)?.Progress ?? 0;

    public CmdOneFinishEvent? EventNotificationOf(uint eventId)
    {
        if (!_events.TryGetValue(eventId, out var state))
            return null;

        return new CmdOneFinishEvent {
            Id = state.EventId,
            Progress = state.Progress,
            Finish = state.Finish
        };
    }

}
