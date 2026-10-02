using Lunaria.Game.Dungeons;
using Lunaria.Game.Logging;
using Lunaria.Game.Player.Gameplay;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    public DungeonEntryOutcome EnterDungeon(ulong dungeonId)
    {
        using var operationTime = BeginOperation();
        if (dungeonId > uint.MaxValue || Wanted.IsRunning || Battles.Current is not null
            || QueryTemporaryTeam((int)EnmTmpTeamType.Dungeon, (uint)dungeonId)?.MemberData.Count is not > 0)
        {
            Log.Flag("dungeon {DungeonId} entry refused, wanted or battle active or no dungeon team", dungeonId);
            return new DungeonEntryOutcome((int)EnmTextCode.EnmTextWrongParam, dungeonId, 0);
        }
        var now = UtcNow;
        var code = Dungeons.CheckEnter(dungeonId, now);

        if (code != 0)
            return new DungeonEntryOutcome(code, dungeonId, StaminaSpent: 0);

        var cost = assets.Dungeons.Type(assets.Dungeons.Dungeon(dungeonId)!.DungeonType)?.VitalityCost ?? 0;

        if (cost > 0 && SpendDungeonStamina(cost, now) != 0)
        {
            Log.Flag("dungeon {DungeonId} entry refused, stamina {Cost} not affordable", dungeonId, cost);
            return new DungeonEntryOutcome((int)EnmTextCode.EnmTextStaminaNotEnough, dungeonId, StaminaSpent: 0);
        }

        Dungeons.Enter(dungeonId, now);
        ResetDungeonEntryLiquid(dungeonId);
        ReconcileTemporaryTeam();
        if (cost > 0) Gameplay.Publish(new StaminaSpent(cost));
        return new DungeonEntryOutcome(Code: 0, dungeonId, cost);
    }

    public DungeonEntryOutcome AdoptDungeonCurrent(ulong dungeonId, uint battleId)
    {
        using var operationTime = BeginOperation();
        if (dungeonId > uint.MaxValue || Wanted.IsRunning
            || Dungeons.Current is null && Battles.Current is not null
            || QueryTemporaryTeam((int)EnmTmpTeamType.Dungeon, (uint)dungeonId)?.MemberData.Count is not > 0)
            return new DungeonEntryOutcome((int)EnmTextCode.EnmTextWrongParam, dungeonId, 0);
        var now = UtcNow;
        var (code, opened) = Dungeons.AdoptCurrent(dungeonId, battleId, now);

        if (code != 0 || !opened)
        {
            if (code == 0) ReconcileTemporaryTeam();
            return new DungeonEntryOutcome(code, dungeonId, StaminaSpent: 0);
        }

        var cost = assets.Dungeons.Type(assets.Dungeons.Dungeon(dungeonId)!.DungeonType)?.VitalityCost ?? 0;

        if (cost > 0 && SpendDungeonStamina(cost, now) != 0)
        {
            Dungeons.AbandonCurrent();
            return new DungeonEntryOutcome((int)EnmTextCode.EnmTextStaminaNotEnough, dungeonId, StaminaSpent: 0);
        }

        ResetDungeonEntryLiquid(dungeonId);
        ReconcileTemporaryTeam();
        if (cost > 0) Gameplay.Publish(new StaminaSpent(cost));
        return new DungeonEntryOutcome(Code: 0, dungeonId, cost);
    }

    private void ResetDungeonEntryLiquid(ulong dungeonId)
    {
        var dungeon = assets.Dungeons.Dungeon(dungeonId)!;
        if (assets.Dungeons.Type(dungeon.DungeonType)?.CharacterPermanentLiquidRatio != 0) return;

        // Only a newly opened, paid run resets its selected participants. Reconnects
        // and temporary-team reconciliation must preserve their current combat state.
        var changed = new List<ulong>();
        foreach (var member in QueryTemporaryTeam((int)EnmTmpTeamType.Dungeon, (uint)dungeonId)!.MemberData)
        {
            if (Characters.PermanentLiquid(member.InstId) == 0) continue;
            Characters.SetPermanentLiquid(member.InstId, 0);
            changed.Add(member.InstId);
        }
        if (changed.Count > 0) Gameplay.Publish(new VitalsChanged(changed));
    }

    public (int Code, RewardDelivery? Delivery, HordeState? Horde) FinishDungeon(
        ulong dungeonId,
        bool victory,
        bool leave,
        uint hordeKills
    )
    {
        using var operationTime = BeginOperation();
        if (Battles.Current is not null) return ((int)EnmTextCode.EnmTextWrongParam, null, null);
        var result = Dungeons.Finish(dungeonId, victory, leave, hordeKills, RandomSources.Loot, UtcNow);

        if (result.Result != 0)
            return (result.Result, null, null);

        ReconcileTemporaryTeam();

        var delivery = result.Settled
            ? GrantRewards(result.Rewards, result.Victory ? EnmItemReason.EnmItemChangeDungeonsEnd : default)
            : null;
        Log.State("dungeon {DungeonId} settled, victory {Victory}, leave {Leave}, rewards {RewardLines}",
            dungeonId, result.Victory, leave, result.Rewards.Count);
        if (result.Victory) Gameplay.Publish(new DungeonCleared(dungeonId));
        return (0, delivery, result.Horde);
    }

    public readonly record struct DungeonEntryOutcome(int Code, ulong DungeonId, uint StaminaSpent);

    private int SpendDungeonStamina(uint cost, DateTimeOffset now)
    {
        if (cost > int.MaxValue) return (int)EnmTextCode.EnmTextStaminaNotEnough;
        var before = Progress.Stamina;
        var code = Progress.SpendStamina((int)cost, now);
        if (code != 0 && Progress.Stamina != before)
            Gameplay.Publish(new StaminaChanged(Progress.Stamina));
        return code;
    }
}
