using Lunaria.Game.Resources;
using Lunaria.Game.Tasks;
using Msg;

namespace Lunaria.Game.Player.Gameplay;

internal sealed class ProgressionHooks :
    IGameplayHook<HousePurchased>,
    IGameplayHook<HouseUpgraded>,
    IGameplayHook<MotiveAcquired>,
    IGameplayHook<ItemAcquired>,
    IGameplayHook<ItemUsed>,
    IGameplayHook<StaminaSpent>,
    IGameplayHook<DungeonCleared>,
    IGameplayHook<WantedCleared>,
    IGameplayHook<CollectionGathered>,
    IGameplayHook<TeleportUnlocked>,
    IGameplayHook<CharacterLeveled>,
    IGameplayHook<CharactersAcquired>,
    IGameplayHook<TeamLevelChanged>,
    IGameplayHook<RoleLoggedIn>,
    IGameplayHook<AttendanceChanged>,
    IGameplayHook<CreatureAcquired>,
    IGameplayHook<BattleCompleted>,
    IGameplayHook<TaskProgressed>
{
    public void Handle(Player p, AttendanceChanged e, PlayerChanges changes) => ReconcileAttendance(p, changes);

    public void Handle(Player p, BattleCompleted e, PlayerChanges changes) =>
        p.RecordTaskEvent(ServerTarget.CompleteBattle, e.BattleId);

    public void Handle(Player p, CharacterLeveled e, PlayerChanges changes) => ReconcileCharacters(p, changes);

    public void Handle(Player p, CharactersAcquired e, PlayerChanges changes)
    {
        p.Skills.GrantStarter(p.Characters);
        ReconcileCharacters(p, changes);
    }

    public void Handle(Player p, CollectionGathered e, PlayerChanges changes)
    {
        if (e.CollectionType >= 0)
            Increment(p, changes, p.Assets.Unlocks.ArgEvents(GlobalEventSub.CollectItemType, (ulong)e.CollectionType), count: 1);
        RefreshExploration(p, changes);
    }

    public void Handle(Player p, CreatureAcquired e, PlayerChanges changes) => RefreshExploration(p, changes);

    public void Handle(Player p, DungeonCleared e, PlayerChanges changes)
    {
        Increment(p, changes, p.Assets.Unlocks.ArgEvents(GlobalEventSub.DungeonCount, e.DungeonId), count: 1);

        if (p.Assets.Dungeons.Dungeon(e.DungeonId) is {} dungeon)
            Increment(p, changes, p.Assets.Unlocks.ArgEvents(GlobalEventSub.DungeonTypeCount, dungeon.DungeonType), count: 1);
    }

    public void Handle(Player p, HousePurchased e, PlayerChanges changes)
    {
        Increment(p, changes, p.Assets.Unlocks.CountEvents(GlobalEventSub.AddHouseAny), count: 1);
        Increment(p, changes, p.Assets.Unlocks.ArgEvents(GlobalEventSub.AddHouse, e.HouseId), count: 1);
    }

    public void Handle(Player p, HouseUpgraded e, PlayerChanges changes) =>
        Increment(p, changes, p.Assets.Unlocks.ArgEvents(GlobalEventSub.AddHouse, e.HouseId), count: 1);

    public void Handle(Player p, ItemAcquired e, PlayerChanges changes)
    {
        if (e.Count == 0 || p.Assets.Items.Get(e.ItemId) is not {} item) return;

        Increment(p, changes, p.Assets.Unlocks.ArgEvents(GlobalEventSub.AddItemType, (ulong)item.ShowType), e.Count);
    }

    public void Handle(Player p, ItemUsed e, PlayerChanges changes)
    {
        p.RecordTaskEvent(ServerTarget.UseItem, e.ItemId, e.Count);

        if (p.Assets.Items.Get(e.ItemId) is {} item)
            Increment(p, changes, p.Assets.Unlocks.ArgEvents(GlobalEventSub.UseItemType, (ulong)item.ShowType), e.Count);
    }

    public void Handle(Player p, MotiveAcquired e, PlayerChanges changes)
    {
        Increment(p, changes, p.Assets.Unlocks.MotiveTypeEvents(p.Assets.Motives.Identity(e.MotiveId)), e.Count);
        Increment(p, changes, p.Assets.Unlocks.MotiveRareEvents(p.Assets.Motives.Rarity(e.MotiveId)), e.Count);
    }

    public void Handle(Player p, RoleLoggedIn e, PlayerChanges changes)
    {
        // Count every login even after the lifetime login goal is complete.
        Increment(p, changes, p.Assets.Unlocks.LoginEvents(), count: 1);
        ReconcileLevels(p, changes);
        ReconcileCharacters(p, changes);
        ReconcileAttendance(p, changes);
        RefreshExploration(p, changes);
    }

    public void Handle(Player p, StaminaSpent e, PlayerChanges changes) =>
        Increment(p, changes, p.Assets.Unlocks.CountEvents(GlobalEventSub.StaminaCost), e.Amount);

    public void Handle(Player p, TaskProgressed e, PlayerChanges changes)
    {
        var progress = e.Progress;
        if (!progress.Recorded) return;

        foreach (var step in progress.PassedSteps)
        {
            Increment(p, changes, p.Assets.Unlocks.StepEvents(step), count: 1);
        }

        if (progress.TaskCompleted)
            Increment(p, changes, p.Assets.Unlocks.TaskEvents(progress.TaskId), count: 1);
        ReconcileLevels(p, changes);
        if (progress.PassedSteps.Count > 0) RefreshExploration(p, changes);
    }

    public void Handle(Player p, TeamLevelChanged e, PlayerChanges changes) => ReconcileLevels(p, changes);

    public void Handle(Player p, TeleportUnlocked e, PlayerChanges changes)
    {
        Increment(p, changes, p.Assets.Unlocks.ArgEvents(GlobalEventSub.TeleportPoint, e.PointId), count: 1);
        RefreshExploration(p, changes);
    }

    public void Handle(Player p, WantedCleared e, PlayerChanges changes)
    {
        Increment(p, changes, p.Assets.Unlocks.CountEvents(GlobalEventSub.WantedFinish), count: 1);
        Increment(p, changes, p.Assets.Unlocks.ArgEvents(GlobalEventSub.WantedFinish, e.EntryId), count: 1);
    }

    internal static void Increment(Player p, PlayerChanges changes, IEnumerable<uint> events, uint count)
    {
        if (count == 0) return;

        var dailyChanged = false;

        foreach (var eventId in events)
        {
            dailyChanged |= p.DailyMissions.AddEventProgress(eventId, count);
            var (code, moved, _) = p.Achievements.AddProgress(eventId, count);

            if (code == 0 && moved && p.Achievements.EventNotificationOf(eventId) is {} state)
                changes.Add(new SCFinishEventUpdate { FinishEvent = state });
        }

        if (dailyChanged)
            changes.Add(new SCDailyMissionNtf { Data = p.DailyMissions.ToDailyMissionData() });
    }

    // Do not add lifetime totals to daily counters during reconciliation.
    private static void EnsureAtLeast(Player p, PlayerChanges changes, uint eventId, uint target)
    {
        target = Math.Min(target, p.Assets.Achievements.NeedCount(eventId));
        var current = p.Achievements.ProgressOf(eventId);
        if (current >= target) return;

        p.Achievements.AddProgress(eventId, (uint)(target - current));
        changes.Add(new SCFinishEventUpdate { FinishEvent = p.Achievements.EventNotificationOf(eventId) });
    }

    private static void ReconcileLevels(Player p, PlayerChanges changes)
    {
        foreach (var id in p.Assets.Unlocks.EventsOfSubType(GlobalEventSub.PlayerLv))
        {
            EnsureAtLeast(p, changes, id, p.Progress.TeamLevel);
        }

        foreach (var id in p.Assets.Unlocks.EventsOfSubType(GlobalEventSub.WorldLv))
        {
            EnsureAtLeast(p, changes, id, p.Progress.EarnedWorldLevel);
        }
    }

    private static void ReconcileCharacters(Player p, PlayerChanges changes)
    {
        foreach (var id in p.Assets.Unlocks.EventsOfSubType(GlobalEventSub.RoleLvUp))
        {
            if (p.Assets.Unlocks.FirstNumericArg(id) is > 0 and var level)
                EnsureAtLeast(p, changes, id, (uint)p.Characters.All.Count(c => c.Level >= level));
        }
    }

    private static void ReconcileAttendance(Player p, PlayerChanges changes)
    {
        foreach (var id in p.Assets.Unlocks.EventsOfSubType(GlobalEventSub.PassDay))
        {
            EnsureAtLeast(p, changes, id, p.SignIn.AttendanceDays);
        }
    }

    private static void RefreshExploration(Player p, PlayerChanges changes)
    {
        var moved = p.RecalculateRegionProgress();

        foreach (var id in p.RegionProgress.DrainUnlocked())
        {
            changes.Add(new SCUnlockSubRegionNtf { SubRegionId = id });
        }

        foreach (var id in moved)
        {
            if (p.RegionProgress.ToSubRegionUpdate(id) is {} update) changes.Add(update);
        }

        foreach (var id in p.Assets.Unlocks.EventsOfSubType(GlobalEventSub.SubRegionProgress))
        {
            if (p.Assets.Unlocks.FirstNumericArg(id) is > 0 and <= uint.MaxValue and var percent)
                EnsureAtLeast(p, changes, id, p.RegionProgress.SubRegionsAtPercent((uint)percent));
        }
    }
}
