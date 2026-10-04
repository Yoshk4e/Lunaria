using Google.Protobuf;
using Lunaria.Game.Battle;
using Lunaria.Game.Player.Gameplay;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    public Lunaria.Game.Battle.BattleSession? CurrentBattle => Battles.Current;
    /// <summary>Patrol clusters still cooling down, as SC_PATROL_MONSTER_RES lists them.</summary>
    public IReadOnlyList<long> PatrolCooldowns =>
        Battles.PatrolCooldownEnds.Where(pair => pair.Value > UtcNow).Select(pair => pair.Key).ToArray();

    public int EnterBattle(EBattleType type, uint fieldId, uint instanceId, EnmMonsterFromType monsterFrom)
    {
        using var operationTime = BeginOperation();
        if (!BattleContextMatches(type, fieldId)) return (int)EnmTextCode.EnmTextBattleStateNotMatch;
        // Select the correct story/dungeon/Wanted team before the battle freezes it.
        if (Battles.Current is null) ReconcileTemporaryTeam();
        return Battles.Enter(type, fieldId, instanceId, monsterFrom);
    }

    public int StartBattle(EBattleType type, uint fieldId)
    {
        using var operationTime = BeginOperation();
        return BattleContextMatches(type, fieldId) ? Battles.Start(type, fieldId)
            : (int)EnmTextCode.EnmTextBattleStateNotMatch;
    }

    public int PauseBattle(EBattleType type, uint fieldId, bool paused)
    {
        using var operationTime = BeginOperation();
        return BattleContextMatches(type, fieldId) ? Battles.Pause(type, fieldId, paused)
            : (int)EnmTextCode.EnmTextBattleStateNotMatch;
    }

    private bool BattleContextMatches(EBattleType type, uint fieldId)
    {
        if (type == EBattleType.EnmBattleTypeWanted) return Wanted.MatchesBattle(fieldId);
        if (Wanted.IsRunning) return false;
        if (type is EBattleType.EnmBattleTypeRepeatDungeon or EBattleType.EnmBattleTypeWeekDungeon or EBattleType.EnmBattleTypeHorde)
            return Dungeons.Current is {} dungeon
                && assets.Dungeons.Dungeon(dungeon.DungeonId)?.BattleId.Contains(fieldId) == true;
        return Dungeons.Current is null;
    }

    public BattleLeaveOutcome LeaveBattle(CSLeaveBattle report)
    {
        using var operationTime = BeginOperation();
        // Patrol encounters enter without battle_inst_id/monster_from_type but report them on leave,
        // so only identifiers sent at entry are checked.
        if (!Enum.IsDefined(report.BattleResult)
            || !Enum.IsDefined(report.MonsterFromType)
            || Battles.Current is {} running
            && (running.BattleInstId != 0 && running.BattleInstId != report.BattleInstId
                || running.MonsterFrom != default && running.MonsterFrom != report.MonsterFromType))
            return new BattleLeaveOutcome((int)EnmTextCode.EnmTextBattleStateNotMatch, RewardDelivery.Empty, false,
                RewardDelivery.Empty, []);
        var settlement = Battles.Leave(report.BattleType, report.BattleFieldId,
            report.BattleResult == EBattleResultType.EnmBattleResultTypeSuccess);


        if (!settlement.Accepted || !settlement.Begun)
        {
            if (settlement.Accepted) ReconcileTemporaryTeam();
            return new BattleLeaveOutcome(settlement.Result, RewardDelivery.Empty, WantedStepCompleted: false,
                RewardDelivery.Empty, []);
        }

        // Ignore client vitals of -1, which mean untracked.
        var changed = new List<ulong>();
        var respawn = report.BattleResult == EBattleResultType.EnmBattleResultTypeDeadFail
            && report.BattleType != EBattleType.EnmBattleTypeWanted;

        foreach (var character in report.CharacterData)
        {
            if (!CurrentTeamMembers().Contains(character.InstId))
                continue;

            var (hp, liquid) = (TeamCharacterHp(character.InstId), TeamCharacterLiquid(character.InstId));

            if (respawn)
                SetTeamCharacterVitals(character.InstId, hp: TeamCharacterMaxHp(character.InstId));
            else if (character.CurrentHp >= 0)
                SetTeamCharacterVitals(character.InstId, hp: character.CurrentHp);

            if (character.PermanentLiquid >= 0)
                SetTeamCharacterVitals(character.InstId, liquid: character.PermanentLiquid);

            if (TeamCharacterHp(character.InstId) != hp || TeamCharacterLiquid(character.InstId) != liquid)
                changed.Add(character.InstId);
        }

        var changedLiquid = ApplyCurrentTeamLiquid(report.TemporaryLiquid, report.TemporaryLiquidLv2);

        // battle_inst_id names the static patrol cluster (its TemplateID). Quest-spawned groups do not cool down.
        var notifications = new List<IMessage>();
        if (settlement.Victory && report.BattleType == EBattleType.EnmBattleTypePatrol
            && report.MonsterFromType == EnmMonsterFromType.EmonsterFromTable && report.BattleInstId != 0)
        {
            Battles.StartPatrolCooldown(report.BattleInstId, UtcNow + BattleManager.PatrolRespawnDelay);
            notifications.Add(new SCPatrolMonsterNtf { MonsterId = report.BattleInstId, CdIsOk = false });
        }

        var (buffUpdates, buffRemoved) = Buffs.BattleEnded(UtcNow);

        var wantedBefore = Wanted.ToResource();
        var (stepCompleted, _, stepDrop) = settlement.Victory && report.BattleType == EBattleType.EnmBattleTypeWanted
            && Wanted.MatchesBattle(report.BattleFieldId) ?
            Wanted.OnBattleEnded(success: true) :
            (false, null, []);
        if (stepCompleted) Gameplay.Publish(new WantedResourcesChanged(wantedBefore));

        if (settlement.Victory)
            Gameplay.Publish(new BattleCompleted(report.BattleFieldId));

        if (changed.Count > 0)
            Gameplay.Publish(new VitalsChanged(changed));

        if (changedLiquid)
            Gameplay.Publish(new LiquidChanged());

        if (buffUpdates.Count > 0 || buffRemoved.Count > 0)
            Gameplay.Publish(new BuffsChanged(
                buffUpdates.Select(data => (data, true)).ToList(), buffRemoved));

        ReconcileTemporaryTeam();

        return new BattleLeaveOutcome(
            settlement.Result,
            stepCompleted ? GrantRewards(stepDrop, EnmItemReason.EnmItemChangeWantedStep) : RewardDelivery.Empty, stepCompleted,
            settlement.Victory && report.BattleType != EBattleType.EnmBattleTypeWanted ? GrantBattleLoot(report.BattleFieldId) : RewardDelivery.Empty,
            notifications);
    }

    /// <summary>Victory loot of the battlefield at the current world level. Daily caps (RewardLimitID) are not applied.</summary>
    private RewardDelivery GrantBattleLoot(uint battleFieldId)
    {
        var dropId = Assets.BattleRewards.DropFor(battleFieldId, Progress.WorldLevel);
        if (dropId == 0) return RewardDelivery.Empty;

        var grants = Assets.DropTable.Roll(dropId, RandomSources.Loot);
        return grants.Count > 0 ? GrantRewards(grants, EnmItemReason.EnmItemChangeBattleEnd) : RewardDelivery.Empty;
    }

    /// <summary>Patrol clusters whose cooldown ended, announced as available again.</summary>
    private IEnumerable<IMessage> RespawnedPatrols(DateTimeOffset now) =>
        Battles.ExpirePatrolCooldowns(now).Select(cluster => new SCPatrolMonsterNtf { MonsterId = (uint)cluster, CdIsOk = true });

    public IReadOnlyList<ulong> HealRoster()
    {
        var healed = Characters.HealAll().ToList();

        if (healed.Count > 0)
            Gameplay.Publish(new VitalsChanged(healed));

        return healed;
    }

    public readonly record struct BattleLeaveOutcome(
        int Result,
        RewardDelivery WantedReward,
        bool WantedStepCompleted,
        RewardDelivery BattleReward,
        IReadOnlyList<IMessage> Notifications
    );
}
