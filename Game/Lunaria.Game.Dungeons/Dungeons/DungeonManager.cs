using System.Globalization;
using Lunaria.Game.Resources;
using Lunaria.Game.Resources.Tables;
using Msg;

namespace Lunaria.Game.Dungeons;

public sealed record DungeonTypeState(uint TypeId, uint UsedDay, uint UsedWeek, DateTimeOffset Anchor);

public sealed record HordeState(uint HordeId, uint KillCount, uint StarAward);

public sealed class DungeonManager(GameData assets)
{
    private readonly SortedDictionary<ulong, uint> _finishes = [];
    private readonly SortedDictionary<uint, HordeState> _hordes = [];
    private readonly Dictionary<uint, DungeonTypeState> _types = [];

    private (ulong DungeonId, uint BattleId)? _current;

    public bool IsDirty { get; private set; }

    public IReadOnlyDictionary<ulong, uint> Finishes => _finishes;

    public IReadOnlyDictionary<uint, DungeonTypeState> Types => _types;

    public IReadOnlyDictionary<uint, HordeState> Hordes => _hordes;

    public (ulong DungeonId, uint BattleId)? Current => _current;

    public void Load(
        IEnumerable<(ulong DungeonId, uint Count)> finishes,
        IEnumerable<(uint TypeId, uint UsedDay, uint UsedWeek, long AnchorUnix)> types,
        IEnumerable<(uint HordeId, uint KillCount, uint StarAward)> hordes,
        (ulong DungeonId, uint BattleId)? current,
        DateTimeOffset now
    )
    {
        _finishes.Clear();
        _types.Clear();
        _hordes.Clear();
        _current = null;

        foreach (var (dungeonId, count) in finishes)
        {
            if (assets.Dungeons.Dungeon(dungeonId) is not null)
                _finishes[dungeonId] = count;
        }

        foreach (var (typeId, usedDay, usedWeek, anchorUnix) in types)
        {
            if (assets.Dungeons.Type(typeId) is null)
                continue;

            _types[typeId] = new DungeonTypeState(typeId, usedDay, usedWeek, DateTimeOffset.FromUnixTimeSeconds(anchorUnix));
            RollOver(_types[typeId], now);
        }

        foreach (var (hordeId, killCount, starAward) in hordes)
        {
            if (assets.Dungeons.Horde(hordeId) is not null)
                _hordes[hordeId] = new HordeState(hordeId, killCount, starAward);
        }

        if (current is {} run && assets.Dungeons.Dungeon(run.DungeonId) is not null)
            _current = run;

        IsDirty = false;
    }

    public void ClearDirty() => IsDirty = false;

    public (uint CountDay, uint CountWeek) RemainingAttempts(uint typeId, DateTimeOffset now)
    {
        var type = assets.Dungeons.Type(typeId);

        if (type is null)
            return (0, 0);

        var state = StateOf(typeId, now);

        return type.LimitType switch {
            DungeonAssets.LimitDay => (Math.Max(val1: 0, type.LimitParam - state.UsedDay), 0u),
            DungeonAssets.LimitWeek => (0u, Math.Max(val1: 0, type.LimitParam - state.UsedWeek)),
            _ => (0, 0)
        };
    }

    public int CheckEnter(ulong dungeonId, DateTimeOffset now)
    {
        if (assets.Dungeons.Dungeon(dungeonId) is not {} dungeon)
            return (int)EnmTextCode.EnmTextDungeonsFail;

        if (_current is not null)
            return (int)EnmTextCode.EnmTextDungeonsIn;

        if (assets.Dungeons.Type(dungeon.DungeonType) is { LimitType: not DungeonAssets.LimitNone } type)
        {
            var (day, week) = RemainingAttempts(dungeon.DungeonType, now);

            if (type.LimitType == DungeonAssets.LimitDay && day == 0)
                return (int)EnmTextCode.EnmTextDungeonsCountMax;

            if (type.LimitType == DungeonAssets.LimitWeek && week == 0)
                return (int)EnmTextCode.EnmTextDungeonsCountMax;
        }

        return 0;
    }

    public void Enter(ulong dungeonId, DateTimeOffset now)
    {
        var dungeon = assets.Dungeons.Dungeon(dungeonId)!;
        _current = (dungeonId, dungeon.BattleId.FirstOrDefault());
        Dirty();
    }

    public bool IsCurrent(ulong dungeonId) => _current is {} current && current.DungeonId == dungeonId;

    /// <summary>Resuming is free. A true result means a new run that the caller must charge for.</summary>
    public (int Result, bool Opened) AdoptCurrent(ulong dungeonId, uint battleId, DateTimeOffset now)
    {
        if (assets.Dungeons.Dungeon(dungeonId) is null)
            return ((int)EnmTextCode.EnmTextDungeonsFail, false);

        if (_current is {} current)
            return current.DungeonId == dungeonId ? (0, false) : ((int)EnmTextCode.EnmTextDungeonsIn, false);

        var code = CheckEnter(dungeonId, now);

        if (code != 0)
            return (code, false);

        _current = (dungeonId, battleId);
        Dirty();
        return (0, true);
    }

    public void AbandonCurrent()
    {
        _current = null;
        Dirty();
    }

    public DungeonSettlement Finish(
        ulong dungeonId,
        bool victory,
        bool leave,
        uint hordeKills,
        Random random,
        DateTimeOffset now
    )
    {
        if (assets.Dungeons.Dungeon(dungeonId) is not {} dungeon)
            return DungeonSettlement.Rejected((int)EnmTextCode.EnmTextDungeonsFail);

        if (_current is not {} current || current.DungeonId != dungeonId)
            return DungeonSettlement.Rejected((int)EnmTextCode.EnmTextDungeonsNotIn);

        var settled = !leave;

        if (settled && !CanConsumeAttempt(dungeon, now))
            return DungeonSettlement.Rejected((int)EnmTextCode.EnmTextDungeonsCountMax);

        _current = null;
        var rewards = new List<ItemGrant>();

        if (settled)
        {
            ConsumeAttempt(dungeon.DungeonType, now);

            if (victory)
            {
                _finishes[dungeonId] = _finishes.GetValueOrDefault(dungeonId) + 1;
                rewards.AddRange(assets.DropTable.Roll(dungeon.RewardDrop, random));
            }
        }

        HordeState? horde = null;

        if (settled && assets.Dungeons.Horde((uint)dungeonId) is {} hordeRow)
        {
            var state = _hordes.GetValueOrDefault((uint)dungeonId) ?? new HordeState((uint)dungeonId, KillCount: 0, StarAward: 0);
            var stars = StarsFor(hordeRow, hordeKills);

            for (var star = state.StarAward + 1; star <= stars; star++)
            {
                var index = (int)star - 1;

                if (index < hordeRow.FirstDrop.Count)
                    rewards.AddRange(assets.DropTable.Roll(hordeRow.FirstDrop[index], random));
            }

            if (stars > 0 && stars <= state.StarAward)
            {
                var index = (int)stars - 1;

                if (index < hordeRow.CommonDrop.Count)
                    rewards.AddRange(assets.DropTable.Roll(hordeRow.CommonDrop[index], random));
            }

            state = state with { KillCount = Math.Max(state.KillCount, hordeKills), StarAward = Math.Max(state.StarAward, stars) };
            _hordes[(uint)dungeonId] = state;
            horde = state;
        }

        Dirty();
        return new DungeonSettlement(0, settled, settled && victory, rewards, horde);
    }

    public CSDungeonsData ToFullData(DateTimeOffset now)
    {
        var data = new CSDungeonsData {
            CommonData = new CSDungeonsCommonData(),
            HordeData = new CSHordeData()
        };

        foreach (var type in assets.Dungeons.Types)
        {
            var (day, week) = RemainingAttempts(type.Id, now);

            data.CommonData.TypeData.Add(new CSDungeonsTypeData {
                Type = type.Id,
                CountDay = day,
                CountWeek = week
            });
        }

        foreach (var (dungeonId, count) in _finishes)
        {
            data.CommonData.DungeonsList.Add(new CSOneDungeonsFinishData {
                Id = dungeonId,
                Count = count
            });
        }

        foreach (var horde in _hordes.Values)
        {
            data.HordeData.HordeList.Add(new CSOneHordeData {
                Id = horde.HordeId,
                KillCount = horde.KillCount,
                StarAward = horde.StarAward
            });
        }

        return data;
    }

    public SCDungeonsDataNtf ToDataNotification(DateTimeOffset now)
    {
        var ntf = new SCDungeonsDataNtf();

        foreach (var type in assets.Dungeons.Types)
        {
            var (day, week) = RemainingAttempts(type.Id, now);

            ntf.Info.Add(new CSDungeonsTypeData {
                Type = type.Id,
                CountDay = day,
                CountWeek = week
            });
        }

        foreach (var (dungeonId, count) in _finishes)
        {
            ntf.FinishCount.Add(new CSOneDungeonsFinishData {
                Id = dungeonId,
                Count = count
            });
        }

        return ntf;
    }

    private static uint StarsFor(PHordeTable horde, uint kills)
    {
        uint stars = 0;

        foreach (var threshold in horde.KillConditon)
        {
            if (kills >= threshold)
                stars++;
        }
        return stars;
    }

    private bool CanConsumeAttempt(PRepeatableDungeonsTable dungeon, DateTimeOffset now)
    {
        if (assets.Dungeons.Type(dungeon.DungeonType) is not { LimitType: not DungeonAssets.LimitNone } type)
            return true;

        var (day, week) = RemainingAttempts(dungeon.DungeonType, now);

        return type.LimitType switch {
            DungeonAssets.LimitDay => day > 0,
            DungeonAssets.LimitWeek => week > 0,
            _ => true
        };
    }

    private void ConsumeAttempt(uint typeId, DateTimeOffset now)
    {
        var state = StateOf(typeId, now);
        _types[typeId] = state with { UsedDay = state.UsedDay + 1, UsedWeek = state.UsedWeek + 1 };
        Dirty();
    }

    private DungeonTypeState StateOf(uint typeId, DateTimeOffset now)
    {
        if (_types.TryGetValue(typeId, out var state))
        {
            RollOver(state, now);
            return _types[typeId];
        }

        state = new DungeonTypeState(typeId, UsedDay: 0, UsedWeek: 0, now);
        _types[typeId] = state;
        return state;
    }

    private void RollOver(DungeonTypeState state, DateTimeOffset now)
    {
        var newDay = now.UtcDateTime.Date > state.Anchor.UtcDateTime.Date;
        var newWeek = WeekOf(now) > WeekOf(state.Anchor);

        if (!newDay && !newWeek)
            return;

        _types[state.TypeId] = state with {
            UsedDay = newDay ? 0 : state.UsedDay,
            UsedWeek = newWeek ? 0 : state.UsedWeek,
            Anchor = now
        };
        Dirty();
    }

    private static int WeekOf(DateTimeOffset moment)
    {
        var date = moment.UtcDateTime;
        return ISOWeek.GetYear(date) * 100 + ISOWeek.GetWeekOfYear(date);
    }

    private void Dirty() => IsDirty = true;
}
