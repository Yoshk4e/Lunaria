using Lunaria.Common.Tracking;
using Lunaria.Game.Logging;
using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Motives;

public sealed partial class MotiveManager : TrackedObject
{
    public const int MaxRefineLevel = 5;

    private static readonly (uint ItemId, uint Exp)[] ExpDenominations = [
        (11201003, 10000),
        (11201002, 5000),
        (11201001, 2000)
    ];

    public uint LevelCap(ulong uniqId) =>
        Get(uniqId) is {} motive ? assets.Motives.LevelCap(motive.MotiveId, motive.BreakLevel) : 0;

    public uint MaxLevel(ulong uniqId) =>
        Get(uniqId) is {} motive ? assets.Motives.MaxLevel(motive.MotiveId) : 0;

    public MotiveExpResult GrantExp(ulong uniqId, uint exp)
    {
        if (Get(uniqId) is not {} motive)
            return MotiveExpResult.Rejected((int)EnmTextCode.EnmTextMotiveUidInvalid);

        if (exp == 0)
            return new MotiveExpResult(Code: 0, motive.Level, motive.Level, LevelsGained: 0, Dropped: 0);

        var cap = assets.Motives.LevelCap(motive.MotiveId, motive.BreakLevel);

        if (motive.Level >= cap)
        {
            var max = assets.Motives.MaxLevel(motive.MotiveId);
            var code = cap >= max ? (int)EnmTextCode.EnmTextMotiveLevelReachedMax : (int)EnmTextCode.EnmTextMotiveNeedBreak;
            return new MotiveExpResult(code, motive.Level, motive.Level, LevelsGained: 0, exp);
        }

        var pool = (ulong)motive.Exp + exp;
        var level = motive.Level;
        uint gained = 0;

        while (level < cap
               && assets.Motives.ExpToAdvance(motive.MotiveId, level) is {} need
               && need > 0
               && pool >= need)
        {
            pool -= need;
            level++;
            gained++;
        }

        var capped = level >= cap;
        var kept = capped ? 0 : (uint)pool;
        var dropped = capped ? (uint)pool : 0;

        Replace(motive with { Level = level, Exp = kept });
        Log.Stage("motive {UniqId} experience applied, level {PreviousLevel} to {Level}, kept experience {KeptExp}, dropped experience {DroppedExp}",
            uniqId, motive.Level, level, kept, dropped);

        return new MotiveExpResult(Code: 0, motive.Level, level, gained, dropped);
    }

    public BreakStep? NextBreak(ulong uniqId) =>
        Get(uniqId) is {} motive ? assets.Motives.NextBreak(motive.MotiveId, motive.BreakLevel) : null;

    public int CheckBreak(ulong uniqId, uint worldLevel)
    {
        if (Get(uniqId) is not {} motive)
            return (int)EnmTextCode.EnmTextMotiveUidInvalid;

        if (assets.Motives.NextBreak(motive.MotiveId, motive.BreakLevel) is not {} step)
            return (int)EnmTextCode.EnmTextMotiveBreakReachedMax;

        if (motive.Level < assets.Motives.LevelCap(motive.MotiveId, motive.BreakLevel))
            return (int)EnmTextCode.EnmTextMotiveNeedLevelUp;

        if (worldLevel < step.NeedWorldLevel)
            return (int)EnmTextCode.EnmTextMotiveBreakNeedWorldLevel;

        return 0;
    }

    public int ApplyBreak(ulong uniqId, uint worldLevel)
    {
        var code = CheckBreak(uniqId, worldLevel);

        if (code != 0)
            return code;

        var motive = Get(uniqId)!;
        var step = assets.Motives.NextBreak(motive.MotiveId, motive.BreakLevel)!;
        Replace(motive with { BreakLevel = step.BreakLevel });
        Log.Stage("motive {UniqId} advanced to break level {BreakLevel}", uniqId, step.BreakLevel);
        return 0;
    }

    public bool IsFullyBroken(ulong uniqId) =>
        Get(uniqId) is {} motive && assets.Motives.IsFullyBroken(motive.MotiveId, motive.BreakLevel);

    public uint ItemExpValue(uint itemId)
    {
        if (assets.Items.IsCurrency(itemId))
            return 0;

        var row = assets.Items.Get(itemId);

        if (row is null || row.Param.Count == 0)
            return 0;

        if (row.UseType == 7)
            return 0;

        if (row.ShowType != 4)
            return 0;

        return row.Param[0];
    }

    public uint InvestedExp(MotiveState motive)
    {
        ulong total = motive.Exp;

        for (uint level = 1; level < motive.Level; level++)
            if (assets.Motives.ExpToAdvance(motive.MotiveId, level) is {} need)
                total += need;
        return total > uint.MaxValue ? uint.MaxValue : (uint)total;
    }

    public uint FedValue(MotiveState motive)
    {
        var invested = InvestedExp(motive);

        if (invested > 0)
            return invested;

        return assets.Motives.ExpToAdvance(motive.MotiveId, level: 1) ?? 0;
    }

    /// <summary>Return the largest recycle materials first. Discard amounts below the smallest material.</summary>
    public IReadOnlyList<ItemGrant> ExpToMaterials(uint exp)
    {
        if (exp == 0)
            return [];

        var grants = new List<ItemGrant>();
        ulong rest = exp;

        foreach (var (itemId, value) in ExpDenominations)
        {
            if (rest < value)
                continue;

            var count = (uint)(rest / value);
            rest -= (ulong)count * value;
            grants.Add(new ItemGrant(itemId, count));
        }

        return grants;
    }

    public int ConsumeFeedsForLevel(ulong targetUniq, IReadOnlyList<ulong> feeds, out IReadOnlyList<MotiveState> consumed)
    {
        consumed = [];

        if (feeds.Count == 0)
            return 0;

        if (feeds.Distinct().Count() != feeds.Count)
            return (int)EnmTextCode.EnmTextMotiveRepeat;

        var states = new List<MotiveState>(feeds.Count);

        foreach (var uniq in feeds)
        {
            if (uniq == targetUniq)
                return (int)EnmTextCode.EnmTextMotiveLevelupUseSelf;

            if (Get(uniq) is not {} feed)
                return (int)EnmTextCode.EnmTextMotiveUidInvalid;

            if (feed.Locked)
                return (int)EnmTextCode.EnmTextMotiveLocked;

            if (feed.EquipedTarget != 0)
                return (int)EnmTextCode.EnmTextMotiveEquiped;

            states.Add(feed);
        }

        foreach (var state in states)
        {
            _motives.Remove(state.UniqId);
            MarkChanged(state, removed: true);
        }

        consumed = states;
        return 0;
    }

    public (int Code, uint OldRefine, uint NewRefine) RefineUp(ulong targetUniq, IReadOnlyList<ulong> feeds)
    {
        if (Get(targetUniq) is not {} target)
            return ((int)EnmTextCode.EnmTextMotiveUidInvalid, 0, 0);

        if (feeds.Count == 0)
            return ((int)EnmTextCode.EnmTextWrongParam, target.RefineLevel, target.RefineLevel);

        if (feeds.Distinct().Count() != feeds.Count)
            return ((int)EnmTextCode.EnmTextMotiveRepeat, target.RefineLevel, target.RefineLevel);

        if (feeds.Contains(targetUniq))
            return ((int)EnmTextCode.EnmTextMotiveRepeat, target.RefineLevel, target.RefineLevel);

        if (target.RefineLevel >= MaxRefineLevel)
            return ((int)EnmTextCode.EnmTextMotiveRefineMax, target.RefineLevel, target.RefineLevel);

        if (target.RefineLevel + (ulong)feeds.Count > MaxRefineLevel)
            return ((int)EnmTextCode.EnmTextMotiveRefineMax, target.RefineLevel, target.RefineLevel);

        var states = new List<MotiveState>(feeds.Count);

        foreach (var uniq in feeds)
        {
            if (Get(uniq) is not {} feed)
                return ((int)EnmTextCode.EnmTextMotiveUidInvalid, target.RefineLevel, target.RefineLevel);

            if (feed.MotiveId != target.MotiveId)
                return ((int)EnmTextCode.EnmTextMotiveConfInvalid, target.RefineLevel, target.RefineLevel);

            if (feed.Locked)
                return ((int)EnmTextCode.EnmTextMotiveLocked, target.RefineLevel, target.RefineLevel);

            if (feed.EquipedTarget != 0)
                return ((int)EnmTextCode.EnmTextMotiveEquiped, target.RefineLevel, target.RefineLevel);

            states.Add(feed);
        }

        foreach (var state in states)
        {
            _motives.Remove(state.UniqId);
            MarkChanged(state, removed: true);
        }
        var next = target.RefineLevel + (uint)feeds.Count;
        Replace(target with { RefineLevel = next });
        Log.Stage("motive {UniqId} refined from {PreviousLevel} to {Level}, consumed {FeedCount} motives", targetUniq, target.RefineLevel, next, feeds.Count);
        return (0, target.RefineLevel, next);
    }

    public (int Code, IReadOnlyList<MotiveState> Removed) Decompose(IReadOnlyList<ulong> uniqs)
    {
        if (uniqs.Count == 0)
            return ((int)EnmTextCode.EnmTextWrongParam, []);

        if (uniqs.Distinct().Count() != uniqs.Count)
            return ((int)EnmTextCode.EnmTextMotiveRepeat, []);

        var states = new List<MotiveState>(uniqs.Count);

        foreach (var uniq in uniqs)
        {
            if (Get(uniq) is not {} motive)
                return ((int)EnmTextCode.EnmTextMotiveUidInvalid, []);

            if (motive.Locked)
                return ((int)EnmTextCode.EnmTextMotiveLocked, []);

            if (motive.EquipedTarget != 0)
                return ((int)EnmTextCode.EnmTextMotiveEquiped, []);

            if (assets.Motives.Rarity(motive.MotiveId) >= 5)
                return ((int)EnmTextCode.EnmTextMotiveDecomposeSsr, []);

            states.Add(motive);
        }

        foreach (var state in states)
        {
            _motives.Remove(state.UniqId);
            MarkChanged(state, removed: true);
        }

        Log.Stage("motive decomposition removed {Count} motives", states.Count);
        return (0, states);
    }
}

public readonly record struct MotiveExpResult(
    int Code,
    uint OldLevel,
    uint NewLevel,
    uint LevelsGained,
    uint Dropped
)
{
    public bool Ok => Code == 0;
    public static MotiveExpResult Rejected(int code) => new(code, OldLevel: 0, NewLevel: 0, LevelsGained: 0, Dropped: 0);
}
