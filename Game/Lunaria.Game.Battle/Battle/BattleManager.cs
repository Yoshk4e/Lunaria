using Msg;

namespace Lunaria.Game.Battle;

public sealed record BattleSession(
    EBattleType Type,
    uint BattleFieldId,
    uint BattleInstId,
    EnmMonsterFromType MonsterFrom,
    bool Started,
    bool Paused
);

public sealed record BattleSettlement(int Result, bool Accepted, bool Begun, bool Victory);

public sealed class BattleManager
{
    private const int MinType = (int)EBattleType.EnmBattleTypeExpose;
    private const int MaxType = (int)EBattleType.EnmBattleTypeHorde;
    private readonly SortedSet<long> _patrolCooldown = [];

    public BattleSession? Current { get; private set; }

    public IReadOnlyCollection<long> PatrolCooldown => _patrolCooldown;

    public void ResetMonster(long monsterId) => _patrolCooldown.Remove(monsterId);

    public int Enter(EBattleType type, uint battleFieldId, uint battleInstId, EnmMonsterFromType monsterFrom)
    {
        var code = Validate(type);

        if (code != 0)
            return code;

        if (!Enum.IsDefined(monsterFrom))
            return (int)EnmTextCode.EnmTextBattleStateNotMatch;

        if (Current is {} running)
            return running.Type == type && running.BattleFieldId == battleFieldId
                && running.BattleInstId == battleInstId && running.MonsterFrom == monsterFrom
                ? 0 : (int)EnmTextCode.EnmTextBattleAleardyExist;

        Current = new BattleSession(type, battleFieldId, battleInstId, monsterFrom, Started: false, Paused: false);
        return 0;
    }

    public int Start(EBattleType type, uint battleFieldId)
    {
        var code = Validate(type);

        if (code != 0)
            return code;

        if (Current is not {} running
            || running.Type != type
            || running.BattleFieldId != battleFieldId)
            return (int)EnmTextCode.EnmTextBattleStateNotMatch;

        if (running.Started)
            return 0;

        Current = running with { Started = true };
        return 0;
    }

    public int Pause(EBattleType type, uint battleFieldId, bool pause)
    {
        var code = Validate(type);

        if (code != 0)
            return code;

        if (Current is not {} running
            || running.Type != type
            || running.BattleFieldId != battleFieldId)
            return (int)EnmTextCode.EnmTextBattleStateNotMatch;

        if (running.Paused == pause)
            return 0;

        Current = running with { Paused = pause };
        return 0;
    }

    public BattleSettlement Leave(EBattleType type, uint battleFieldId, bool victory)
    {
        if (Current is not {} running)
            return new BattleSettlement(0, false, false, false);

        if (running.Type != type || running.BattleFieldId != battleFieldId)
        {
            return new BattleSettlement((int)EnmTextCode.EnmTextBattleStateNotMatch, false, false, false);
        }

        var begun = running.Started;
        Current = null;
        return new BattleSettlement(0, true, begun, begun && victory);
    }

    public void RecordKills(IEnumerable<long> monsters)
    {
        foreach (var monster in monsters)
        {
            _patrolCooldown.Add(monster);
        }
    }

    private static int Validate(EBattleType type)
    {
        var value = (int)type;
        return value is < MinType or > MaxType ? (int)EnmTextCode.EnmTextBattleTypeInvalid : 0;
    }
}
