using Lunaria.Common.Tracking;
using Lunaria.Game.Logging;
using Lunaria.Game.Resources.Tables;
using Lunaria.Game.Resources;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.Game.Player.Managers;

public sealed record SubRegionProgressState(
    ulong SubRegionId,
    IReadOnlyDictionary<uint, uint> Sequences,
    IReadOnlySet<uint> ClaimedValues
);

public sealed partial class RegionProgressManager(GameData assets) : TrackedObject
{
    private static readonly ILogger Log = GameLog.Create("Lunaria.Game.Player.RegionProgress");

    private static readonly SortedSet<uint> EmptyValues = [];
    private readonly TrackedSortedDictionary<ulong, SubRegionProgressState> __tracked_subregions = [];
    [Tracked]
    private partial TrackedSortedDictionary<ulong, SubRegionProgressState> _subregions { get; }
    [Untracked]
    private readonly List<ulong> _unlocked = [];

    public IReadOnlyDictionary<ulong, SubRegionProgressState> Subregions => _subregions;

    public void Load(
        IEnumerable<(ulong SubRegionId, IEnumerable<(uint SequenceId, uint Count)> Sequences, IEnumerable<uint> ClaimedValues)> persisted
    )
    {
        _subregions.Clear();
        _unlocked.Clear();

        foreach (var row in persisted)
        {
            if (!assets.RegionProgress.SubRegionExists(row.SubRegionId))
                continue;

            var sequences = new SortedDictionary<uint, uint>();

            foreach (var (sequenceId, count) in row.Sequences)
            {
                if (assets.RegionProgress.Sequences(row.SubRegionId).Contains(sequenceId))
                    sequences[sequenceId] = count;
            }

            var claimed = new SortedSet<uint>(row.ClaimedValues);
            _subregions[row.SubRegionId] = new SubRegionProgressState(row.SubRegionId, sequences, claimed);
        }

        AcceptLoadedState();
    }

    public IReadOnlyList<ulong> DrainUnlocked()
    {
        if (_unlocked.Count == 0)
            return [];

        var drained = _unlocked.ToArray();
        _unlocked.Clear();
        return drained;
    }

    public SCResRegionProgress ToRegionProgress()
    {
        var reply = new SCResRegionProgress { Result = 0 };

        foreach (var regionId in assets.RegionProgress.Regions)
        {
            var region = new RegionProgress { RegionId = regionId };

            foreach (var subRegionId in assets.RegionProgress.SubRegions(regionId))
            {
                region.SubRegionProgress.Add(ToSubRegionProgress(subRegionId));
            }

            if (region.SubRegionProgress.Count > 0)
                reply.RegionProgress.Add(region);
        }
        return reply;
    }

    private IReadOnlySet<uint> ClaimedOf(ulong subRegionId) =>
        _subregions.GetValueOrDefault(subRegionId)?.ClaimedValues ?? EmptyValues;

    public bool CanClaim(ulong subRegionId)
    {
        if (!assets.RegionProgress.SubRegionExists(subRegionId))
            return false;

        var percent = CompletionPercent(subRegionId);

        return assets.RegionProgress.Rewards(subRegionId)
            .Any(reward => reward.PrograssValue <= percent && !ClaimedOf(subRegionId).Contains(reward.PrograssValue));
    }

    public IReadOnlyList<PRegionRewardDataTable> EligibleRewards(ulong subRegionId)
    {
        var percent = CompletionPercent(subRegionId);

        return assets.RegionProgress.Rewards(subRegionId)
            .Where(reward => reward.PrograssValue <= percent && !ClaimedOf(subRegionId).Contains(reward.PrograssValue))
            .ToList();
    }

    public void MarkClaimed(ulong subRegionId, IReadOnlyList<uint> values)
    {
        if (!assets.RegionProgress.SubRegionExists(subRegionId))
            return;

        var state = EnsureState(subRegionId);
        var claimed = new SortedSet<uint>(state.ClaimedValues);
        var changed = false;

        foreach (var value in values)
        {
            if (claimed.Add(value))
                changed = true;
        }

        if (!changed)
            return;

        _subregions[subRegionId] = state with { ClaimedValues = claimed };

        Log.Stage("subregion {SubRegionId} recorded {NewCount} new reward claims", subRegionId, claimed.Count - state.ClaimedValues.Count);
    }

    public IReadOnlyList<ItemGrant> RewardOf(PRegionRewardDataTable reward) =>
        assets.Drops.Bundle(reward.DropId);

    public bool SetSequence(ulong subRegionId, uint sequenceId, uint count)
    {
        if (!assets.RegionProgress.SubRegionExists(subRegionId))
            return false;

        if (!assets.RegionProgress.Sequences(subRegionId).Contains(sequenceId))
            return false;

        var state = _subregions.GetValueOrDefault(subRegionId);

        if (state is null && count == 0)
            return false;

        if (state is not null
            && state.Sequences.TryGetValue(sequenceId, out var current)
            && current >= count)
            return false;

        state = EnsureState(subRegionId);

        var sequences = new SortedDictionary<uint, uint>(state.Sequences.ToDictionary(pair => pair.Key, pair => pair.Value)) {
            [sequenceId] = count
        };
        _subregions[subRegionId] = state with { Sequences = sequences };

        return true;
    }

    public uint SubRegionsAtPercent(uint percent) =>
        (uint)_subregions.Keys.Count(id => CompletionPercent(id) >= percent);

    /// <summary>Match the client's weighted completion ratio, rounded down to 0, 25, 50, 75, or 100.</summary>
    public uint CompletionPercent(ulong subRegionId)
    {
        long needed = 0;
        long attained = 0;
        var state = _subregions.GetValueOrDefault(subRegionId);

        foreach (var sequenceId in assets.RegionProgress.Sequences(subRegionId))
        {
            if (assets.RegionProgress.SequenceRow(subRegionId, sequenceId) is not {} sequence)
                continue;

            var weight = assets.RegionProgress.RegistOf(sequence.Type)?.Weight ?? 1;
            needed += weight * sequence.ParamNum;
            var count = state?.Sequences.GetValueOrDefault(sequenceId) ?? 0;
            attained += weight * Math.Min(count, sequence.ParamNum);
        }

        if (needed <= 0)
            return 0;

        var percent = (uint)Math.Clamp(attained * 100 / needed, min: 0, max: 100);

        return percent switch {
            >= 100 => 100,
            >= 75 => 75,
            >= 50 => 50,
            >= 25 => 25,
            _ => 0
        };
    }

    private SubRegionProgress ToSubRegionProgress(ulong subRegionId)
    {
        var state = _subregions.GetValueOrDefault(subRegionId);
        var sub = new SubRegionProgress { SubRegionId = subRegionId };

        if (state is not null)
        {
            foreach (var (sequenceId, count) in state.Sequences)
            {
                sub.Progress.Add(new ProgressData { SequenceId = sequenceId, Count = count });
            }

            foreach (var value in state.ClaimedValues)
            {
                sub.ProgressValue.Add((EnmProgressValue)value);
            }
        }
        return sub;
    }

    public SCSubRegionUpdateNtf? ToSubRegionUpdate(ulong subRegionId)
    {
        if (!assets.RegionProgress.SubRegionExists(subRegionId))
            return null;

        return new SCSubRegionUpdateNtf {
            RegionId = assets.RegionProgress.RegionOf(subRegionId),
            SubRegionData = ToSubRegionProgress(subRegionId)
        };
    }

    private SubRegionProgressState EnsureState(ulong subRegionId)
    {
        if (_subregions.TryGetValue(subRegionId, out var state))
            return state;

        state = new SubRegionProgressState(subRegionId, new SortedDictionary<uint, uint>(), new SortedSet<uint>());
        _subregions[subRegionId] = state;
        _unlocked.Add(subRegionId);
        Log.Stage("subregion {SubRegionId} progress unlocked", subRegionId);
        return state;
    }

}
