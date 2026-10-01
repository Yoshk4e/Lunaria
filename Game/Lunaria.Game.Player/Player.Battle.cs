using Lunaria.Game.Player.Gameplay;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{


    public BattleLeaveOutcome LeaveBattle(CSLeaveBattle report)
    {
        // Patrol encounters enter without battle_inst_id/monster_from_type but report them on leave,
        // so only identifiers sent at entry are checked.
        if (!Enum.IsDefined(report.BattleResult)
            || Battles.Current is {} running
            && (running.BattleInstId != 0 && running.BattleInstId != report.BattleInstId
                || running.MonsterFrom != default && running.MonsterFrom != report.MonsterFromType))
            return new BattleLeaveOutcome((int)EnmTextCode.EnmTextBattleStateNotMatch, RewardDelivery.Empty, false);
        var settlement = Battles.Leave(report.BattleType, report.BattleFieldId,
            report.BattleResult == EBattleResultType.EnmBattleResultTypeSuccess);


        if (!settlement.Accepted || !settlement.Begun)
        {
            if (settlement.Accepted) ReconcileTemporaryTeam();
            return new BattleLeaveOutcome(settlement.Result, RewardDelivery.Empty, WantedStepCompleted: false);
        }

        // Ignore client vitals of -1, which mean untracked.
        var changed = new List<ulong>();
        var wiped = report.BattleResult == EBattleResultType.EnmBattleResultTypeDeadFail;

        foreach (var character in report.CharacterData)
        {
            if (!CurrentTeamMembers().Contains(character.InstId))
                continue;

            var (hp, liquid) = (TeamCharacterHp(character.InstId), TeamCharacterLiquid(character.InstId));

            if (wiped)
                SetTeamCharacterVitals(character.InstId, hp: TeamCharacterMaxHp(character.InstId));
            else if (character.CurrentHp >= 0)
                SetTeamCharacterVitals(character.InstId, hp: character.CurrentHp);

            if (character.PermanentLiquid >= 0)
                SetTeamCharacterVitals(character.InstId, liquid: character.PermanentLiquid);

            if (TeamCharacterHp(character.InstId) != hp || TeamCharacterLiquid(character.InstId) != liquid)
                changed.Add(character.InstId);
        }

        var changedLiquid = ApplyCurrentTeamLiquid(report.TemporaryLiquid, report.TemporaryLiquidLv2);

        if (report.BattleEndInfo?.Monsters is { Count: > 0 } kills)
            Battles.RecordKills(kills.Select(id => id));

        var (buffUpdates, buffRemoved) = Buffs.BattleEnded(DateTimeOffset.UtcNow);

        var wantedBefore = Wanted.ToResource();
        var (stepCompleted, _, stepDrop) = settlement.Victory && report.BattleType == EBattleType.EnmBattleTypeWanted ?
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
            stepCompleted ? GrantRewards(stepDrop, EnmItemReason.EnmItemChangeWantedStep) : RewardDelivery.Empty, stepCompleted);
    }

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
        bool WantedStepCompleted
    );
}
