using Lunaria.Game.Logging;
using Microsoft.Extensions.Logging;
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
    private static readonly ILogger Log = GameLog.Create("Lunaria.Game.Battle");

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
        {
            Log.Stage("battle entry refused for type {BattleType} field {BattleFieldId}, unknown monster source {MonsterFrom}", type, battleFieldId, monsterFrom);
            return (int)EnmTextCode.EnmTextBattleStateNotMatch;
        }

        if (Current is {} running)
        {
            var same = running.Type == type && running.BattleFieldId == battleFieldId
                && running.BattleInstId == battleInstId && running.MonsterFrom == monsterFrom;
            if (!same)
                Log.Stage("battle entry refused for type {BattleType} field {BattleFieldId} instance {BattleInstId} source {MonsterFrom}, active type {ActiveType} field {ActiveFieldId} instance {ActiveInstId} source {ActiveMonsterFrom}",
                    type, battleFieldId, battleInstId, monsterFrom, running.Type, running.BattleFieldId, running.BattleInstId, running.MonsterFrom);
            return same ? 0 : (int)EnmTextCode.EnmTextBattleAleardyExist;
        }

        Current = new BattleSession(type, battleFieldId, battleInstId, monsterFrom, Started: false, Paused: false);
        Log.State("battle entered with type {BattleType} field {BattleFieldId} instance {BattleInstId} source {MonsterFrom}",
            type, battleFieldId, battleInstId, monsterFrom);
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
        {
            Log.Stage("battle start refused for type {BattleType} field {BattleFieldId}, active type {ActiveType} field {ActiveFieldId}",
                type, battleFieldId, Current?.Type, Current?.BattleFieldId);
            return (int)EnmTextCode.EnmTextBattleStateNotMatch;
        }

        if (running.Started)
            return 0;

        Current = running with { Started = true };
        Log.Stage("battle started with type {BattleType} field {BattleFieldId} instance {BattleInstId}", type, battleFieldId, running.BattleInstId);
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
        {
            Log.Stage("battle pause refused for type {BattleType} field {BattleFieldId}, active type {ActiveType} field {ActiveFieldId}",
                type, battleFieldId, Current?.Type, Current?.BattleFieldId);
            return (int)EnmTextCode.EnmTextBattleStateNotMatch;
        }

        if (running.Paused == pause)
            return 0;

        Current = running with { Paused = pause };
        Log.Stage("battle pause changed to {Paused} for type {BattleType} field {BattleFieldId} instance {BattleInstId}", pause, type, battleFieldId, running.BattleInstId);
        return 0;
    }

    public BattleSettlement Leave(EBattleType type, uint battleFieldId, bool victory)
    {
        if (Current is not {} running)
            return new BattleSettlement(0, false, false, false);

        if (running.Type != type || running.BattleFieldId != battleFieldId)
        {
            Log.Stage("battle settlement refused for type {BattleType} field {BattleFieldId}, active type {ActiveType} field {ActiveFieldId}",
                type, battleFieldId, running.Type, running.BattleFieldId);
            return new BattleSettlement((int)EnmTextCode.EnmTextBattleStateNotMatch, false, false, false);
        }

        var begun = running.Started;
        Current = null;
        Log.State("battle settled with type {BattleType} field {BattleFieldId} instance {BattleInstId}, started {Started}, requested victory {RequestedVictory}, accepted victory {Victory}",
            type, battleFieldId, running.BattleInstId, begun, victory, begun && victory);
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
        if (value is < MinType or > MaxType)
            Log.Stage("battle request refused for unsupported type {BattleType}", value);
        return value is < MinType or > MaxType ? (int)EnmTextCode.EnmTextBattleTypeInvalid : 0;
    }
}
