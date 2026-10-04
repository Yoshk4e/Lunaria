using Lunaria.Game.Resources;
using Lunaria.Game.Tasks;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    public IReadOnlyList<ulong> RecalculateRegionProgress()
    {
        var changed = new List<ulong>();

        foreach (var subRegionId in assets.RegionProgress.TrackedSubRegions)
        {
            var moved = false;

            foreach (var sequenceId in assets.RegionProgress.Sequences(subRegionId))
            {
                // Gather counts only grow from CollectionGathered and cannot be recounted from the save.
                if (assets.RegionProgress.SequenceRow(subRegionId, sequenceId) is not {} sequence
                    || assets.RegionProgress.CountsGathers(sequence))
                    continue;

                // The client shows the raw count over ParamNum. Silvercraft objectives count owned creatures of the
                // listed growth IDs, which can exceed ParamNum, so the count stops at the objective.
                var count = Math.Min(CountSequence(subRegionId, sequence), sequence.ParamNum);

                if (RegionProgress.SetSequence(subRegionId, sequenceId, count))
                    moved = true;
            }

            if (moved)
                changed.Add(subRegionId);
        }

        return changed;
    }

    /// <summary>
    /// Saves from before per-gather counting hold the number of distinct templates gathered anywhere. Rebuild the chest
    /// and resource objectives from the objects the save records as gathered: collected or destroyed, or collectable
    /// again after a respawn. Earlier gathers of a respawned object are not recorded, so this is a lower bound.
    /// </summary>
    internal void RebuildGatherCounts()
    {
        RegionProgress.ClearGatherCounts();

        foreach (var node in Collections.Entries.Values.OrderBy(node => node.Uniq))
        {
            var gathered = node.Status is EnmCollectionStatus.EcsCollected or EnmCollectionStatus.EcsDestroyed
                || (node.Status == EnmCollectionStatus.EcsCanCollect && assets.Collections.Respawn(node.Cfg).ResetsEver);

            if (gathered)
                RegionProgress.RecordGather(node.Block, node.Cfg);
        }
    }

    private uint CountSequence(ulong subRegionId, RegionSequence sequence)
    {
        var regist = assets.RegionProgress.RegistOf(sequence.Type);

        if (regist is null)
            return 0;

        // The registry lists completed POI quests, not main quests.
        if (regist.IsTask == 1)
            return (uint)sequence.ParamId.Count(taskId => Tasks.IsFinished(TaskAssets.POIQuest, taskId));

        // ParamId holds the teleport template (P_TeleportPointTemplate). The unlocked points are P_FunctionalNPCTable
        // instances of that template, each placed on one sub-region.
        if (regist.RegistType == 3)
            return (uint)Map.UnlockedTeleports.Count(id => assets.Maps.Teleport(id) is {} point
                && point.MapId == subRegionId && sequence.ParamId.Any(template => template == point.TemplateId));

        if (regist.RegistType == 6)
            return (uint)sequence.ParamId.Distinct().Sum(SilverCreatures.CountOfGrowth);

        return 0;
    }
}
