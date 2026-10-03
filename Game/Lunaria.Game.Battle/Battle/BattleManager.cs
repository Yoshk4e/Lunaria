using Lunaria.Common.Tracking;
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

public sealed partial class BattleManager : TrackedObject
{
    private static readonly ILogger Log = GameLog.Create("Lunaria.Game.Battle");

    private const int MinType = (int)EBattleType.EnmBattleTypeExpose;
    private const int MaxType = (int)EBattleType.EnmBattleTypeHorde;
    /// <summary>
    /// CBT1 patrol clusters carry no RefreshConfigId, so a defeated cluster comes back after this fixed delay.
    /// </summary>
    public static readonly TimeSpan PatrolRespawnDelay = TimeSpan.FromSeconds(300);

    // Static patrol cluster id (the TemplateID the client reports as battle_inst_id) to the end of its cooldown.
    private readonly TrackedSortedDictionary<long, DateTimeOffset> __tracked_patrolCooldown = [];
    [Tracked]
    private partial TrackedSortedDictionary<long, DateTimeOffset> _patrolCooldown { get; }

    [Untracked]
    public BattleSession? Current { get; private set; }

    public IReadOnlyCollection<long> PatrolCooldown => _patrolCooldown.Keys.ToArray();

    public IReadOnlyDictionary<long, DateTimeOffset> PatrolCooldownEnds => _patrolCooldown;

    public void LoadPatrolCooldowns(IEnumerable<(long Cluster, DateTimeOffset Until)> persisted)
    {
        _patrolCooldown.Clear();

        foreach (var (cluster, until) in persisted)
        {
            if (cluster > 0 && cluster <= uint.MaxValue)
                _patrolCooldown[cluster] = until;
        }

        AcceptLoadedState();
    }

    public bool ResetMonster(long monsterId)
    {
        if (!_patrolCooldown.Remove(monsterId))
            return false;

        return true;
    }

    public void StartPatrolCooldown(long cluster, DateTimeOffset until)
    {
        _patrolCooldown[cluster] = until;
    }

    /// <summary>Clusters whose cooldown ended by <paramref name="now"/>, removed from the cooldown list.</summary>
    public IReadOnlyList<long> ExpirePatrolCooldowns(DateTimeOffset now)
    {
        var expired = _patrolCooldown.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToList();

        foreach (var cluster in expired)
        {
            _patrolCooldown.Remove(cluster);
        }

        return expired;
    }

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

    private static int Validate(EBattleType type)
    {
        var value = (int)type;
        if (value is < MinType or > MaxType)
            Log.Stage("battle request refused for unsupported type {BattleType}", value);
        return value is < MinType or > MaxType ? (int)EnmTextCode.EnmTextBattleTypeInvalid : 0;
    }
}
