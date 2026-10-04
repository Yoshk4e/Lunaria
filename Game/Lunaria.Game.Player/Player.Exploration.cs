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
                if (assets.RegionProgress.SequenceRow(subRegionId, sequenceId) is not {} sequence)
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

    private uint CountSequence(ulong subRegionId, RegionSequence sequence)
    {
        var regist = assets.RegionProgress.RegistOf(sequence.Type);

        if (regist is null)
            return 0;

        // The registry lists completed POI quests, not main quests.
        if (regist.IsTask == 1)
            return (uint)sequence.ParamId.Count(taskId => Tasks.IsFinished(TaskAssets.POIQuest, taskId));

        if (regist.RegistType == 2 || regist.RegistType == 7)
            return (uint)sequence.ParamId.Count(cfg => Collections.Gathered.Contains(cfg));

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
