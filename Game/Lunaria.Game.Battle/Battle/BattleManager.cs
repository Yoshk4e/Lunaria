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
    /// <summary>
    /// CBT1 patrol clusters carry no RefreshConfigId, so a defeated cluster comes back after this fixed delay.
    /// </summary>
    public static readonly TimeSpan PatrolRespawnDelay = TimeSpan.FromSeconds(300);

    // Static patrol cluster id (the TemplateID the client reports as battle_inst_id) to the end of its cooldown.
    private readonly SortedDictionary<long, DateTimeOffset> _patrolCooldown = [];

    public BattleSession? Current { get; private set; }

    public bool IsDirty { get; private set; }

    public IReadOnlyCollection<long> PatrolCooldown => _patrolCooldown.Keys;

    public IReadOnlyDictionary<long, DateTimeOffset> PatrolCooldownEnds => _patrolCooldown;

    public void ClearDirty() => IsDirty = false;

    public void LoadPatrolCooldowns(IEnumerable<(long Cluster, DateTimeOffset Until)> persisted)
    {
        _patrolCooldown.Clear();

        foreach (var (cluster, until) in persisted)
        {
            if (cluster > 0 && cluster <= uint.MaxValue)
                _patrolCooldown[cluster] = until;
        }

        IsDirty = false;
    }

    public bool ResetMonster(long monsterId)
    {
        if (!_patrolCooldown.Remove(monsterId))
            return false;

        IsDirty = true;
        return true;
    }

    public void StartPatrolCooldown(long cluster, DateTimeOffset until)
    {
        _patrolCooldown[cluster] = until;
        IsDirty = true;
    }

    /// <summary>Clusters whose cooldown ended by <paramref name="now"/>, removed from the cooldown list.</summary>
    public IReadOnlyList<long> ExpirePatrolCooldowns(DateTimeOffset now)
    {
        var expired = _patrolCooldown.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToList();

        foreach (var cluster in expired)
        {
            _patrolCooldown.Remove(cluster);
        }

        if (expired.Count > 0) IsDirty = true;
        return expired;
    }

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

    private static int Validate(EBattleType type)
    {
        var value = (int)type;
        return value is < MinType or > MaxType ? (int)EnmTextCode.EnmTextBattleTypeInvalid : 0;
    }
}
