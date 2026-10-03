using Lunaria.Common.Tracking;
using Lunaria.Game.Logging;
using Lunaria.Game.Resources.Tables;
using Lunaria.Game.Resources;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.Game.Player.Managers;

public sealed record SilverCreatureState(uint UniqId, uint ItemId, uint Level);

public sealed partial class SilverCreatureManager(GameData assets) : TrackedObject
{
    private static readonly ILogger Log = GameLog.Create("Lunaria.Game.Player.SilverCreature");

    private readonly TrackedSortedDictionary<uint, SilverCreatureState> __tracked_creatures = [];
    [Tracked]
    private partial TrackedSortedDictionary<uint, SilverCreatureState> _creatures { get; }

    private uint __trackedLastMinted = default!;
    [Tracked]
    public partial uint LastMinted { get; private set; }

    public IReadOnlyDictionary<uint, SilverCreatureState> Creatures => _creatures;

    private uint __trackedInBattleUniqId = default!;
    [Tracked]
    public partial uint InBattleUniqId { get; private set; }

    public uint TotalCost => (uint)Math.Min(uint.MaxValue,
        _creatures.Values.Sum(creature => (long)(GrowthOf(creature.ItemId)?.Cost ?? 0)));

    public void Load(IEnumerable<(uint UniqId, uint ItemId, uint Level)> persisted, uint inBattleUniqId, uint lastMinted = 0)
    {
        _creatures.Clear();
        LastMinted = lastMinted;
        InBattleUniqId = 0;

        foreach (var row in persisted)
        {
            if (row.UniqId == 0 || !IsCreatureItem(row.ItemId))
                continue;

            if (_creatures.ContainsKey(row.UniqId))
                continue;

            _creatures[row.UniqId] = new SilverCreatureState(row.UniqId, row.ItemId, row.Level);
            LastMinted = Math.Max(LastMinted, row.UniqId);
        }

        if (_creatures.ContainsKey(inBattleUniqId))
            InBattleUniqId = inBattleUniqId;

        AcceptLoadedState();
    }

    public SCSilverCreatureList ToList()
    {
        var reply = new SCSilverCreatureList { Result = 0, InBattleUniqId = InBattleUniqId };

        foreach (var creature in _creatures.Values)
        {
            reply.ItemList.Add(ToCmdItem(creature));
        }
        return reply;
    }

    public (int Result, SilverCreatureChange? Change) Combine(IReadOnlyList<uint> uniqIds)
    {
        if (uniqIds.Count == 0)
            return ((int)EnmTextCode.EnmTextCannotCombine, null);

        var sources = new List<SilverCreatureState>();

        foreach (var uniqId in uniqIds)
        {
            if (!_creatures.TryGetValue(uniqId, out var creature))
                return ((int)EnmTextCode.EnmTextSilverCreatureNotFound, null);

            sources.Add(creature);
        }

        if (sources.Select(creature => creature.UniqId).Distinct().Count() != sources.Count)
            return ((int)EnmTextCode.EnmTextCannotCombine, null);

        var itemId = sources[0].ItemId;

        if (sources.Any(creature => creature.ItemId != itemId))
            return ((int)EnmTextCode.EnmTextCannotCombine, null);

        var combine = assets.SilverCreatures.Combine(itemId);

        if (combine is null || combine.SrcItemNum != sources.Count)
            return ((int)EnmTextCode.EnmTextCannotCombine, null);

        if (!IsCreatureItem(combine.DstItemId) || !TryMintUniq(out var newUniq))
            return ((int)EnmTextCode.EnmTextCannotCombine, null);
        var level = sources.Max(creature => creature.Level);
        var fieldedRemoved = sources.Any(creature => creature.UniqId == InBattleUniqId);

        foreach (var creature in sources)
        {
            _creatures.Remove(creature.UniqId);
        }
        _creatures[newUniq] = new SilverCreatureState(newUniq, combine.DstItemId, level);

        if (fieldedRemoved)
            InBattleUniqId = newUniq;

        Log.Stage("silver creature combination consumed {Count} copies of item {ItemId}, created instance {UniqId} item {NewItemId}, active instance {InBattleUniqId}",
            sources.Count, itemId, newUniq, combine.DstItemId, InBattleUniqId);
        return (0, Change(sources.Select(creature => creature.UniqId).ToList(), _creatures[newUniq]));
    }

    public (int Result, SilverCreatureChange? Change) Release(IReadOnlyList<uint> uniqIds)
    {
        if (uniqIds.Count == 0)
            return ((int)EnmTextCode.EnmTextSilverCreatureNotFound, null);

        var removed = new List<uint>();

        foreach (var uniqId in uniqIds.Distinct())
        {
            if (!_creatures.ContainsKey(uniqId))
                return ((int)EnmTextCode.EnmTextSilverCreatureNotFound, null);

            removed.Add(uniqId);
        }

        foreach (var uniqId in removed)
        {
            _creatures.Remove(uniqId);
        }

        if (removed.Contains(InBattleUniqId))
            InBattleUniqId = 0;

        Log.Stage("silver creature release removed {Count} instances, active instance {InBattleUniqId}", removed.Count, InBattleUniqId);
        return (0, Change(removed, add: null));
    }

    public (int Result, uint UniqId) SetInBattle(uint uniqId)
    {
        if (!_creatures.ContainsKey(uniqId))
            return ((int)EnmTextCode.EnmTextSilverCreatureNotFound, 0);

        if (InBattleUniqId != uniqId)
        {
            InBattleUniqId = uniqId;

        }
        return (0, uniqId);
    }

    public (int Result, uint UniqId) SetLeaveBattle(uint uniqId)
    {
        if (InBattleUniqId == 0 || InBattleUniqId != uniqId)
            return ((int)EnmTextCode.EnmTextSilverCreatureNotFound, 0);

        InBattleUniqId = 0;

        return (0, uniqId);
    }

    /// <summary>
    /// If a cap blocks collection, keep the item in the bag and send SC_SILVER_CREATURE_COLLECT_FAIL.
    /// </summary>
    public (bool Collected, CmdSilverCreatureItem? Added) TryCollect(uint itemId)
    {
        if (!IsCreatureItem(itemId))
            return (false, null);

        var growth = GrowthOf(itemId);
        var cost = growth?.Cost ?? 0;

        if (_creatures.Count >= assets.GlobalConfig.MaxSilverCreatureNum)
        {
            Log.Stage("silver creature collection refused for item {ItemId}, count {Count} at cap {Cap}", itemId, _creatures.Count, assets.GlobalConfig.MaxSilverCreatureNum);
            return (false, null);
        }

        if ((long)TotalCost + cost > assets.GlobalConfig.MaxSilverCreatureCost)
        {
            Log.Stage("silver creature collection refused for item {ItemId}, added cost {Cost}, current cost {TotalCost}, cap {Cap}",
                itemId, cost, TotalCost, assets.GlobalConfig.MaxSilverCreatureCost);
            return (false, null);
        }

        if (!TryMintUniq(out var uniqId)) return (false, null);
        _creatures[uniqId] = new SilverCreatureState(uniqId, itemId, growth?.Level ?? 1);

        return (true, ToCmdItem(_creatures[uniqId]));
    }

    private PSilverCreatureGrowthTable? GrowthOf(uint itemId)
    {
        var param = assets.Items.Get(itemId)?.Param;
        return param is { Count: > 0 } ? assets.SilverCreatures.Growth(param[0]) : null;
    }

    public int CountOfGrowth(uint growthId) =>
        _creatures.Values.Count(creature => GrowthOf(creature.ItemId)?.Id == growthId);

    private SilverCreatureChange Change(IReadOnlyList<uint> remove, SilverCreatureState? add)
    {
        var change = new SilverCreatureChange();
        change.RemoveList.AddRange(remove);

        if (add is not null)
            change.AddList.Add(ToCmdItem(add));
        return change;
    }

    private CmdSilverCreatureItem ToCmdItem(SilverCreatureState creature) => new() {
        ItemId = creature.ItemId,
        UniqId = creature.UniqId,
        SilverCreatureData = new CmdSilverCreatureData { Level = creature.Level }
    };

    private bool IsCreatureItem(uint itemId) =>
        assets.Items.Get(itemId) is {} row && (ItemUseType)row.UseType == ItemUseType.AddSilverCreature;

    /// <summary>Keep the highest allocated ID after all creatures are released to prevent ID reuse.</summary>
    private bool TryMintUniq(out uint id)
    {
        LastMinted = Math.Max(LastMinted, _creatures.Keys.DefaultIfEmpty().Max());
        id = 0;
        if (LastMinted == uint.MaxValue)
        {
            Log.Flag("silver creature instance allocation failed, ID space exhausted at {LastMinted}", LastMinted);
            return false;
        }
        id = ++LastMinted;
        return true;
    }

}
