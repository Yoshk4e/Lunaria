using Lunaria.Common.Tracking;
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

        ReturnToOpenWorld("dungeon");
    }

    /// <summary>
    /// A wanted poster map cannot be loaded at login: the big-world NPC system config it relies on is only loaded by
    /// the open world, so the NPC monster wait never ends (loading stuck at 96%). The client handles a run left
    /// from outside (SCWantedOutsideData current_id / current_step, RequestOutsideWPSettlement), so the role goes
    /// back to the open world and keeps its run.
    /// </summary>
    internal void LeaveWantedMapOnLogin()
    {
        if (!Assets.Maps.IsWantedPosterMap(Map.MapId))
            return;

        ReturnToOpenWorld("wanted poster");

        // A run kept outside its map has no active team until it is entered again: the client disables team
        // editing (Partners) while a temporary team is active, and the entry request edits the wanted selection.
        WantedSuspended = Wanted.IsRunning && !Assets.Maps.IsWantedPosterMap(Map.MapId);
    }

    /// <summary>The wanted run was left in progress and the role is outside its map. Derived at login, not saved.</summary>
    [Untracked]
    internal bool WantedSuspended { get; private set; }

    /// <summary>A wanted run is in progress on its map. A suspended run does not hold the open world.</summary>
    internal bool InWantedRun => Wanted.IsRunning && !WantedSuspended;

    private void ReturnToOpenWorld(string left)
    {
        if (Map.ReturnPoint is {} origin)
        {
            var position = origin.IsSynced ? (origin.X, origin.Y, origin.Z) : Assets.Maps.SpawnPos(origin.MapId);
            if (position is {} resume && Map.RestoreReturnPosition(origin.MapId, resume))
            {
                Log.Flag("left {Left} map at login, back to map {MapId}", left, origin.MapId);
                return;
            }
        }

        if (Assets.Maps.Savepoint(Map.Savepoint) is {} savepoint && Assets.Maps.SavepointPos(Map.Savepoint) is {} at
            && Map.RestoreReturnPosition(savepoint.MapId, at))
            Log.Flag("left {Left} map at login, back to savepoint {Savepoint}", left, Map.Savepoint);
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
