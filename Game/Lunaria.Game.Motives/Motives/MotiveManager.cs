using Lunaria.Common;
using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Motives;

public sealed partial class MotiveManager(GameData assets)
{
    public const int MaxMotives = (int)EnmSizeLimit.MaxMotiveElemNum;

    private readonly SortedDictionary<ulong, MotiveState> _motives = [];

    public bool IsDirty { get; private set; }

    public IReadOnlyList<MotiveState> All => _motives.Values.ToList();
    public bool IsEmpty => _motives.Count == 0;
    public int Count => _motives.Count;

    public void Load(IEnumerable<MotiveState> persisted)
    {
        _motives.Clear();
        _changed.Clear();

        foreach (var motive in persisted
                     .Where(m => m.UniqId != 0 && assets.Motives.Exists(m.MotiveId))
                     .DistinctBy(m => m.UniqId)
                     .OrderBy(m => m.UniqId)
                     .Take(MaxMotives)
                     .Select(Clamp))
        {
            _motives[motive.UniqId] = motive;
        }
        IsDirty = false;
    }

    public MotiveGrant Add(GuidManager guid, uint motiveId, ulong claimTime)
    {
        if (!assets.Motives.Exists(motiveId))
            return MotiveGrant.Rejected((int)EnmTextCode.EnmTextMotiveConfInvalid);

        if (_motives.Count >= MaxMotives)
            return MotiveGrant.Rejected((int)EnmTextCode.EnmTextCountGroupLimit);

        var uniqId = guid.Next();

        _motives[uniqId] = new MotiveState {
            UniqId = uniqId,
            MotiveId = motiveId,
            ItemId = motiveId,
            ClaimTime = claimTime,
            Level = 1,
            Exp = 0,
            RefineLevel = 0,
            BreakLevel = 0,
            Locked = false,
            EquipedTarget = 0
        };
        IsDirty = true;
        MarkChanged(_motives[uniqId], isNew: true);
        return new MotiveGrant(Code: 0, uniqId);
    }

    public MotiveState? Get(ulong uniqId) => _motives.GetValueOrDefault(uniqId);

    public bool Owns(ulong uniqId) => _motives.ContainsKey(uniqId);

    public void ClearDirty() => IsDirty = false;

    private MotiveState Clamp(MotiveState motive)
    {
        var cap = assets.Motives.LevelCap(motive.MotiveId, motive.BreakLevel);
        var level = Math.Clamp(motive.Level, min: 1u, Math.Max(val1: 1u, cap));
        var itemId = motive.ItemId == 0 ? motive.MotiveId : motive.ItemId;

        if (level == motive.Level && itemId == motive.ItemId)
            return motive;

        return motive with { Level = level, ItemId = itemId };
    }

    public IReadOnlyList<CSMotiveElem> ListData() =>
        _motives.Values
            .Take(MaxMotives)
            .Select(ToMotiveElem)
            .ToList();

    public CSMotiveElem ToMotiveElem(MotiveState motive) => new() {
        UniqId = motive.UniqId,
        MotiveId = motive.MotiveId,
        ClaimTime = motive.ClaimTime,
        Lv = (int)motive.Level,
        Refinelv = (int)motive.RefineLevel,
        EquipedTarget = motive.EquipedTarget,
        Locked = motive.Locked,
        ItemId = motive.ItemId
    };

    public CmdMotiveData ToCmdMotiveData(MotiveState motive) => new() {
        MotiveUniqId = motive.UniqId,
        ClaimTime = motive.ClaimTime,
        Level = motive.Level,
        Refine = motive.RefineLevel,
        LockState = motive.Locked ? 1u : 0u,
        Exp = motive.Exp,
        BreakLevel = motive.BreakLevel
    };

    private void Replace(MotiveState motive)
    {
        if (!_motives.ContainsKey(motive.UniqId))
            return;

        _motives[motive.UniqId] = motive;
        IsDirty = true;
        MarkChanged(motive);
    }
}
