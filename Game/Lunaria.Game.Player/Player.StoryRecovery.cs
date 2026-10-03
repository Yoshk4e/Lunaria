using Lunaria.Game.Logging;
using Lunaria.Game.Resources;
using Lunaria.Game.Tasks;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    /// <summary>
    /// A dungeon map without a running dungeon cannot be loaded (the client's dungeon process has no data), so a
    /// role saved there after its run ended goes back to the open-world map it came from.
    /// </summary>
    internal void LeaveStrandedDungeonMap()
    {
        if (Dungeons.Current is not null || !Assets.Maps.IsScriptedWorld(Map.MapId) || !Assets.Dungeons.IsDungeonMap(Map.MapId))
            return;

        if (Map.ReturnPoint is {} origin)
        {
            var position = origin.IsSynced ? (origin.X, origin.Y, origin.Z) : Assets.Maps.SpawnPos(origin.MapId);
            if (position is {} resume && Map.RestoreReturnPosition(origin.MapId, resume))
            {
                Log.Flag("left stranded dungeon map, back to map {MapId}", origin.MapId);
                return;
            }
        }

        if (Assets.Maps.Savepoint(Map.Savepoint) is {} savepoint && Assets.Maps.SavepointPos(Map.Savepoint) is {} at
            && Map.RestoreReturnPosition(savepoint.MapId, at))
            Log.Flag("left stranded dungeon map, back to savepoint {Savepoint}", Map.Savepoint);
    }

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
