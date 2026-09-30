using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Player.Managers;

public sealed record BattlePassState(uint Level, uint Exp, uint AwardLevel);

public sealed class BattlePassManager(GameData assets)
{
    private readonly SortedDictionary<uint, BattlePassState> _passes = [];

    public bool IsDirty { get; private set; }

    public IReadOnlyDictionary<uint, BattlePassState> Passes => _passes;

    public void Load(IEnumerable<(uint PassId, uint Level, uint Exp, uint AwardLevel)> persisted)
    {
        _passes.Clear();

        foreach (var row in persisted)
        {
            if (!assets.BattlePasses.Exists(row.PassId))
                continue;

            var maxLevel = assets.BattlePasses.MaxLevel(row.PassId);
            var level = Math.Clamp(row.Level, min: 1, Math.Max(maxLevel, val2: 1));
            var expNeed = assets.BattlePasses.ExpNeed(row.PassId, level + 1);

            _passes[row.PassId] = new BattlePassState(
                level,
                Math.Min(row.Exp, expNeed),
                Math.Min(row.AwardLevel, level));
        }

        IsDirty = false;
    }

    public void ClearDirty() => IsDirty = false;

    public SCBattlePassData ToBattlePassData(IReadOnlyList<uint> passIds)
    {
        if (passIds.Any(id => !Exists(id)))
            return new SCBattlePassData { Result = (int)EnmTextCode.EnmTextBattlePassNotExist };

        var data = new SCBattlePassData { Result = 0 };

        foreach (var passId in passIds)
        {
            data.Datas.Add(ToCmdOne(passId, StateOf(passId)));
        }
        return data;
    }

    public CmdOneBattelPassData? NotificationOf(uint passId)
    {
        if (!_passes.TryGetValue(passId, out var state))
            return null;

        return ToCmdOne(passId, state);
    }

    public (bool Changed, CmdOneBattelPassData Data) AddExp(uint passId, uint exp)
    {
        if (!assets.BattlePasses.Exists(passId) || exp == 0)
            return (false, ToCmdOne(passId, _passes.GetValueOrDefault(passId) ?? new BattlePassState(1, 0, 0)));

        var state = StateOf(passId);
        var maxLevel = assets.BattlePasses.MaxLevel(passId);
        var level = state.Level;
        var remaining = (ulong)state.Exp + exp;

        // expNeed is the XP cost to advance from this level.
        var cost = assets.BattlePasses.ExpNeed(passId, level + 1);

        while (level < maxLevel && remaining >= cost && cost > 0)
        {
            remaining -= cost;
            level++;
            cost = assets.BattlePasses.ExpNeed(passId, level + 1);
        }

        if (level >= maxLevel)
            remaining = 0;

        if (level == state.Level && remaining == state.Exp)
            return (false, ToCmdOne(passId, state));

        state = new BattlePassState(level, (uint)Math.Min(uint.MaxValue, remaining), state.AwardLevel);
        _passes[passId] = state;
        Dirty();
        return (true, ToCmdOne(passId, state));
    }

    public IReadOnlyList<uint> ClaimableLevels(uint passId)
    {
        if (!Exists(passId)) return [];
        var state = StateOf(passId);
        var first = state.AwardLevel + 1;

        return [
            .. Enumerable.Range((int)first, (int)Math.Max(val1: 0, (long)state.Level - first + 1))
                .Select(level => (uint)level)
        ];
    }

    public void MarkAwardClaimed(uint passId, uint awardLevel)
    {
        if (!Exists(passId)) return;
        var state = _passes.GetValueOrDefault(passId) ?? new BattlePassState(1, 0, 0);

        if (awardLevel <= state.AwardLevel || awardLevel > state.Level)
            return;

        _passes[passId] = state with { AwardLevel = awardLevel };
        Dirty();
    }

    public IReadOnlyList<ItemGrant> AwardOf(uint passId, uint level) =>
        assets.BattlePasses.Award(passId, level);

    public bool Exists(uint passId) => assets.BattlePasses.Exists(passId);

    private BattlePassState StateOf(uint passId)
    {
        if (_passes.TryGetValue(passId, out var state))
            return state;

        state = new BattlePassState(Level: 1, Exp: 0, AwardLevel: 0);

        if (assets.BattlePasses.Exists(passId))
        {
            _passes[passId] = state;
            Dirty();
        }
        return state;
    }

    private static CmdOneBattelPassData ToCmdOne(uint passId, BattlePassState state) => new() {
        Id = passId,
        Level = state.Level,
        Exp = state.Exp,
        AwardLv = state.AwardLevel
    };

    private void Dirty() => IsDirty = true;
}
