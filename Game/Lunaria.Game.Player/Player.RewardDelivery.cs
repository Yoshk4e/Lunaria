using Lunaria.Game.Player.Rewards;
using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    private RewardMutation DeliverRewards(RewardResolution resolution, EnmItemReason reason, DateTimeOffset now)
    {
        var credited = new List<ItemGrant>();
        var stored = new List<ItemGrant>();
        var undelivered = new List<ItemGrant>(resolution.Unresolved);
        var collectedCreatures = new List<CmdSilverCreatureItem>();
        var creatureFailures = new List<ItemGrant>();
        var newcomers = new List<CharacterData>();
        var newMotives = new List<CSMotiveElem>();
        var acquired = new Dictionary<uint, uint>();
        var motivesAcquired = new Dictionary<uint, uint>();

        void Acquired(uint id, uint count)
        {
            acquired[id] = (uint)Math.Min((ulong)acquired.GetValueOrDefault(id) + count, uint.MaxValue);
        }

        var changedPasses = new List<uint>();
        uint teamExpFromItems = 0;
        int? stamina = null;
        var claimTime = unchecked((ulong)now.ToUnixTimeSeconds());

        foreach (var resolved in resolution.Items)
        {
            var grant = resolved;

            if (grant.Count == 0)
                continue;

            var useType = assets.Items.Get(grant.ItemId) is { AutoUse: true } row ? (ItemUseType)row.UseType : ItemUseType.None;

            switch (useType)
            {
                case ItemUseType.AddCoin when assets.Items.MoneyTypeOf(grant.ItemId) is {} moneyType:
                    if (Wallet.Credit(moneyType, grant.Count) != 0)
                        undelivered.Add(grant);
                    else
                    {
                        credited.Add(grant);
                        Acquired(grant.ItemId, grant.Count);
                    }
                    continue;

                case ItemUseType.AddTeamExp when assets.Items.TeamExpOf(grant.ItemId) is {} perItem:
                    var experience = (ulong)perItem * grant.Count + teamExpFromItems;

                    if (experience > uint.MaxValue)
                        undelivered.Add(grant);
                    else
                    {
                        teamExpFromItems = (uint)experience;
                        Acquired(grant.ItemId, grant.Count);
                    }
                    continue;

                case ItemUseType.AddBpExp when assets.Items.BattlePassOf(grant.ItemId) is {} passId:
                    if (BattlePasses.AddExp(passId, grant.Count).Changed && !changedPasses.Contains(passId))
                        changedPasses.Add(passId);
                    Acquired(grant.ItemId, grant.Count);
                    continue;

                // Duplicate character cards go to the bag. Only gacha converts them.
                case ItemUseType.AddCharacter when assets.Items.Get(grant.ItemId)!.Param.Count > 0: {
                    var character = Characters.Add(Guid, assets.Items.Get(grant.ItemId)!.Param[0]);

                    if (character.Ok)
                    {
                        newcomers.Add(Characters.ToCharacterData(Characters.Get(character.InstId)!));
                        Acquired(grant.ItemId, count: 1);

                        if (grant.Count == 1)
                            continue;

                        grant = grant with { Count = grant.Count - 1 };
                    }
                    break;
                }

                case ItemUseType.AddMotive when assets.Items.Get(grant.ItemId)!.Param.Count > 0: {
                    uint added = 0;
                    var motiveId = assets.Items.Get(grant.ItemId)!.Param[0];

                    while (added < grant.Count)
                    {
                        var addition = Motives.Add(Guid, motiveId, claimTime);
                        if (!addition.Ok) break;

                        newMotives.Add(Motives.ToMotiveElem(Motives.Get(addition.UniqId)!));
                        added++;
                    }

                    if (added > 0)
                    {
                        Acquired(grant.ItemId, added);
                        motivesAcquired[motiveId] = (uint)Math.Min(uint.MaxValue, (ulong)motivesAcquired.GetValueOrDefault(motiveId) + added);
                    }

                    if (added == grant.Count)
                        continue;

                    grant = grant with { Count = grant.Count - added };
                    break;
                }

                case ItemUseType.AddStamina when assets.Items.StaminaOf(grant.ItemId) is {} perPoint:
                    var staminaAmount = perPoint * grant.Count;

                    if (staminaAmount <= int.MaxValue && Progress.AddStamina((int)staminaAmount, now) == 0)
                    {
                        stamina = Progress.Stamina;
                        Acquired(grant.ItemId, grant.Count);
                    } else
                        undelivered.Add(grant);
                    continue;

                case ItemUseType.AddSilverCreature: {
                    uint added = 0;

                    while (added < grant.Count)
                    {
                        var (collected, creature) = SilverCreatures.TryCollect(grant.ItemId);

                        if (!collected || creature is null)
                            break;

                        collectedCreatures.Add(creature);
                        added++;
                    }

                    if (added > 0) Acquired(grant.ItemId, added);

                    if (added == grant.Count)
                        continue;

                    grant = grant with { Count = grant.Count - added };

                    creatureFailures.Add(grant);
                    break;
                }
            }

            var result = Bag.Add(grant.ItemId, grant.Count);

            if (result.AnyStored)
            {
                stored.Add(new ItemGrant(grant.ItemId, result.Stored));
                Acquired(grant.ItemId, result.Stored);
            }
            var missed = result.AnyStored ? result.Overflow : grant.Count;

            if (missed > 0)
                undelivered.Add(new ItemGrant(grant.ItemId, missed));
        }

        var delivery = new RewardDelivery {
            Reason = reason,
            Credited = credited.ToArray(), Stored = stored.ToArray(), Undelivered = undelivered.ToArray(),
            DirectFailures = undelivered.ToArray(),
            CollectedCreatures = collectedCreatures, CreatureFailures = creatureFailures,
            TeamExpFromItems = teamExpFromItems, Newcomers = newcomers, NewMotives = newMotives,
            TeamExpFromReason = TeamExpFor(reason),
            ChangedBattlePasses = changedPasses, Stamina = stamina
        };
        ApplyTeamExperience(delivery.TeamExpAwarded);
        return new RewardMutation(delivery, acquired, motivesAcquired);
    }
}
