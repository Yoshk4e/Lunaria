using Msg;
using Lunaria.Game.Resources;

namespace Lunaria.Game.Player.Gameplay;

/// <summary>Capture notifications before later operations change the state.</summary>
internal sealed class SyncHooks :
    IGameplayHook<WalletChanged>,
    IGameplayHook<BagChanged>,
    IGameplayHook<StaminaSpent>,
    IGameplayHook<StaminaChanged>,
    IGameplayHook<SatietyChanged>,
    IGameplayHook<CooldownStarted>,
    IGameplayHook<VitalsChanged>,
    IGameplayHook<LiquidChanged>,
    IGameplayHook<SkillGroupsChanged>,
    IGameplayHook<BuffsChanged>,
    IGameplayHook<CharactersChanged>,
    IGameplayHook<TaskProgressed>,
    IGameplayHook<CharacterLeveled>,
    IGameplayHook<CharactersAcquired>,
    IGameplayHook<GuidesChanged>,
    IGameplayHook<BattlePassChanged>,
    IGameplayHook<CreatureRosterChanged>,
    IGameplayHook<LevelDataChanged>,
    IGameplayHook<WantedResourcesChanged>
{
    public void Handle(Player p, WalletChanged e, PlayerChanges changes)
    {
        foreach (var (moneyType, amount) in p.Wallet.DrainChangedBalances())
        {
            changes.Add(new SCMoneyUpdate {
                Result = 0,
                Type = moneyType,
                Amount = amount
            });
        }
    }

    public void Handle(Player p, BagChanged e, PlayerChanges changes)
    {
        var items = p.DrainInventoryChanges();

        if (items.Count == 0)
            return;

        foreach (var batch in items.Chunk((int)EnmSizeLimit.MaxItemLen))
        {
            var ntf = new SCItemBagChangeNtf { Reason = e.Reason };
            ntf.Items.AddRange(batch);
            changes.Add(ntf);
        }
    }

    public void Handle(Player p, StaminaSpent e, PlayerChanges changes) =>
        changes.Add(StaminaAttr(p, p.Progress.Stamina));

    public void Handle(Player p, StaminaChanged e, PlayerChanges changes) =>
        changes.Add(StaminaAttr(p, e.Stamina));

    public void Handle(Player p, SatietyChanged e, PlayerChanges changes) =>
        changes.Add(new SCPlayerAttrUpdateNtf {
            UpdateAttrs = {
                new PlayerAttr {
                    AttrType = (int)PlayerAttrType.EnmPlayerAttrSatiety,
                    ValueInt32 = e.Satiety
                }
            }
        });

    public void Handle(Player p, CooldownStarted e, PlayerChanges changes) =>
        changes.Add(new SCItemCDNtf {
            Cd = new CmdItemCD {
                Type = e.CdType,
                CdTime = e.ReadyUnix
            }
        });

    public void Handle(Player p, VitalsChanged e, PlayerChanges changes)
    {
        foreach (var instId in e.InstIds)
        {
            changes.Add(new SCOutsideAttribNtf { Data = p.OutsideAttributes(instId) });
        }
        Handle(p, new CharactersChanged(e.InstIds), changes);
    }

    public void Handle(Player p, LiquidChanged e, PlayerChanges changes) =>
        changes.Add(p.CurrentLiquidNotification());

    public void Handle(Player p, SkillGroupsChanged e, PlayerChanges changes)
    {
        foreach (var group in e.Groups.Distinct())
        {
            if (p.Skills.GroupLevel(group) is {} level)
                changes.Add(new SCSkillUpdate { Result = 0, InstId = e.InstId, Groupid = group, Level = level });
        }
    }

    public void Handle(Player p, BuffsChanged e, PlayerChanges changes)
    {
        foreach (var buffId in e.Removed)
        {
            changes.Add(new SCBuffDel { Result = 0, Id = buffId });
        }

        foreach (var (data, refreshed) in e.Updated)
        {
            changes.Add(refreshed ?
                new SCBuffUpdate { Result = 0, Data = data } :
                new SCBuffAdd { Result = 0, Data = data });
        }

        // Team buffs change every character's attributes, which the client only refreshes from this notification.
        foreach (var character in p.Characters.All)
        {
            changes.Add(new SCOutsideAttribNtf { Data = p.OutsideAttributes(character.InstId) });
        }
    }

    public void Handle(Player p, CharactersChanged e, PlayerChanges changes)
    {
        var update = new SCCharacterUpdateNtf();
        foreach (var id in e.InstIds.Distinct())
            if (p.Characters.Get(id) is {} character)
                update.Characters.Add(p.Characters.ToCharacterData(character));
        if (update.Characters.Count > 0) changes.Add(update);
    }

    public void Handle(Player p, TaskProgressed e, PlayerChanges changes)
    {
        var progress = e.Progress;
        var tasks = progress.StartedTasks.Select(id => (progress.TaskType, id)).Append((progress.TaskType, progress.TaskId));

        if (p.TaskCollectionChanges(tasks) is {} ntf)
            changes.Add(ntf);
    }

    public void Handle(Player p, CharacterLeveled e, PlayerChanges changes) =>
        Handle(p, new CharactersChanged([e.InstanceId]), changes);

    public void Handle(Player p, CharactersAcquired e, PlayerChanges changes)
    {
        foreach (var character in e.Characters)
            changes.Add(new SCCharacterNewcomerNtf { Newcomer = character });
    }

    public void Handle(Player p, GuidesChanged e, PlayerChanges changes)
    {
        var notification = new SCUnlockGuideNtf();
        notification.GuideInfo.AddRange(p.Guides.InfosOf(e.Ids));
        if (notification.GuideInfo.Count > 0) changes.Add(notification);
    }

    public void Handle(Player p, BattlePassChanged e, PlayerChanges changes)
    {
        if (p.BattlePasses.NotificationOf(e.PassId) is {} data)
            changes.Add(new SCBattlePassNtf { Data = data });
    }

    public void Handle(Player p, CreatureRosterChanged e, PlayerChanges changes)
    {
        if (e.Added.Count == 0) return;
        var change = new SilverCreatureChange();
        change.AddList.AddRange(e.Added);
        changes.Add(new SCSilverCreatureChangeNtf { Change = change });
    }

    public void Handle(Player p, LevelDataChanged e, PlayerChanges changes) =>
        changes.Add(new SCPlayerLevelDataNtf { BeforeList = e.Before, UpdateList = e.After });

    public void Handle(Player p, WantedResourcesChanged e, PlayerChanges changes)
    {
        var resource = p.Wanted.ToResource();
        var blesses = Added(resource.BlessIds, e.Before.BlessIds);
        var relics = Added(resource.RelicsIds, e.Before.RelicsIds);
        if (blesses.Count > 0)
            changes.Add(new SCWantedBlessNtf {
                Type = (uint)(e.AwardType == EWantedAwardType.AddBlessSelect ? EWantedAwardType.AddBlessSelect : EWantedAwardType.AddBlessRandom),
                BlessIds = { blesses }
            });
        if (!resource.Bonds.SequenceEqual(e.Before.Bonds))
            changes.Add(new SCWantedBondNtf { Bonds = { resource.Bonds } });
        if (relics.Count > 0)
            changes.Add(new SCWantedRelicsNtf {
                Type = (uint)(e.AwardType == EWantedAwardType.AddRelicSelect ? EWantedAwardType.AddRelicSelect : EWantedAwardType.AddRelicRandom),
                RelicsIds = { relics }
            });
        if (!resource.Bionics.SequenceEqual(e.Before.Bionics))
        {
            changes.Add(new SCWantedBionicsNtf {
                Type = (uint)(e.AwardType == EWantedAwardType.AddCreatureSelect ? EWantedAwardType.AddCreatureSelect : EWantedAwardType.AddOneCreature),
                Bionics = { resource.Bionics }
            });
            foreach (var attrib in p.Wanted.BionicsAttribData([]))
                changes.Add(new SCWantedBionicsAttribNtf { Data = attrib });
        }
    }

    // The client appends these entries. Keep new duplicates without resending old entries.
    private static IReadOnlyList<uint> Added(IEnumerable<uint> after, IEnumerable<uint> before)
    {
        var counts = before.GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
        var added = new List<uint>();
        foreach (var id in after)
            if (counts.GetValueOrDefault(id) > 0) counts[id]--;
            else added.Add(id);
        return added;
    }

    // The stamina popup counts down to STAMINA_FULLTIME; without it the client formats a negative time.
    private static SCPlayerAttrUpdateNtf StaminaAttr(Player p, int stamina) =>
        new() {
            UpdateAttrs = {
                new PlayerAttr {
                    AttrType = (int)PlayerAttrType.EnmPlayerAttrStaminaCur,
                    ValueInt32 = stamina
                },
                p.StaminaFullTimeAttr()
            }
        };
}
