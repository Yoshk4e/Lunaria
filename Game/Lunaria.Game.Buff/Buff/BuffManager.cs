using Lunaria.Common.Tracking;
using Lunaria.Game.Logging;
using Lunaria.Game.Resources.Tables;
using Lunaria.Game.Resources;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.Game.Buff;

public sealed record BuffState(uint BuffId, DateTimeOffset AttachedAt, int LeftBattle);

public sealed partial class BuffManager(GameData assets) : TrackedObject
{
    private static readonly ILogger Log = GameLog.Create("Lunaria.Game.Buff");

    private readonly TrackedSortedDictionary<uint, BuffState> __tracked_buffs = [];
    [Tracked]
    private partial TrackedSortedDictionary<uint, BuffState> _buffs { get; }

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

        AcceptLoadedState();
    }

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

            Log.Event("buff {BuffId} refreshed with {Battles} battles remaining", buffId, buff.DurationBattle);
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

        Log.Event("buff {BuffId} applied, mutex group {MutexGroup}, removed {RemovedCount} buffs", buffId, buff.MutexGroup, removed.Count);
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

            } else
            {
                var next = state with { LeftBattle = state.LeftBattle - 1 };
                _buffs[state.BuffId] = next;
                updated.Add(ToPb(next, now));

            }
        }

        if (updated.Count > 0 || removed.Count > 0)
            Log.Event("battle buff settlement updated {UpdatedCount} and removed {RemovedCount} buffs", updated.Count, removed.Count);
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

            }
        }
        if (removed.Count > 0)
            Log.Event("buff sweep removed {Count} expired or unavailable buffs", removed.Count);
        return removed;
    }

}
