using Lunaria.Game.Player;
using Lunaria.Game.Player.Persistence.Saves;
using Lunaria.Game.Resources;
using Lunaria.Game.Tasks;
using Lunaria.Game.Wanted;
using Msg;
using Xunit;
using Xunit.Abstractions;

namespace Lunaria.Tests;

// Regression tests for bugs reproduced against the bundled data and public gameplay APIs.
[Collection("bundled-gameplay")]
[Trait("Category", "Audit")]
public sealed class AuditGameplayTests(BundledGameplayFixture fixture, ITestOutputHelper output)
{
    private GameData Assets => fixture.Data;

    [Fact]
    public void DungeonProgress_ExchangesTheLastFinishedBattleAndAcceptsEveryBattleOfTheRun()
    {
        const uint dungeonId = 200008; // battles 200008001 and 200008002
        var player = Fresh();
        player.Progress.Load(1, 0, 0, 240, DateTimeOffset.UtcNow);
        var source = Assets.Dungeons.Dungeon(dungeonId)!.DungeonType;
        Assert.NotNull(player.QueryTemporaryTeam((int)EnmTmpTeamType.Dungeon, source));
        Assert.Equal(0, player.EnterDungeon(dungeonId).Code);

        // Nothing finished yet: the client starts from the first battle instead of reading the run as over.
        Assert.Equal(0u, player.Dungeons.CompletedBattle());
        Assert.Equal(0, player.EnterBattle(EBattleType.EnmBattleTypeRepeatDungeon, 200008001, 0, default));
        player.Battles.Leave(EBattleType.EnmBattleTypeRepeatDungeon, 200008001, true);

        // The client reports the first battle as finished; the run moves on and the second battle is accepted.
        Assert.Equal(0, player.AdoptDungeonCurrent(dungeonId, 200008001).Code);
        Assert.Equal(200008001u, player.Dungeons.CompletedBattle());
        Assert.Equal(0, player.EnterBattle(EBattleType.EnmBattleTypeRepeatDungeon, 200008002, 0, default));
    }

    [Fact]
    public void HordeBattle_IsEnteredOnTheBattlefieldOfItsBattleRow()
    {
        const uint dungeonId = 11120201; // battle row 11120201 fights on battlefield 100100705
        var player = Fresh();
        player.Progress.Load(1, 0, 0, 240, DateTimeOffset.UtcNow);
        Assert.Equal(0, player.EnterDungeon(dungeonId).Code);

        Assert.Equal(0, player.EnterBattle(EBattleType.EnmBattleTypeHorde, 100100705, 0, default));
        Assert.Equal(0, player.StartBattle(EBattleType.EnmBattleTypeHorde, 100100705));
    }

    [Fact]
    public void SaveOnADungeonMapWithoutARun_ReloadsOnTheOpenWorldMapItCameFrom()
    {
        var player = Fresh();
        player.Map.Load(201001001005, Assets.Starter.Savepoint, [], [], (1, 2, 3),
            returnPoint: new Lunaria.Game.World.MapReturnPoint(100001001001, 141540, 41185, 34003, IsSynced: true));
        Assert.Null(player.Dungeons.Current);

        var json = System.Text.Json.JsonSerializer.Serialize(
            Lunaria.Game.Player.Persistence.Saves.RoleSaveMapper.Capture(player), Lunaria.Game.Player.Persistence.Saves.SaveJson.Options);
        var restored = new Player(2, Assets);
        Lunaria.Game.Player.Persistence.Saves.RoleSaveMapper.Apply(restored,
            System.Text.Json.JsonSerializer.Deserialize<Lunaria.Game.Player.Persistence.Saves.RoleSaveDocument>(
                json, Lunaria.Game.Player.Persistence.Saves.SaveJson.Options)!);

        Assert.Equal(100001001001ul, restored.Map.MapId);
        Assert.Equal((141540, 41185, 34003), restored.Map.Position);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void DungeonStamina_IsKeptOnlyWhenTheRunIsWon(bool victory, bool leave)
    {
        const uint dungeonId = 205001; // Superintendence I, 30 stamina
        var player = Fresh();
        player.Progress.Load(1, 0, 0, 200, DateTimeOffset.UtcNow);
        var before = player.Progress.Stamina;
        Assert.Equal(0, player.EnterDungeon(dungeonId).Code);
        Assert.Equal(before - 30, player.Progress.Stamina);

        Assert.Equal(0, player.FinishDungeon(dungeonId, victory, leave, 0).Code);

        Assert.Equal(victory ? before - 30 : before, player.Progress.Stamina);
    }

    [Fact]
    public void AbyssStageWithAHordeRow_NeverSendsHordeData()
    {
        const uint dungeonId = 203001; // Deep Cognito T1, an Abyss stage that also has a P_HordeTable row
        var player = Fresh();
        player.Progress.Load(1, 0, 0, 200, DateTimeOffset.UtcNow);
        Assert.Equal(0, player.EnterDungeon(dungeonId).Code);
        Assert.Equal(0, player.FinishDungeon(dungeonId, true, false, 0).Code);
        Assert.Empty(player.Dungeons.ToFullData(DateTimeOffset.UtcNow).HordeData.HordeList);

        // Saves written before the fix hold a horde state for it, which made the client hang while loading the world.
        var saved = RoleSaveMapper.Capture(player);
        var old = saved with { Dungeons = saved.Dungeons! with {
            Hordes = [new RoleSaveDocument.HordeSave { HordeId = dungeonId, KillCount = 0, StarAward = 0 }]
        } };
        var restored = Fresh();
        RoleSaveMapper.Apply(restored, old);
        Assert.Empty(restored.Dungeons.ToFullData(DateTimeOffset.UtcNow).HordeData.HordeList);
    }

    [Fact]
    public void AbyssStage_GrantsItsFirstClearRewardOnlyOnce()
    {
        const uint dungeonId = 203001; // Deep Cognito T1: firstPassRewardDrop 333201, no rewardDrop
        var player = Fresh();
        player.Progress.Load(1, 0, 0, 200, DateTimeOffset.UtcNow);

        Assert.Equal(0, player.EnterDungeon(dungeonId).Code);
        var first = player.FinishDungeon(dungeonId, true, false, 0);
        Assert.Equal(0, first.Code);
        Assert.Equal(4u, player.OwnedItemCount(11406001));

        Assert.Equal(0, player.EnterDungeon(dungeonId).Code);
        Assert.Equal(0, player.FinishDungeon(dungeonId, true, false, 0).Code);
        Assert.Equal(4u, player.OwnedItemCount(11406001));
    }

    [Fact]
    public void DailyLimitedDungeon_SendsTheAttemptsUsedToday()
    {
        const uint dungeonId = 202001; // Inside the Incinerator: I, type 2, one reward chance per day
        var player = Fresh();
        player.Progress.Load(1, 0, 0, 200, DateTimeOffset.UtcNow);
        CSDungeonsTypeData TypeData() => player.Dungeons.ToFullData(DateTimeOffset.UtcNow).CommonData.TypeData.Single(t => t.Type == 2);

        Assert.Equal(0u, TypeData().CountDay);
        Assert.Equal(0, player.EnterDungeon(dungeonId).Code);
        Assert.Equal(0, player.FinishDungeon(dungeonId, true, false, 0).Code);

        Assert.Equal(1u, TypeData().CountDay);
        Assert.Equal(1u, player.Dungeons.ToDataNotification(DateTimeOffset.UtcNow).Info.Single(t => t.Type == 2).CountDay);
    }

    private Player Fresh(bool extraMember = false)
    {
        var player = new Player(1, Assets);
        player.Characters.GrantStarter(player.Guid);
        player.Teams.GrantStarter(player.Characters);
        if (extraMember)
        {
            var extra = fixture.Rows("P_CharacterTable").Select(r => r.GetProperty("id").GetUInt32())
                .First(id => Assets.Characters.Exists(id) && player.Characters.InstanceOf(id) is null);
            Assert.True(player.Characters.Add(player.Guid, extra).Ok);
            Assert.Equal(0, player.Teams.SetMembers(player.Teams.Current,
                player.Characters.All.Take(2).Select((c, i) => ((uint)i + 1, c.InstId, c.CharacterId)).ToArray(), player.Characters));
        }
        return player;
    }

    [Theory]
    [InlineData(21206015u)]
    [InlineData(21206016u)]
    [InlineData(21206017u)]
    [InlineData(21206018u)]
    public void FoodWithHealingAndTimedBuff_AppliesBothEffects(uint itemId)
    {
        var player = Fresh();
        var effect = Assert.IsType<ItemUseEffect>(Assets.ItemEffects.Effect(itemId));
        var healing = Assert.Single(effect.Effects, e => e.FirstAttributeId == Assets.Inside.Attr.Hp);
        Assert.Equal(1, healing.TargetType);
        Assert.Equal(200, healing.FirstValue);
        var buffId = Assert.Single(effect.TimedBuffs);
        var members = player.CurrentTeamMembers();
        Assert.NotEmpty(members);
        foreach (var id in members) Assert.Equal(0, player.Characters.SetHp(id, 1));
        Assert.Equal(1u, player.Bag.Add(itemId, 1).Stored);

        var result = player.UseItem(itemId, 1, []);

        Assert.Equal(0, result.Code);
        Assert.Equal(1u, result.Used);
        Assert.Equal(0u, player.Bag.CountOf(itemId));
        Assert.Contains(buffId, player.Buffs.Buffs.Keys);
        foreach (var id in members)
        {
            var expected = Math.Min(player.Characters.MaxHp(id), 1 + healing.FirstValue);
            output.WriteLine($"Item {itemId}, character {id}: expected HP {expected}, actual HP {player.Characters.Hp(id)}; timed buff {buffId} applied.");
            Assert.Equal(expected, player.Characters.Hp(id));
        }
    }

    [Theory]
    [InlineData(21206001u, false)]
    [InlineData(21206010u, true)]
    public void HealingItem_RespectsItsReviveFlag(uint itemId, bool revives)
    {
        var player = Fresh();
        var id = player.CurrentTeamMembers().First();
        var effect = Assert.IsType<ItemUseEffect>(Assets.ItemEffects.Effect(itemId));
        Assert.Equal(revives, effect.Revive);
        Assert.Equal(0, player.Characters.SetHp(id, 0));
        Assert.Equal(1u, player.Bag.Add(itemId, 1).Stored);

        var result = player.UseItem(itemId, 1, [id]);

        output.WriteLine($"Item {itemId}, revive={revives}: used {result.Used}, HP after use {player.Characters.Hp(id)}.");
        if (revives)
        {
            Assert.Equal(0, result.Code);
            Assert.Equal(1u, result.Used);
            Assert.True(player.Characters.Hp(id) > 0);
        }
        else
        {
            Assert.Equal(0, player.Characters.Hp(id));
            Assert.Equal(0u, result.Used);
            Assert.Equal(1u, player.Bag.CountOf(itemId));
            Assert.Empty(player.DrainGameplayChanges());
            if (Assets.ItemEffects.Cooldown(itemId) is {} cooldown)
                Assert.True(player.Cooldowns.ReadyAt(cooldown.TypeId) <= DateTimeOffset.UtcNow);
        }
    }

    [Fact]
    public void MixedFood_HealsLivingMembersWithoutRevivingFallenMembers_AndPublishesBothEffects()
    {
        var player = Fresh(extraMember: true);
        var members = player.CurrentTeamMembers();
        Assert.True(members.Count > 1);
        foreach (var id in members) player.Characters.SetHp(id, 1);
        player.Characters.SetHp(members[0], 0);
        player.Bag.Add(21206015, 2);
        player.Bag.DrainChanged();
        var satiety = player.Progress.Satiety;

        Assert.Equal(1u, player.UseItem(21206015, 1, []).Used);

        Assert.Equal(0, player.Characters.Hp(members[0]));
        foreach (var id in members.Skip(1)) Assert.Equal(Math.Min(201, player.Characters.MaxHp(id)), player.Characters.Hp(id));
        var changes = player.DrainGameplayChanges();
        // Healed members first, then every character again for the team buff the food adds.
        var attributes = changes.OfType<SCOutsideAttribNtf>().Select(n => n.Data.InstId).ToList();
        Assert.Equal(members.Skip(1), attributes.Take(members.Count - 1));
        Assert.Equal(player.Characters.All.Select(c => c.InstId), attributes.Skip(members.Count - 1));
        Assert.Single(changes.OfType<SCBuffAdd>());
        Assert.Single(changes.OfType<SCItemCDNtf>());
        Assert.Equal(1u, Assert.Single(Assert.Single(changes.OfType<SCItemBagChangeNtf>()).Items).ItemNum);
        Assert.Equal(satiety + Assets.ItemEffects.Effect(21206015)!.Satiety, player.Progress.Satiety);
        Assert.NotEqual(0, player.UseItem(21206015, 1, []).Code);
        Assert.Empty(player.DrainGameplayChanges());
    }

    [Fact]
    public void WantedDefeat_KeepsFallenCharactersEligibleForPaidRevival()
    {
        var player = Fresh();
        Assert.Equal(0, player.EnterWanted(10101));
        // Select a real first-step battle deterministically from the configured pool.
        var run = player.Wanted.CaptureRun()!;
        var process = Assets.Wanted.StepsOf(run.RouteId, 1).First();
        var eventId = Assets.Policy.Wanted.EventPools[process.Pool]
            .First(id => Assets.Wanted.Npc(id)?.NpcType == (uint)WantedNpcType.NormalBattle);
        player.Wanted.Load([], run with {
            Current = new WantedStepSnapshot(EnmWantedStepStatus.EnmWssStart, process.Id, eventId, false, [])
        });
        var members = player.CurrentTeamMembers();
        Assert.NotEmpty(members);
        Assert.True(player.Wanted.NextReviveCost() > 0);
        var npc = Assets.Wanted.Npc(eventId);
        Assert.NotNull(npc);
        var battlefield = npc.Params;
        Assert.NotEqual(0u, battlefield);
        Assert.Equal(0, player.Battles.Enter(EBattleType.EnmBattleTypeWanted, battlefield, 123, default));
        Assert.Equal(0, player.Battles.Start(EBattleType.EnmBattleTypeWanted, battlefield));
        var report = new CSLeaveBattle {
            BattleType = EBattleType.EnmBattleTypeWanted,
            BattleFieldId = battlefield,
            BattleInstId = 123,
            BattleResult = EBattleResultType.EnmBattleResultTypeDeadFail,
            CharacterData = { members.Select(id => new CharacterAttribInfo {
                InstId = id, CurrentHp = 0, PermanentLiquid = 0
            }) }
        };

        var result = player.LeaveBattle(report);

        Assert.Equal(0, result.Result);
        Assert.True(player.Wanted.IsRunning);
        Assert.Equal(0u, player.Wanted.ReviveCount);
        Assert.False(result.WantedStepCompleted);
        output.WriteLine($"Wanted battlefield {battlefield}: defeat reported HP=0 for all members; server HP: {string.Join(", ", members.Select(player.Characters.Hp))}.");
        Assert.All(members, id => Assert.Equal(0, player.Characters.Hp(id)));
        Assert.Equal(0, player.Wallet.Credit((int)MoneyType.ThoughtSand, 1000));
        Assert.Equal(0, player.BuyWantedRevive(members));
        Assert.Equal(1u, player.Wanted.ReviveCount);
        Assert.All(members, id => Assert.True(player.Characters.Hp(id) > 0));
    }

    [Fact]
    public void DungeonEntry_UsesConfiguredZeroPermanentLiquid()
    {
        const uint dungeonId = 201001;
        var player = Fresh();
        var dungeon = Assets.Dungeons.Dungeon(dungeonId)!;
        var type = Assets.Dungeons.Type(dungeon.DungeonType)!;
        Assert.Equal(0u, type.CharacterPermanentLiquidRatio);
        var members = player.CurrentTeamMembers();
        foreach (var id in members)
        {
            Assert.True(player.Characters.PermanentLiquidMax(id) > 0);
            Assert.Equal(0, player.Characters.SetPermanentLiquid(id, player.Characters.PermanentLiquidMax(id)));
        }
        player.Progress.Load(1, 0, 0, 240, DateTimeOffset.UtcNow);

        Assert.Equal(0, player.EnterDungeon(dungeonId).Code);

        foreach (var id in members)
        {
            output.WriteLine($"Dungeon {dungeonId}, character {id}: configured liquid ratio 0, expected 0, actual {player.TeamCharacterLiquid(id)}.");
            Assert.Equal(0, player.TeamCharacterLiquid(id));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DungeonEntry_RunsOnSeparateVitals_AndLeavesTheOwnedCharactersUntouched(bool adopt)
    {
        const uint dungeonId = 201001;
        var player = Fresh(extraMember: true);
        var members = player.CurrentTeamMembers();
        Assert.True(members.Count > 1);
        foreach (var id in members) player.SetTeamCharacterVitals(id, hp: 23, liquid: 77);
        var teamSource = Assets.Dungeons.Dungeon(dungeonId)!.DungeonType;
        var type = Assets.Dungeons.Type(teamSource)!;
        var selection = player.QueryTemporaryTeam((int)EnmTmpTeamType.Dungeon, teamSource)!;
        var selected = selection.MemberData[0].Clone();
        selection.MemberData.Clear();
        selection.MemberData.Add(selected);
        Assert.Equal(0, player.UpdateTemporaryTeam((int)EnmTmpTeamType.Dungeon, teamSource, selection).Result);
        player.Progress.Load(1, 0, 0, 240, DateTimeOffset.UtcNow);
        var battleId = Assets.Dungeons.Dungeon(dungeonId)!.BattleId.First();

        var entry = adopt ? player.AdoptDungeonCurrent(dungeonId, battleId) : player.EnterDungeon(dungeonId);

        // The run uses the dungeon type's ratios, the characters keep what they entered with.
        Assert.Equal(0, entry.Code);
        Assert.Equal(Lunaria.Game.Characters.Teams.TempTeamManager.Ratio(player.Characters.MaxHp(selected.InstId), type.CharacterHpRatio),
            player.TeamCharacterHp(selected.InstId));
        Assert.Equal(Lunaria.Game.Characters.Teams.TempTeamManager.Ratio(player.Characters.PermanentLiquidMax(selected.InstId), type.CharacterPermanentLiquidRatio),
            player.TeamCharacterLiquid(selected.InstId));
        Assert.All(members, id => Assert.Equal(23, player.Characters.Hp(id)));
        Assert.All(members, id => Assert.Equal(77, player.Characters.PermanentLiquid(id)));

        // Battle damage stays on the run, also across a resume.
        player.SetTeamCharacterVitals(selected.InstId, hp: 5, liquid: 39);
        var stamina = player.Progress.Stamina;
        Assert.NotEqual(0, player.EnterDungeon(dungeonId).Code);
        Assert.Equal(0, player.AdoptDungeonCurrent(dungeonId, battleId).Code);
        player.ReconcileTemporaryTeam();
        Assert.Equal(5, player.TeamCharacterHp(selected.InstId));
        Assert.Equal(39, player.TeamCharacterLiquid(selected.InstId));
        Assert.Equal(stamina, player.Progress.Stamina);
        Assert.Equal(23, player.Characters.Hp(selected.InstId));

        // Leaving the run restores the characters' own vitals.
        Assert.Equal(0, player.FinishDungeon(dungeonId, false, true, 0).Code);
        Assert.Equal(23, player.TeamCharacterHp(selected.InstId));
        Assert.Equal(77, player.TeamCharacterLiquid(selected.InstId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnaffordableDungeonEntry_PreservesVitalsAndTeam(bool adopt)
    {
        const uint dungeonId = 201001;
        var player = Fresh();
        var members = player.CurrentTeamMembers();
        foreach (var id in members) player.SetTeamCharacterVitals(id, hp: 23, liquid: 77);
        player.Progress.Load(1, 0, 0, 0, DateTimeOffset.UtcNow);

        var entry = adopt ? player.AdoptDungeonCurrent(dungeonId, Assets.Dungeons.Dungeon(dungeonId)!.BattleId.First())
            : player.EnterDungeon(dungeonId);

        Assert.Equal((int)EnmTextCode.EnmTextStaminaNotEnough, entry.Code);
        Assert.Null(player.Dungeons.Current);
        Assert.Null(player.ActiveTemporaryTeam);
        Assert.All(members, id => Assert.Equal(77, player.Characters.PermanentLiquid(id)));
        Assert.All(members, id => Assert.Equal(23, player.Characters.Hp(id)));
        Assert.Empty(player.DrainGameplayChanges());
    }

    [Fact]
    public void CreatureRewardStoredAtCapacity_CanBeCollectedAfterReleasingOne()
    {
        const uint itemId = 29900001;
        var player = Fresh();
        var item = Assets.Items.Get(itemId)!;
        Assert.Equal((int)ItemUseType.AddSilverCreature, item.UseType);
        var cost = Assets.SilverCreatures.Growth(item.Param[0])!.Cost;
        Assert.True(cost > 0);
        var capacity = Math.Min(Assets.GlobalConfig.MaxSilverCreatureNum,
            Assets.GlobalConfig.MaxSilverCreatureCost / (int)cost);
        Assert.InRange(capacity, 1, 1000);

        var granted = player.GrantRewards([new ItemGrant(itemId, (uint)capacity + 1)], EnmItemReason.EnmItemChangeNormal);

        Assert.Equal(capacity, player.SilverCreatures.Creatures.Count);
        Assert.Equal(new ItemGrant(itemId, 1), Assert.Single(granted.CreatureFailures));
        Assert.Equal(1u, player.Bag.CountOf(itemId));
        Assert.Equal(0, player.SilverCreatures.Release([player.SilverCreatures.Creatures.Keys.First()]).Result);
        player.AdvanceTime(DateTimeOffset.UtcNow);
        Assert.Equal(capacity, player.SilverCreatures.Creatures.Count);
        Assert.Equal(0u, player.Bag.CountOf(itemId));
    }

    [Theory]
    [InlineData("release")]
    [InlineData("combine")]
    [InlineData("login")]
    public void StoredCreatureRetry_ConsumesOnceWithoutRepeatingAcquisitionOrRewardXp(string trigger)
    {
        const uint itemId = 29900001;
        var player = Fresh();
        var cost = Assets.SilverCreatures.Growth(Assets.Items.Get(itemId)!.Param[0])!.Cost;
        var capacity = Math.Min(Assets.GlobalConfig.MaxSilverCreatureNum, Assets.GlobalConfig.MaxSilverCreatureCost / (int)cost);
        player.GrantRewards([new ItemGrant(itemId, (uint)capacity + 1)], EnmItemReason.EnmItemChangeNormal);
        player.DrainGameplayChanges();
        var experience = (player.Progress.TeamLevel, player.Progress.TeamExp);
        var events = Assets.Unlocks.ArgEvents(GlobalEventSub.AddItemType, (ulong)Assets.Items.Get(itemId)!.ShowType)
            .ToDictionary(id => id, player.Achievements.ProgressOf);
        SilverCreatureChange? change = null;

        if (trigger == "release")
        {
            var result = player.ReleaseSilverCreatures([player.SilverCreatures.Creatures.Keys.First()]);
            Assert.Equal(0, result.Result);
            change = result.Change;
        }
        else if (trigger == "combine")
        {
            var result = player.CombineSilverCreatures(player.SilverCreatures.Creatures.Keys
                .Take((int)Assets.SilverCreatures.Combine(itemId)!.SrcItemNum).ToArray());
            Assert.Equal(0, result.Result);
            change = result.Change;
        }
        else
        {
            player.SilverCreatures.Release([player.SilverCreatures.Creatures.Keys.First()]);
            player.InitializeRoleState(DateTimeOffset.UtcNow, hasSave: true);
        }

        Assert.Equal(capacity, player.SilverCreatures.Creatures.Count);
        Assert.Equal(0u, player.Bag.CountOf(itemId));
        if (change is not null)
        {
            // The reply keeps only its own result: the fused creature on combine, nothing on release.
            Assert.Equal(trigger == "combine" ? 1 : 0, change.AddList.Count);
            Assert.DoesNotContain(change.AddList, c => c.ItemId == itemId);
            var ntf = Assert.Single(player.DrainGameplayChanges().OfType<SCSilverCreatureChangeNtf>());
            Assert.Single(ntf.Change.AddList, c => c.ItemId == itemId);
        }
        var roster = player.SilverCreatures.ToList();
        player.AdvanceTime(DateTimeOffset.UtcNow);
        player.AdvanceTime(DateTimeOffset.UtcNow);
        Assert.Equal(roster, player.SilverCreatures.ToList());
        Assert.Equal(experience, (player.Progress.TeamLevel, player.Progress.TeamExp));
        foreach (var (id, count) in events) Assert.Equal(count, player.Achievements.ProgressOf(id));
    }

    // Control for the ten baseline failures: their helper never enters the map
    // required by the story step. These cases set that precondition explicitly.
    [Theory]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(4u)]
    [InlineData(5u)]
    [InlineData(6u)]
    [InlineData(7u)]
    [InlineData(8u)]
    [InlineData(9u)]
    [InlineData(10u)]
    [InlineData(11u)]
    public void StoryTrialTeam_OnItsConfiguredMap_ResolvesMembers(uint source)
    {
        var player = Fresh();
        var row = Assets.TmpTeams.GetBySrc(source)!;
        var step = row.StepId.First(s => Assets.Tasks.TaskOfStep(row.TaskType, s) != 0);
        var map = Assets.Tasks.Actions(row.TaskType, step)
            .Select(id => Assets.Tasks.Action(row.TaskType, id)!)
            .Where(action => action.MapId > 0).Select(action => action.MapId).FirstOrDefault();
        if (map != 0)
        {
            // MapID can denote a level prefix. Pick a real map covered by that prefix.
            var playable = fixture.Rows("P_MapDataTable").Select(r => r.GetProperty("id").GetUInt64())
                .First(id => TaskManager.MatchesMap(map, id));
            player.Map.Load(playable, Assets.Starter.Savepoint, [Assets.Starter.Savepoint], [], (0, 0, 0));
        }
        player.Tasks.Load([(row.TaskType, Assets.Tasks.TaskOfStep(row.TaskType, step), step,
            Array.Empty<(ulong, uint, uint)>().AsEnumerable())], []);

        Assert.True(player.ReconcileTemporaryTeam());
        Assert.Equal(source, player.ActiveTemporaryTeam!.Source);
        Assert.Equal(row.TmpCharacters.Count, player.CurrentTeamMembers().Count);
        Assert.All(player.CurrentTeamMembers(), id => Assert.True(player.TeamCharacterHp(id) > 0));
    }
}
