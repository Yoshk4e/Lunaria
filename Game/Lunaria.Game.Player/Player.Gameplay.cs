using Lunaria.Common.Tracking;
using Google.Protobuf;
using Lunaria.Game.Player.Gameplay;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    private readonly PlayerChanges _changes = new();
    [Untracked]
    private GameplayEventDispatcher? _gameplay;

    private GameplayEventDispatcher Gameplay
    {
        get
        {
            if (_gameplay is not null) return _gameplay;

            var dispatcher = new GameplayEventDispatcher(this, _changes);
            var progress = new ProgressionHooks();
            var sync = new SyncHooks();
            var temporaryTeams = new TemporaryTeamHooks();
            dispatcher.Register<TaskProgressed>(temporaryTeams);
            dispatcher.Register<RoleLoggedIn>(temporaryTeams);
            dispatcher.Register<HousePurchased>(progress);
            dispatcher.Register<HouseUpgraded>(progress);
            dispatcher.Register<MotiveAcquired>(progress);
            dispatcher.Register<ItemAcquired>(progress);
            dispatcher.Register<ItemUsed>(progress);
            dispatcher.Register<StaminaSpent>(progress);
            dispatcher.Register<DungeonCleared>(progress);
            dispatcher.Register<WantedCleared>(progress);
            dispatcher.Register<CollectionGathered>(progress);
            dispatcher.Register<TeleportUnlocked>(progress);
            dispatcher.Register<CharacterLeveled>(progress);
            dispatcher.Register<TeamLevelChanged>(progress);
            dispatcher.Register<RoleLoggedIn>(progress);
            dispatcher.Register<AttendanceChanged>(progress);
            dispatcher.Register<CreatureAcquired>(progress);
            dispatcher.Register<CharactersAcquired>(progress);
            dispatcher.Register<BattleCompleted>(progress);
            dispatcher.Register<TaskProgressed>(progress);
            dispatcher.Register<WalletChanged>(sync);
            dispatcher.Register<BagChanged>(sync);
            dispatcher.Register<StaminaSpent>(sync);
            dispatcher.Register<StaminaChanged>(sync);
            dispatcher.Register<SatietyChanged>(sync);
            dispatcher.Register<CooldownStarted>(sync);
            dispatcher.Register<VitalsChanged>(sync);
            dispatcher.Register<LiquidChanged>(sync);
            dispatcher.Register<BuffsChanged>(sync);
            dispatcher.Register<CharactersChanged>(sync);
            dispatcher.Register<CharacterLeveled>(sync);
            dispatcher.Register<CharactersAcquired>(sync);
            dispatcher.Register<GuidesChanged>(sync);
            dispatcher.Register<BattlePassChanged>(sync);
            dispatcher.Register<CreatureRosterChanged>(sync);
            dispatcher.Register<LevelDataChanged>(sync);
            dispatcher.Register<WantedResourcesChanged>(sync);
            return _gameplay = dispatcher;
        }
    }

    public IReadOnlyList<IMessage> DrainGameplayChanges() => _changes.Drain();

    public void CompleteRoleLogin()
    {
        using var operationTime = BeginOperation();
        _changes.Clear();
        Bag.DrainChanged();
        Wallet.DrainChanged();
        Motives.DrainInventoryChanges();
        _syncedLevelData = LevelData();
        Gameplay.Publish(new RoleLoggedIn());
    }

    public int UnlockTeleport(ulong pointId)
    {
        using var operationTime = BeginOperation();
        var known = Map.UnlockedTeleports.Contains(pointId);
        var code = Map.UnlockTeleport(pointId);
        if (code == 0 && !known) Gameplay.Publish(new TeleportUnlocked(pointId));
        return code;
    }

    public int UnlockSavepoint(ulong pointId)
    {
        using var operationTime = BeginOperation();
        var code = Map.UnlockSavepoint(pointId);
        return code;
    }

    public (int Result, SignInActivityData? Data) QuerySignIn(uint activityId)
    {
        using var operationTime = BeginOperation();
        var result = SignIn.Query(activityId);
        if (result.Result == 0) Gameplay.Publish(new AttendanceChanged());
        return result;
    }

    public int ReportClientProgress(uint eventId, uint count)
    {
        using var operationTime = BeginOperation();
        if (!assets.Unlocks.IsClientReportable(eventId) || count == 0)
            return (int)EnmTextCode.EnmTextAchievementInvalidEvent;

        ProgressionHooks.Increment(this, _changes, [eventId], count);
        return 0;
    }
}
