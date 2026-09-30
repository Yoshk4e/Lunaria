using Google.Protobuf;
using Lunaria.Game.Player.Gameplay;
using Lunaria.Game.Tasks;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    private TaskActionOutcome CreateTaskOutcome(TaskProgressResult progress, RewardDelivery delivery, IReadOnlyList<SCCaseReceiveNtf> cases)
    {
        var notifications = new List<IMessage>();
        if (progress.Recorded)
        {
            Gameplay.Publish(new TaskProgressed(progress));
            foreach (var action in progress.SettledActions)
                notifications.Add(new SCTaskActionUpdate {
                    TaskType = progress.TaskType, ActionId = action.ActionId,
                    Progress = action.Progress, MaxProgress = action.MaxProgress
                });
            if (progress.UpdatedData is {} updated)
                notifications.Add(new SCTaskProgressUpdateNtf {
                    UpdatedData = updated,
                    UpdateType = progress.TaskFailed ? EnmTaskActionUpdateType.EtaskActionUpdateTypeFail
                        : progress.TaskCompleted ? EnmTaskActionUpdateType.EtaskActionUpdateTypeFinish
                        : EnmTaskActionUpdateType.EtaskActionUpdateTypeNormal
                });
            foreach (var started in progress.StartedTaskData)
                notifications.Add(new SCTaskProgressUpdateNtf { UpdatedData = started, UpdateType = EnmTaskActionUpdateType.EtaskActionUpdateTypeNew });
            notifications.AddRange(cases);
        }
        notifications.AddRange(delivery.Presentation);
        return new TaskActionOutcome(progress, delivery, cases) { Notifications = notifications };
    }
}
