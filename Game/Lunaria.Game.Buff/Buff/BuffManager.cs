using Lunaria.Game.Resources;
using Lunaria.Game.Resources.Tables;
using Msg;

namespace Lunaria.Game.Buff;

public sealed record BuffState(uint BuffId, DateTimeOffset AttachedAt, int LeftBattle);

public sealed class BuffManager(GameData assets)
{
    private readonly SortedDictionary<uint, BuffState> _buffs = [];

    public bool IsDirty { get; private set; }

    public IReadOnlyDictionary<uint, BuffState> Buffs => _buffs;

    public void Load(IEnumerable<(uint BuffId, long AttachUnix, int LeftBattle)> persisted, DateTimeOffset now)
    {
        _buffs.Clear();

        foreach (var row in persisted)
        {
            var buff = assets.ItemEffects.Buff(row.BuffId);

            if (buff is null)
                continue;

            var attached = DateTimeOffset.FromUnixTimeSeconds(row.AttachUnix);

            if (RemainingTime(buff, attached, now) == 0 || buff.DurationBattle > 0 && row.LeftBattle <= 0)
                continue;

            _buffs[row.BuffId] = new BuffState(row.BuffId, attached, row.LeftBattle);
        }

        IsDirty = false;
    }

    public void ClearDirty() => IsDirty = false;

    public IReadOnlyList<PBBuffData> ToBuffData(DateTimeOffset now)
    {
        Sweep(now);

        return _buffs.Values
            .Select(state => ToPb(state, now))
            .ToList();
    }

    public (int Result, bool Refreshed, PBBuffData? Updated, IReadOnlyList<uint> Removed) Apply(
        uint buffId,
        DateTimeOffset now
    )
    {
        var buff = assets.ItemEffects.Buff(buffId);

        if (buff is null)
            return ((int)EnmTextCode.EnmTextBuffInvalid, false, null, []);

        var removed = Sweep(now).ToList();

        if (_buffs.TryGetValue(buffId, out var running))
        {
            if (!buff.RefreshOnReuse)
                return (0, true, ToPb(running, now), removed);

            _buffs[buffId] = new BuffState(buffId, now, buff.DurationBattle);
            Dirty();
            return (0, true, ToPb(_buffs[buffId], now), removed);
        }

        if (buff.MutexGroup != 0)
        {
            foreach (var other in _buffs.Values.ToList())
            {
                var sibling = assets.ItemEffects.Buff(other.BuffId);

                if (sibling is not null && sibling.MutexGroup == buff.MutexGroup)
                {
                    _buffs.Remove(other.BuffId);
                    removed.Add(other.BuffId);
                }
            }
        }

        _buffs[buffId] = new BuffState(buffId, now, buff.DurationBattle);
        Dirty();
        return (0, false, ToPb(_buffs[buffId], now), removed);
    }

    private PBBuffData ToPb(BuffState state, DateTimeOffset now)
    {
        var buff = assets.ItemEffects.Buff(state.BuffId)!;

        return new PBBuffData {
            Id = state.BuffId,
            AttachTime = (int)state.AttachedAt.ToUnixTimeSeconds(),
            LeftTime = RemainingTime(buff, state.AttachedAt, now),
            LeftBattle = state.LeftBattle
        };
    }

    public (IReadOnlyList<PBBuffData> Updated, IReadOnlyList<uint> Removed) BattleEnded(DateTimeOffset now)
    {
        var updated = new List<PBBuffData>();
        var removed = Sweep(now).ToList();

        foreach (var state in _buffs.Values.ToList())
        {
            var buff = assets.ItemEffects.Buff(state.BuffId);

            if (buff is null || buff.DurationBattle <= 0)
                continue;

            if (state.LeftBattle <= 1)
            {
                _buffs.Remove(state.BuffId);
                removed.Add(state.BuffId);
                Dirty();
            } else
            {
                var next = state with { LeftBattle = state.LeftBattle - 1 };
                _buffs[state.BuffId] = next;
                updated.Add(ToPb(next, now));
                Dirty();
            }
        }

        return (updated, removed);
    }

    /// <summary>Seconds remaining, 0 when expired, or -1 for unlimited buffs and buffs limited by battles.</summary>
    private static int RemainingTime(POutsideBuffTable buff, DateTimeOffset attached, DateTimeOffset now) =>
        buff.DurationTime switch {
            -1 => -1,
            <= 0 => 0,
            _ => (int)Math.Max(val1: 0, attached.AddSeconds(buff.DurationTime).ToUnixTimeSeconds() - now.ToUnixTimeSeconds())
        };

    public IReadOnlyList<uint> Sweep(DateTimeOffset now)
    {
        var removed = new List<uint>();
        foreach (var state in _buffs.Values.ToList())
        {
            var buff = assets.ItemEffects.Buff(state.BuffId);

            if (buff is null || RemainingTime(buff, state.AttachedAt, now) == 0)
            {
                _buffs.Remove(state.BuffId);
                removed.Add(state.BuffId);
                Dirty();
            }
        }
        return removed;
    }

    private void Dirty() => IsDirty = true;
}
