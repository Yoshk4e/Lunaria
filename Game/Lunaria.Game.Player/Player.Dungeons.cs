using Lunaria.Game.Dungeons;
using Lunaria.Game.Logging;
using Lunaria.Game.Player.Gameplay;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    public DungeonEntryOutcome EnterDungeon(ulong dungeonId)
    {
        if (dungeonId > uint.MaxValue || Wanted.IsRunning || Battles.Current is not null
            || QueryTemporaryTeam((int)EnmTmpTeamType.Dungeon, (uint)dungeonId)?.MemberData.Count is not > 0)
        {
            Log.Flag("dungeon {DungeonId} entry refused, wanted or battle active or no dungeon team", dungeonId);
            return new DungeonEntryOutcome((int)EnmTextCode.EnmTextWrongParam, dungeonId, 0);
        }
        var now = DateTimeOffset.UtcNow;
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
        ReconcileTemporaryTeam();
        if (cost > 0) Gameplay.Publish(new StaminaSpent(cost));
        return new DungeonEntryOutcome(Code: 0, dungeonId, cost);
    }

    public DungeonEntryOutcome AdoptDungeonCurrent(ulong dungeonId, uint battleId)
    {
        if (dungeonId > uint.MaxValue || Wanted.IsRunning
            || Dungeons.Current is null && Battles.Current is not null
            || QueryTemporaryTeam((int)EnmTmpTeamType.Dungeon, (uint)dungeonId)?.MemberData.Count is not > 0)
            return new DungeonEntryOutcome((int)EnmTextCode.EnmTextWrongParam, dungeonId, 0);
        var now = DateTimeOffset.UtcNow;
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

        ReconcileTemporaryTeam();
        if (cost > 0) Gameplay.Publish(new StaminaSpent(cost));
        return new DungeonEntryOutcome(Code: 0, dungeonId, cost);
    }

    public (int Code, RewardDelivery? Delivery, HordeState? Horde) FinishDungeon(
        ulong dungeonId,
        bool victory,
        bool leave,
        uint hordeKills
    )
    {
        if (Battles.Current is not null) return ((int)EnmTextCode.EnmTextWrongParam, null, null);
        var result = Dungeons.Finish(dungeonId, victory, leave, hordeKills, GachaRng, DateTimeOffset.UtcNow);

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
