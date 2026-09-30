using Lunaria.Game.Resources;
using Lunaria.Game.Tasks;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    /// <summary>Repair only the Mind Palace continuation stranded by the old return bug.</summary>
    internal void RestoreCompletedMindPalaceReturn()
    {
        const uint type = TaskAssets.QuestMain;
        const uint parent = 11002;
        const uint visit = 91001;
        const ulong continuation = 1100211;

        if (!Tasks.IsFinished(type, visit)
            || Tasks.TaskDataOf(type, parent)?.CurrentStep.StepId != continuation
            || Assets.Tasks.Action(type, 110020601) is not {} entry
            || !TargetParameter.TryId(entry.ServerParam1, out var palaceMap)
            || Map.MapId != palaceMap
            || Assets.Tasks.Action(type, 110021002) is not {} exit
            || !TargetParameter.TryId(exit.ServerParam1, out var returnMap))
            return;

        var position = Map.ReturnPoint is {} point && point.MapId == returnMap && point.IsSynced
            ? (point.X, point.Y, point.Z) : Assets.Tasks.PositionAfterStep(type, continuation);
        if (position is {} resume)
            Map.RestoreReturnPosition(returnMap, resume);
    }
}
