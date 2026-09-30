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

                if (RegionProgress.SetSequence(subRegionId, sequenceId, CountSequence(subRegionId, sequence)))
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

        if (regist.RegistType == 3)
            return (uint)sequence.ParamId.Count(id => Map.UnlockedTeleports.Contains(id));

        if (regist.RegistType == 6)
            return (uint)sequence.ParamId.Count(growthId => SilverCreatures.CountOfGrowth(growthId) > 0);

        return 0;
    }
}
