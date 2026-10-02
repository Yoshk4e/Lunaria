using Lunaria.Game.Player.Gameplay;
using Lunaria.Game.Player.Notifications;
using Lunaria.Game.Player.Rewards;
using Lunaria.Game.Resources;
using Lunaria.Game.Logging;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    private RewardDelivery GrantWithoutOverflowMail(IEnumerable<ItemGrant> grants, EnmItemReason reason)
    {
        var resolution = RewardResolver.Resolve(assets.Items, assets.Drops, grants);
        var mutation = DeliverRewards(resolution, reason, UtcNow);
        PublishRewardChanges(mutation);
        var delivery = mutation.Delivery;
        Log.Event("grant reason {Reason} credited {CreditedCount} stored {StoredCount} undelivered {UndeliveredCount}",
            reason, delivery.Credited.Count, delivery.Stored.Count, delivery.Undelivered.Count);
        return delivery;
    }

    private RewardDelivery PresentRewards(RewardDelivery delivery) =>
        delivery with { Presentation = RewardPresentation.Capture(this, delivery) };

    private void PublishRewardChanges(RewardMutation mutation)
    {
        var delivery = mutation.Delivery;
        foreach (var (motiveId, count) in mutation.MotivesAcquired)
            Gameplay.Publish(new MotiveAcquired(motiveId, count));
        foreach (var (id, count) in mutation.ItemsAcquired)
        {
            Gameplay.Publish(new ItemAcquired(id, count));
        }

        foreach (var group in delivery.CollectedCreatures.GroupBy(creature => creature.ItemId))
        {
            Gameplay.Publish(new CreatureAcquired(group.Key, (uint)group.Count()));
        }
        SynchronizeLevelData();
        Gameplay.Publish(new WalletChanged());

        Gameplay.Publish(new BagChanged(delivery.Reason));

        if (delivery.Stamina is {} grantedStamina)
            Gameplay.Publish(new StaminaChanged(grantedStamina));

        if (delivery.Newcomers.Count > 0) Gameplay.Publish(new CharactersAcquired(delivery.Newcomers));
        foreach (var passId in delivery.ChangedBattlePasses.Distinct()) Gameplay.Publish(new BattlePassChanged(passId));
        if (delivery.CollectedCreatures.Count > 0) Gameplay.Publish(new CreatureRosterChanged(delivery.CollectedCreatures));

    }
}
