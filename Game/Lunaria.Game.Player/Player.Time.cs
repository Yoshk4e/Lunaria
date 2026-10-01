using Google.Protobuf;
using Lunaria.Game.Player.Gameplay;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    private DateTimeOffset? _lastTimeSettlement;

    public IReadOnlyList<IMessage> AdvanceTime(DateTimeOffset now)
    {
        // Moving the system clock backward must not reset daily counters.
        if (_lastTimeSettlement is {} previous && now < previous) return [];
        _lastTimeSettlement = now;
        var messages = new List<IMessage>();
        if (Progress.Regenerate(now) > 0) Gameplay.Publish(new StaminaChanged(Progress.Stamina));

        var expiredBuffs = Buffs.Sweep(now);
        if (expiredBuffs.Count > 0) Gameplay.Publish(new BuffsChanged([], expiredBuffs));
        var expiredMail = Mails.SweepExpired(now.ToUnixTimeSeconds());
        if (expiredMail.Count > 0) messages.Add(new SCMailAddDelNft { DelMailIds = { expiredMail } });
        RetryPendingRewardMail(now);
        if (DailyMissions.AdvanceTime(now))
            messages.Add(new SCDailyMissionNtf { Data = DailyMissions.ToDailyMissionData(now) });

        if (RespawnedCollections(now) is {} respawned) messages.Add(respawned);

        var houses = DueHouseIncomeAnnouncements(now);
        if (houses.Count > 0) messages.Add(new SCHouseIncomeNtf { HouseInfoList = { houses } });
        messages.AddRange(SettleMonthCards(now));
        if (TasksBootstrapped)
            foreach (var outcome in SettleServerTargets())
                messages.AddRange(outcome.AllNotifications);
        return messages;
    }
}
