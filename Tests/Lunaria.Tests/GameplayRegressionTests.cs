using System.Text.Json;
using Lunaria.Game.Characters;
using Lunaria.Game.Player;
using Lunaria.Game.Player.Persistence.Saves;
using Lunaria.Game.Resources;
using Lunaria.Game.Resources.Tables;
using Lunaria.Game.Tasks;
using Msg;
using Xunit;

namespace Lunaria.Tests;

[CollectionDefinition("bundled-gameplay", DisableParallelization = true)]
public sealed class BundledGameplayCollection : ICollectionFixture<BundledGameplayFixture>;

public sealed class BundledGameplayFixture
{
    public BundledGameplayFixture()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "assets", "tables")))
            directory = directory.Parent;
        Root = Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("Bundled assets/tables not found"), "assets");
        Data = new GameData(Root);
        Data.StartAsync(default).GetAwaiter().GetResult();
    }

    public GameData Data { get; }
    public string Root { get; }

    public JsonElement[] Rows(string table)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "tables", table + ".json")));
        return document.RootElement.EnumerateObject().Single().Value.EnumerateObject().Select(p => p.Value.Clone()).ToArray();
    }
}

[Collection("bundled-gameplay")]
public sealed class GameplayRegressionTests(BundledGameplayFixture fixture)
{
    [Fact]
    public void Attributes_StackLevelBreakTalentsAndMotive_LikeTheClientFormula()
    {
        // Munin level 30 break 1, talent nodes 1 and 3 (MAXHP +578 each), Motive 12031001 level 10 (ATK +49, +10.92%).
        var player = new Player(1, fixture.Data);
        player.Characters.GrantStarter(player.Guid);
        player.Skills.GrantStarter(player.Characters);
        var munin = player.Characters.All.Single(c => c.CharacterId == 1001);
        player.Characters.Load([munin with { Level = 30, BreakLevel = 1 }]);
        var motive = player.Motives.Add(player.Guid, 12031001, 1).UniqId;
        player.Motives.Load([player.Motives.Get(motive)! with { Level = 10 }]);
        player.Skills.Load(player.Skills.SkillGroups(),
            [(munin.InstId, new TalentMasks().WithUnlock(0).WithUnlock(1).WithUnlock(2).WithUnlock(3))], player.Characters);
        Assert.Equal(0, player.EquipMotive(motive, munin.InstId));
        var attr = fixture.Data.Inside.Attr;
        PBAttribDataElem Attr(int id) => player.Characters.AttribData(munin.InstId).AttribData.Single(a => a.AttribType == id);

        // p_developattributetable 100130 (4095, 240, 585, 25%, 80%) plus the break 1 row 1001201 (420, 25, 60).
        Assert.Equal((4095 + 420 + 2 * 578) * 10_000, Attr(attr.Maxhp).FinalValue);
        Assert.Equal((585 + 60) * 10_000, Attr(attr.Def).FinalValue);
        Assert.Equal(2500, Attr(attr.AtkCriticalChance).FinalValue);
        Assert.Equal(8000, Attr(attr.AtkCriticalDamage).FinalValue);
        Assert.Equal(Attr(attr.Maxhp).FinalValue, Attr(attr.Hp).FinalValue);
        Assert.Contains(player.Characters.AttribData(munin.InstId).AttribData, a => a.AttribType == 1149 && a.FinalValue > 0);

        // OutsideAttributeData formula 1: base = 240 + 25 + 49, extra = floor(base * 10.92%).
        Assert.Equal(314 * 10_000, Attr(attr.Atk).BaseValue);
        Assert.Equal(314 * 10_000 + 314 * 1092, Attr(attr.Atk).FinalValue);
    }

    private GameData Assets => fixture.Data;

    private Player Fresh()
    {
        var player = new Player(sessionId: 1, Assets);
        player.Characters.GrantStarter(player.Guid);
        player.Teams.GrantStarter(player.Characters);
        return player;
    }

    private PTaskActionsTyped AtAction(Player player, ulong actionId, uint type = TaskAssets.QuestMain)
    {
        var action = Assets.Tasks.Action(type, actionId)!;
        var step = Assets.Tasks.StepOfAction(type, actionId);
        var task = Assets.Tasks.TaskOfStep(type, step);
        Assert.NotEqual(expected: 0u, task);
        player.Tasks.Load([(type, task, step, Array.Empty<(ulong, uint, uint)>().AsEnumerable())], []);
        return action;
    }

    [Fact]
    public void OpenWorldEntry_SettlesInstantMarkers_EnvironmentLandsInEntryReply()
    {
        var player = Fresh();
        player.Tasks.Load(
            [(TaskAssets.QuestMain, TaskAssets.OpeningTaskId, 101004001, Array.Empty<(ulong, uint, uint)>().AsEnumerable())],
            []);
        player.Map.BeginEnter(211001001001, 0);
        player.Map.FinishEnter();

        _ = player.SettleMapArrival(211001001001);

        Assert.True(player.Tasks.IsFinished(TaskAssets.QuestMain, TaskAssets.OpeningTaskId));
        Assert.Equal((WeatherType)3, player.CurrentWeather);
        Assert.Equal(540u, player.GameTimeMinutes);

        Assert.DoesNotContain(
            player.SettleMapArrival(211001001001).SelectMany(o => o.AllNotifications),
            n => n is SCGameTimeSlipNtf);
    }

    [Fact]
    public void CharacterExperience_OverCapPays_AlreadyCappedPaysNothing()
    {
        var player = Fresh();
        var id = player.Characters.All.First().InstId;
        player.Bag.Add(itemId: 11201001, count: 1000);
        var result = player.LevelUpCharacter(id, [new ItemGrant(ItemId: 11201001, Count: 1000)]);
        Assert.Equal(expected: 0, result.Code);
        Assert.Equal(player.Characters.LevelCap(id), player.Characters.Get(id)!.Level);
        Assert.Equal(expected: 0u, player.Bag.CountOf(11201001));
        var update = Assert.Single(player.DrainGameplayChanges().OfType<SCCharacterUpdateNtf>());
        Assert.Equal(player.Characters.ToCharacterData(player.Characters.Get(id)!), Assert.Single(update.Characters));
        player.Bag.Add(itemId: 11201001, count: 1);
        Assert.NotEqual(expected: 0, player.LevelUpCharacter(id, [new ItemGrant(ItemId: 11201001, Count: 1)]).Code);
        Assert.Equal(expected: 1u, player.Bag.CountOf(11201001));
        Assert.Empty(player.DrainGameplayChanges());
    }

    [Fact]
    public void Healing_AllCharactersAndFullHealthItems()
    {
        var player = Fresh();

        var extra = fixture.Rows("P_CharacterTable").Select(r => r.GetProperty("id").GetUInt32())
            .First(id => player.Characters.InstanceOf(id) is null);
        player.Characters.Add(player.Guid, extra);
        var ids = player.Characters.All.Take(2).Select(c => c.InstId).ToArray();

        foreach (var id in ids)
        {
            player.Characters.SetHp(id, value: 1);
        }
        Assert.Equal(expected: 2, player.Characters.HealAll().Count);
        Assert.All(ids, id => Assert.Equal(player.Characters.MaxHp(id), player.Characters.Hp(id)));
        player.Bag.Add(itemId: 21206001, count: 1);
        var used = player.UseItem(itemId: 21206001, count: 1, [ids[0]]);
        Assert.Equal(expected: 0u, used.Used);
        Assert.Equal(expected: 1u, player.Bag.CountOf(21206001));
    }

    [Fact]
    public void Grants_RespectManualUseAndEveryCopy()
    {
        var player = Fresh();
        var stamina = player.Progress.Stamina;

        player.GrantRewards([
            new ItemGrant(ItemId: 21308001, Count: 3), new ItemGrant(ItemId: 12031001, Count: 3), new ItemGrant(ItemId: 60100001, Count: 3)
        ], EnmItemReason.EnmItemChangeNormal);
        Assert.Equal(stamina, player.Progress.Stamina);
        Assert.Equal(expected: 3u, player.Bag.CountOf(21308001));
        Assert.Equal(expected: 3, player.Motives.Count);
        Assert.Equal(expected: 450000, player.Wallet.Balance(1));
    }

    [Fact]
    public void Progression_LoadPreservesLevel_AndExperienceCrossesEarnedWorldTier()
    {
        var player = Fresh();
        player.Progress.QuestGate = _ => true;
        player.Progress.Load(teamLevel: 35, teamExp: 0, satiety: 0, stamina: 0, DateTimeOffset.UtcNow);
        Assert.Equal(expected: 35u, player.Progress.TeamLevel);
        player.Progress.Load(teamLevel: 19, teamExp: 0, satiety: 0, stamina: 0, DateTimeOffset.UtcNow);
        var exp = Assets.Progression.TeamExpToAdvance(19)!.Value + Assets.Progression.TeamExpToAdvance(20)!.Value;
        player.Progress.AddTeamExp(exp);
        Assert.Equal(expected: 21u, player.Progress.TeamLevel);
    }

    [Fact]
    public void HouseIncome_UsesClientFormula_AndPreservesUnclaimedIncomeOnUpgrade()
    {
        var player = Fresh();
        var start = DateTimeOffset.UtcNow.AddHours(-2);
        player.Houses.MarkBought(houseId: 1, start);
        player.Houses.MarkOpened(houseId: 1, start);
        Assert.Equal(expected: 530u, Assets.Houses.IncomePerInterval(houseId: 1, level: 1));
        Assert.Equal(expected: 1060u, player.Houses.AccruedIncome(houseId: 1, start.AddHours(2)));
        Assert.Equal(expected: 100u, player.Houses.NextUpgradeCost(1));
        player.Houses.MarkUpgraded(houseId: 1, start.AddHours(2));
        Assert.Equal(expected: 1060u, player.Houses.AccruedIncome(houseId: 1, start.AddHours(2)));
        var delivery = player.ClaimHouseIncome();
        Assert.Equal(expected: 1060u, Assert.Single(delivery.Credited).Count);
        Assert.Equal(expected: 1060, player.Wallet.Balance((int)MoneyType.HouseCoins));
        Assert.False(player.ClaimHouseIncome().HasChanges);
    }

    [Fact]
    public void HouseIncome_ClaimKeepsPartialInterval()
    {
        var player = Fresh();
        var start = DateTimeOffset.FromUnixTimeSeconds(1700000000);
        player.Houses.MarkBought(houseId: 1, start);
        player.Houses.MarkOpened(houseId: 1, start);
        player.Houses.MarkIncomeClaimed([1], start.AddMinutes(90));
        Assert.Equal(expected: 0u, player.Houses.AccruedIncome(houseId: 1, start.AddMinutes(119)));
        Assert.Equal(expected: 530u, player.Houses.AccruedIncome(houseId: 1, start.AddMinutes(120)));
    }

    [Fact]
    public void SignIn_OneAttendancePerUtcDay_IncludingReload()
    {
        var player = Fresh();
        var activity = Assets.SignIn.Activity(3)!;
        var now = DateTimeOffset.FromUnixTimeSeconds((long)activity.TimeOffsetStart).AddHours(1);
        for (var i = 0; i < 7; i++) Assert.Equal(expected: 0, player.SignIn.Query(activityId: 3, now).Result);
        Assert.Single(player.SignIn.SignedDays);
        Assert.NotEqual(expected: 0, player.SignIn.Claim(activityId: 3, day: 5).Result);
        Assert.Equal(expected: 0, player.SignIn.Claim(activityId: 3, day: 1).Result);
        Assert.NotEqual(expected: 0, player.SignIn.Claim(activityId: 3, day: 1).Result);
        var restored = Fresh();
        restored.SignIn.Load(player.SignIn.SignedDays, player.SignIn.ClaimedDays, player.SignIn.LastSignInDay);
        restored.SignIn.Query(activityId: 3, now);
        Assert.Single(restored.SignIn.SignedDays);
        restored.SignIn.Query(activityId: 3, now.AddDays(1));
        Assert.Equal(expected: 2, restored.SignIn.SignedDays.Count);
    }

    [Fact]
    public void Collections_ListingAndDiscardingDoNotCountAsGathering()
    {
        var player = Fresh();
        var now = DateTimeOffset.UtcNow;
        var block = Assets.Collections.WorldObjects(blockId: 0)[0].BlockId;
        var nodes = player.GetCollections(block, now);
        Assert.Empty(player.RecalculateRegionProgress());
        Assert.Empty(player.Collections.Gathered);
        Assert.Empty(player.Collections.Entries);
        Assert.Equal(expected: 0, player.Collections.ApplyDestroyed(nodes[0].UniqId, now).Code);
        Assert.Empty(player.Collections.Gathered);
        Assert.Equal(expected: 0, player.Collections.ApplyCollected(nodes[1].UniqId, now).Code);
        Assert.Contains(nodes[1].CfgId, player.Collections.Gathered);
        player.GetCollections(block, now.AddDays(30));
        Assert.Contains(nodes[1].CfgId, player.Collections.Gathered);
    }

    [Fact]
    public void Collections_ListThePlacedObjectsTheClientBindsByRowId()
    {
        var player = Fresh();
        var placed = Assets.Collections.WorldObjects(blockId: 0)[0];
        var listed = player.GetCollections(placed.BlockId, DateTimeOffset.UtcNow);

        Assert.Equal(Assets.Collections.WorldObjects(placed.BlockId).Count, listed.Count);
        Assert.All(listed, item => {
            Assert.Equal(item.UniqId, item.FromId);
            Assert.Equal(EnmCollectionFromType.EcollectFromTable, item.FromType);
            Assert.Equal(placed.BlockId, item.BlockId);
        });
        var first = listed.Single(item => item.UniqId == placed.Id);
        Assert.Equal(placed.TemplateId, first.CfgId);
        Assert.Equal((int)MathF.Round(placed.PosX), first.Location.X);
        Assert.Equal(first.Location, first.FromLocation);
    }

    [Fact]
    public void Collections_ChestsStayOpenedWhileDailyGatherablesComeBackAndArePushed()
    {
        var player = Fresh();
        var now = DateTimeOffset.UtcNow;
        var chest = Assets.Collections.WorldObjects(blockId: 0)
            .First(row => Assets.Collections.Get(row.TemplateId)!.CollectionType == 1
                          && !Assets.Collections.Respawn(row.TemplateId).ResetsEver);
        var daily = Assets.Collections.WorldObjects(blockId: 0)
            .First(row => Assets.Collections.Respawn(row.TemplateId).Kind == RefreshPeriod.Daily);

        Assert.Equal(expected: 0, player.Collections.ApplyCollected(chest.Id, now).Code);
        Assert.Equal(expected: 0, player.Collections.ApplyCollected(daily.Id, now).Code);
        Assert.Null(player.RespawnedCollections(now));

        var pushed = player.RespawnedCollections(now.AddDays(1))!;
        Assert.Equal([daily.Id], pushed.NtfList.Select(item => item.UniqId));
        Assert.Equal(EnmCollectionStatus.EcsCanCollect, pushed.NtfList[0].Status);
        Assert.NotEqual(EnmCollectionStatus.EcsCanCollect, player.Collections.Get(chest.Id)!.Status);
    }

    [Fact]
    public void Collections_LoadDropsRowsThatMatchNoPlacedObject()
    {
        var player = Fresh();
        var placed = Assets.Collections.WorldObjects(blockId: 0)[0];
        var other = Assets.Collections.WorldObjects(blockId: 0).First(row => row.TemplateId != placed.TemplateId);
        var at = DateTimeOffset.UtcNow;
        player.Collections.Load([
            (placed.Id, placed.TemplateId, (int)EnmCollectionStatus.EcsCollected, at, 0UL, (0, 0, 0)),
            (placed.Id + 1_000_000_000, placed.TemplateId, (int)EnmCollectionStatus.EcsCollected, at, 1UL, (0, 0, 0)),
            (other.Id, placed.TemplateId, (int)EnmCollectionStatus.EcsCollected, at, 1UL, (0, 0, 0))
        ]);

        Assert.Equal([placed.Id], player.Collections.Entries.Keys);
        Assert.Equal(placed.BlockId, player.Collections.Get(placed.Id)!.Block);
    }

    [Fact]
    public void Cases_OpeningGrantsTheCaseEvidence_AndOldSavesGetItBack()
    {
        var evidence = Assets.Cases.Evidence(caseId: 1003).Select(row => row.Id).ToArray();
        Assert.NotEmpty(evidence);

        var player = Fresh();
        var opened = player.Cases.OpenCase(1003)!.Value;
        Assert.Equal(evidence, opened.EvidenceIds);
        Assert.Equal(evidence, player.Cases.Processing[1003].OwnedEvidence);
        Assert.Equal(evidence, Lunaria.Game.Player.Managers.CaseManager.ToReceiveNotification(1003, opened.ClueIds, opened.EvidenceIds).EvidenceIds);

        // Older saves did not grant evidence when a case opened.
        var restored = Fresh();
        restored.Cases.Load([(1003u, 1u, new ulong[] { 1003101 }.AsEnumerable(), Array.Empty<ulong>().AsEnumerable())], []);
        restored.Cases.LoadOwned([(1003u, new ulong[] { 1003101, 1003102 }.AsEnumerable(), Array.Empty<ulong>().AsEnumerable())]);
        Assert.Equal(evidence, restored.Cases.Processing[1003].OwnedEvidence);
        Assert.Contains(restored.Cases.ToCaseData().ProcessingCase.Single().EvidenceStatus, s => s.EvidenceId == evidence[0]);
    }

    [Fact]
    public void Cases_FinishedPhaseIsSentAsTheLastFinishedStageId()
    {
        var stages = Assets.Cases.Stages(caseId: 1003);
        var player = Fresh();
        player.Cases.Load([(1003u, 1u, new ulong[] { 1003101 }.AsEnumerable(), Array.Empty<ulong>().AsEnumerable())], []);

        // The client looks up a stage ID in StageIDList. Sending a count would hide the clues.
        Assert.Equal((uint)stages[0], player.Cases.ToCaseData().ProcessingCase.Single().FinishedPhase);
        Assert.Equal(0u, player.Cases.FinishedStageId(1003, 0));
        Assert.Equal((uint)stages[^1], player.Cases.FinishedStageId(1003, (uint)stages.Count));
    }

    private static TeamData WithGems(TeamData team, params (int Member, uint GemSlot, uint GemId)[] gems)
    {
        var copy = team.Clone();
        foreach (var member in copy.MemberData) member.GemSlots.Clear();
        foreach (var (index, slot, gem) in gems)
            copy.MemberData[index].GemSlots.Add(new GemSlotData { GemSlotId = slot, GemItemid = gem, GemState = EnmGemStatus.Valid });
        return copy;
    }

    [Fact]
    public void Gems_EquipAndRoundTripThroughTheTeamAndTheSave()
    {
        var player = Fresh();
        player.Bag.Add(itemId: 21501001, count: 1);
        player.Bag.Add(itemId: 21501002, count: 1);
        var team = player.Teams.ToTeamData(player.Teams.CurrentTeam()!);

        // World level 1 allows a cost of 3: two gems on one character cost 1 + 2.
        var update = player.UpdateTeam(WithGems(team, (0, 1, 21501001), (0, 3, 21501002)));
        Assert.Equal(0, update.Result);
        var slots = update.TeamData.MemberData[0].GemSlots;
        Assert.Equal([(1u, 21501001u), (3u, 21501002u)], slots.Select(s => (s.GemSlotId, s.GemItemid)));
        Assert.All(slots, s => Assert.Equal(EnmGemStatus.Valid, s.GemState));

        var json = JsonSerializer.Serialize(RoleSaveMapper.Capture(player), SaveJson.Options);
        var restored = Fresh();
        RoleSaveMapper.Apply(restored, JsonSerializer.Deserialize<RoleSaveDocument>(json, SaveJson.Options)!);
        Assert.Equal(slots, restored.Teams.ToTeamData(restored.Teams.CurrentTeam()!).MemberData[0].GemSlots);

        Assert.Equal(0, player.UpdateTeam(WithGems(team)).Result);
        Assert.Empty(player.Teams.CurrentTeam()!.Members[0].Gems);
    }

    [Fact]
    public void Gems_RepeatedSlotIdsRejectTheWholeUpdate()
    {
        var player = Fresh();
        player.Bag.Add(itemId: 21501001, count: 1);
        player.Bag.Add(itemId: 21501002, count: 1);
        var team = player.Teams.ToTeamData(player.Teams.CurrentTeam()!);
        Assert.Equal(0, player.UpdateTeam(WithGems(team, (0, 2, 21501001))).Result);
        var before = player.Teams.ToTeamData(player.Teams.CurrentTeam()!);
        player.Teams.ClearDirty();

        foreach (var firstGem in new uint[] { 0, 21501001 })
        {
            var update = player.UpdateTeam(WithGems(team, (0, 1, firstGem), (0, 1, 21501002)));
            Assert.Equal((int)EnmTextCode.EnmTextCharacterTeamGemSizeNotMatch, update.Result);
            Assert.Equal(before, update.TeamData);
            Assert.Equal(before, player.Teams.ToTeamData(player.Teams.CurrentTeam()!));
            Assert.False(player.Teams.IsDirty);
        }
    }

    [Fact]
    public void Gems_UseEarnedWorldLevelBudgetAfterSelectingALowerLevel()
    {
        var player = Fresh();
        player.Bag.Add(itemId: 21501001, count: 1);
        player.Bag.Add(itemId: 21501002, count: 1);
        player.Bag.Add(itemId: 21501004, count: 1);
        var proposed = WithGems(player.Teams.ToTeamData(player.Teams.CurrentTeam()!),
            (0, 1, 21501001), (0, 2, 21501002), (0, 3, 21501004));
        Assert.Equal((int)EnmTextCode.EnmTextCharacterTeamGemCostNotEnough, player.UpdateTeam(proposed).Result);

        player.Progress.QuestGate = _ => true;
        player.Progress.Load(20, 0, 0, 240, DateTimeOffset.UtcNow);
        Assert.Equal(2u, player.Progress.EarnedWorldLevel);
        Assert.Equal(0, player.Progress.SelectWorldLevel(1).Result);
        Assert.Equal(1u, player.Progress.WorldLevel);

        var update = player.UpdateTeam(proposed);
        Assert.Equal(0, update.Result);
        Assert.Equal(proposed.MemberData[0].GemSlots, update.TeamData.MemberData[0].GemSlots);
    }

    [Fact]
    public void Gems_RejectUnownedUnknownDuplicateOutOfRangeAndOverBudget()
    {
        var player = Fresh();
        player.Bag.Add(itemId: 21501001, count: 1);
        player.Bag.Add(itemId: 21501002, count: 1);
        player.Bag.Add(itemId: 21501003, count: 1);
        var team = player.Teams.ToTeamData(player.Teams.CurrentTeam()!);

        Assert.Equal((int)EnmTextCode.EnmTextCharacterTeamGemNotOwned, player.UpdateTeam(WithGems(team, (0, 1, 21501004))).Result);
        Assert.Equal((int)EnmTextCode.EnmTextCharacterTeamGemNotExist, player.UpdateTeam(WithGems(team, (0, 1, 999))).Result);
        Assert.Equal((int)EnmTextCode.EnmTextCharacterTeamGemDuplicate,
            player.UpdateTeam(WithGems(team, (0, 1, 21501001), (0, 2, 21501001))).Result);
        Assert.Equal((int)EnmTextCode.EnmTextCharacterTeamGemSizeNotMatch, player.UpdateTeam(WithGems(team, (0, 4, 21501001))).Result);
        // Three gems on one character cost 1 + 2 + 4 = 7, over world level 1's budget of 3.
        Assert.Equal((int)EnmTextCode.EnmTextCharacterTeamGemCostNotEnough,
            player.UpdateTeam(WithGems(team, (0, 1, 21501001), (0, 2, 21501002), (0, 3, 21501003))).Result);

        Assert.All(player.Teams.CurrentTeam()!.Members, m => Assert.Empty(m.Gems));
    }

    [Theory]
    [InlineData(21501011u, 1)]
    [InlineData(21501010u, 2)]
    [InlineData(21501009u, 3)]
    public void Gems_ElementStatusFollowsTeamCompositionAndSaveReload(uint gemId, int requiredFireMembers)
    {
        var player = Fresh();
        player.Bag.Add(gemId, 1);
        var team = player.Teams.ToTeamData(player.Teams.CurrentTeam()!);
        Assert.Equal(1001u, Assert.Single(team.MemberData).CharacterId); // Gravitas, not Ignis.
        var update = player.UpdateTeam(WithGems(team, (0, 1, gemId)));
        Assert.Equal(0, update.Result);
        Assert.Equal(EnmGemStatus.Invalid, Assert.Single(update.TeamData.MemberData[0].GemSlots).GemState);

        uint[] fireCharacters = [1004, 1501, 1505];
        for (var i = 0; i < requiredFireMembers; i++)
        {
            var granted = player.Characters.Add(player.Guid, fireCharacters[i]);
            Assert.Equal(0, granted.Code);
            team = update.TeamData.Clone();
            team.MemberData.Add(new TeamMemberData {
                MemberSlotId = (uint)i + 2, InstId = granted.InstId, CharacterId = fireCharacters[i]
            });
            update = player.UpdateTeam(team);
            Assert.Equal(0, update.Result);
            Assert.Equal(i + 1 == requiredFireMembers ? EnmGemStatus.Valid : EnmGemStatus.Invalid,
                Assert.Single(update.TeamData.MemberData[0].GemSlots).GemState);
        }

        var json = JsonSerializer.Serialize(RoleSaveMapper.Capture(player), SaveJson.Options);
        var restored = Fresh();
        restored.Characters.Load(player.Characters.All);
        RoleSaveMapper.Apply(restored, JsonSerializer.Deserialize<RoleSaveDocument>(json, SaveJson.Options)!);
        team = restored.Teams.ToTeamData(restored.Teams.CurrentTeam()!);
        Assert.Equal(EnmGemStatus.Valid, Assert.Single(team.MemberData[0].GemSlots).GemState);

        // An earlier VALID status from the client cannot keep a gem active after its requirement is lost.
        team.MemberData.RemoveAt(team.MemberData.Count - 1);
        update = restored.UpdateTeam(team);
        Assert.Equal(0, update.Result);
        var slot = Assert.Single(update.TeamData.MemberData[0].GemSlots);
        Assert.Equal(gemId, slot.GemItemid);
        Assert.Equal(EnmGemStatus.Invalid, slot.GemState);
    }

    [Fact]
    public void WantedShop_WithoutRunRejectsWithoutCharging()
    {
        var player = Fresh();
        player.Wallet.Credit((int)MoneyType.ThoughtSand, amount: 1000);
        Assert.NotEqual(expected: 0, player.BuyWantedShopGood(shopId: 1001, goodsId: 10001, buyCount: 1).Result);
        Assert.Equal(expected: 1000, player.Wallet.Balance((int)MoneyType.ThoughtSand));
    }

    [Fact]
    public void Wanted_AllDifficultyRoutesFinish_AndFirstRewardsDoNotRepeat()
    {
        var player = Fresh();
        var entries = Assets.Wanted.AllPosters.SelectMany(Assets.Wanted.EntriesOf).ToArray();
        Assert.NotEmpty(entries);

        foreach (var entry in entries)
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                Assert.Equal(expected: 0, player.EnterWanted(entry.Id));
                FinishWanted(player);
                var result = player.Wanted.Over();
                Assert.Equal(expected: 0, result.Result);

                if (attempt == 0) Assert.NotEmpty(result.Settlement.AwardFirst);
                else Assert.Empty(result.Grants);
            }
        }
    }

    private void FinishWanted(Player player)
    {
        for (var guard = 0; guard < 100; guard++)
        {
            var run = player.Wanted.CaptureRun()!;
            if (run.Step == run.MaxStep && run.Current.Status == EnmWantedStepStatus.EnmWssAwardFinished) return;

            var row = Assets.Wanted.Event(run.Current.EventId);
            Assert.NotNull(row);
            Assert.NotEqual(expected: 0u, run.Current.EventId);

            if (!run.Current.EventDone)
            {
                var completed = row.WantedEventType switch {
                    (uint)WantedEventType.Shop => player.Wanted.OnTaskStepsPassed([row.TaskStepOfEventFinish]).Completed,
                    (uint)WantedEventType.Adventure => FinishAdventure(player, row.Id),
                    _ => player.Wanted.OnBattleEnded(true).Completed
                };
                Assert.True(completed, $"Entry {run.EntryId}, step {run.Step}, event {row.Id} did not complete");
            }

            foreach (var award in player.Wanted.CaptureRun()!.Current.Awards.Where(a => !a.Chosen).ToArray())
            {
                var replacement = player.Wanted.CaptureRun()!.Bionics.FirstOrDefault()?.UniqId ?? 0;
                Assert.Equal(expected: 0, player.Wanted.ChooseAward(award.AwardId, award.Options.First(), replacement).Result);
            }
        }
        Assert.Fail("Wanted route did not finish within 100 transitions");
    }

    private bool FinishAdventure(Player player, uint eventId)
    {
        var adventureId = Assets.Wanted.Npc(eventId)!.Params;
        var contentId = Assets.Wanted.Adventure(adventureId)!.ContentHeadId;

        for (var guard = 0; guard < 50; guard++)
        {
            var content = Assets.Wanted.AdventureContent(contentId)!;
            var dialogId = content.DialogId.First();

            if (content.Type == 1)
            {
                while (Assets.Wanted.AdventureDialog(dialogId)!.NextId is > 0 and var nextDialog)
                    dialogId = nextDialog;
            }
            Assert.True(Assets.Wanted.TryAdvanceAdventure(adventureId, contentId, dialogId, out var next));
            var result = player.Wanted.OnAdventureResolved(adventureId, contentId, dialogId, optionResult: 0);
            Assert.Equal(0, result.Result);
            Assert.Equal(next == 0, result.Completed);
            if (next == 0) return result.Completed;

            Assert.Empty(result.StepDrop);
            contentId = next;
        }
        return false;
    }

    [Fact]
    public void Wanted_EventCompletionIncludesTheMostRecentFinishedStep()
    {
        var player = Fresh();
        player.EnterWanted(10101);
        var eventId = player.Wanted.CurrentEventId;
        Assert.False(player.Wanted.IsEventComplete(eventId));
        Assert.True(player.Wanted.OnBattleEnded(true).Completed);

        foreach (var award in player.Wanted.CaptureRun()!.Current.Awards.ToArray())
        {
            Assert.Equal(expected: 0, player.Wanted.ChooseAward(award.AwardId, award.Options.First(), replacedBionicsUniqId: 0).Result);
        }
        Assert.True(player.Wanted.CurrentStep > 1);
        Assert.True(player.Wanted.IsEventComplete(eventId));
    }

    [Fact]
    public void ServerLevelHouseAndInventoryTargets_RequireTheirWorldFacts()
    {
        var player = Fresh();
        AtAction(player, actionId: 999870101);
        Assert.Empty(player.SettleServerTargets());
        player.Progress.Load(teamLevel: 15, teamExp: 0, satiety: 0, stamina: 0, DateTimeOffset.UtcNow);
        Assert.NotEmpty(player.SettleServerTargets());
        AtAction(player, actionId: 310350105);
        Assert.Empty(player.SettleServerTargets());
        player.Houses.MarkBought(houseId: 1, DateTimeOffset.UtcNow);
        Assert.NotEmpty(player.SettleServerTargets());
        AtAction(player, actionId: 410010108);
        Assert.Empty(player.SettleServerTargets());
        player.Bag.Add(itemId: 29900041, count: 1);
        Assert.NotEmpty(player.SettleServerTargets());
    }

    [Fact]
    public void ServerTaskTargets_StartNamedTask_AndWaitForCompletion()
    {
        var player = Fresh();
        AtAction(player, actionId: 1100322101);
        Assert.Equal(expected: 0, player.ReportTaskAction(taskType: 1, actionId: 1100322101, progress: 1).Code);
        Assert.True(player.Tasks.IsProcessing(type: 1, taskId: 99970));
        var action = AtAction(player, actionId: 1100900701);
        Assert.Empty(player.SettleServerTargets());
        var state = player.Tasks.Processing.Values.Single();

        player.Tasks.Load([(state.Type, state.TaskId, state.CurrentStep.StepId, Array.Empty<(ulong, uint, uint)>().AsEnumerable())],
            [(1u, 11004u)]);
        Assert.NotEmpty(player.SettleServerTargets());
    }

    [Fact]
    public void ServerArrivalAndResetTargets_RequireALoadedMatchingMap()
    {
        var player = Fresh();
        var action = AtAction(player, actionId: 110012102);
        Assert.Empty(player.SettleServerTargets());
        player.Map.BeginEnter(mapId: 100001001001, teleportId: 0);
        Assert.Empty(player.SettleServerTargets());
        Assert.Equal(expected: 0, player.Map.FinishEnter());
        Assert.NotEmpty(player.SettleServerTargets());
        AtAction(player, actionId: 1100900301);
        player.Battles.StartPatrolCooldown(12020016, DateTimeOffset.MaxValue);
        player.ReportTaskAction(taskType: 1, actionId: 1100900301, progress: 1);
        Assert.DoesNotContain(expected: 12020016L, player.Battles.PatrolCooldown);
        Assert.True(TaskManager.MatchesMap("100.0", player.Map.MapId));
        Assert.False(TaskManager.MatchesMap("206", player.Map.MapId));
    }

    [Fact]
    public void ServerDungeonTarget_RejectsClientCompletionAndAcceptsAClear()
    {
        var player = Fresh();
        AtAction(player, actionId: 310380401);
        Assert.Equal(expected: 0u, player.ReportTaskAction(taskType: 1, actionId: 310380401, progress: 1).Outcome!.Progress.Progress);
        Assert.Equal(expected: 0, player.EnterDungeon(207001).Code);
        Assert.Equal(expected: 0, player.FinishDungeon(dungeonId: 207001, victory: true, leave: false, hordeKills: 0).Code);
        Assert.NotEmpty(player.SettleServerTargets());
    }

    [Fact]
    public void ServerTargets_SaveRestoresEventProgressAndCommandReplayProtection()
    {
        var player = Fresh();
        AtAction(player, actionId: 1100112201);
        player.ReportTaskAction(taskType: 1, actionId: 1100112201, progress: 1);
        var applied = player.Tasks.AppliedEffects.ToArray();
        AtAction(player, actionId: 1100112201);
        player.Tasks.LoadAppliedEffects(applied);
        player.ReportTaskAction(taskType: 1, actionId: 1100112201, progress: 1);
        Assert.Equal(expected: 0u, player.Bag.CountOf(11041006));
        Assert.Single(player.Characters.All, c => c.CharacterId == 1006);
    }

    [Fact]
    public void Wanted_ConfigKeepsTrailingZeroes()
    {
        Assert.Equal(expected: 8000u, Assets.Wanted.ReviveMaxHp);
        Assert.Equal(expected: 4u, Assets.Wanted.CreatureMaxCount);
    }

    [Theory]
    [InlineData(1100228101UL, 1139u, 1u)]
    [InlineData(310030111UL, 479u, 1u)]
    [InlineData(310141101UL, 1439u, 1u)]
    [InlineData(310290801UL, 1019u, 1u)]
    public void ServerTimeTargets_UseTimeOfDay(ulong actionId, uint start, uint elapsed)
    {
        var player = Fresh();
        AtAction(player, actionId);
        player.LoadGameTime(start);
        Assert.Equal(expected: 1u, player.Tasks.Maximum(type: 1, actionId));
        Assert.Empty(player.SettleServerTargets());
        player.AdvanceGameTime(elapsed);

        Assert.Contains(player.SettleServerTargets(),
            outcome => outcome.Progress.SettledActions.Any(a => a.ActionId == actionId && a.Progress == 1));
    }

    [Fact]
    public void ServerTimeTargets_DoNotCountElapsedMinutesAsClockTime()
    {
        var player = Fresh();
        AtAction(player, actionId: 310030111);
        player.LoadGameTime(540);
        player.AdvanceGameTime(480);
        Assert.Empty(player.SettleServerTargets());
        player.AdvanceGameTime(900);
        Assert.NotEmpty(player.SettleServerTargets());
    }

    [Fact]
    public void ServerTargets_AllShippedTypesAreKnown_AndAlwaysSaved()
    {
        foreach (var (name, type) in new[] { ("QuestMain", 1u), ("POIQuest", 2u), ("Wanted", 3u), ("DailyTask", 4u) })
        foreach (var row in fixture.Rows("P_TaskActions_" + name))
        {
            if (!row.TryGetProperty("serverTargetType", out var target) || target.GetInt32() == 0) continue;

            Assert.True(Enum.IsDefined((ServerTarget)target.GetInt32()));
            Assert.True(Assets.Tasks.IsServerSaved(type, row.GetProperty("id").GetUInt64()));
        }
    }

    [Fact]
    public void ServerGiveItems_WaitsForClientSignal_AndCannotReplay()
    {
        var player = Fresh();
        AtAction(player, actionId: 1100112201);
        Assert.Empty(player.SettleServerTargets());
        Assert.Equal(expected: 0u, player.Bag.CountOf(11041006));
        Assert.Equal(expected: 0, player.ReportTaskAction(taskType: 1, actionId: 1100112201, progress: 1).Code);
        Assert.NotNull(player.Characters.InstanceOf(1006));
        player.ReportTaskAction(taskType: 1, actionId: 1100112201, uint.MaxValue);
        Assert.Equal(expected: 0u, player.Bag.CountOf(11041006));
    }

    [Fact]
    public void ServerTakeItems_IsAtomic_AndRetriesWhenInventoryArrives()
    {
        var player = Fresh();
        AtAction(player, actionId: 310152001);
        player.Bag.Add(itemId: 21400001, count: 1);
        player.ReportTaskAction(taskType: 1, actionId: 310152001, progress: 1);
        Assert.Equal(expected: 1u, player.Bag.CountOf(21400001));

        foreach (var id in new uint[] { 21400002, 21400003, 21400004 })
        {
            player.Bag.Add(id, count: 1);
        }
        Assert.NotEmpty(player.SettleServerTargets());

        foreach (var id in new uint[] { 21400001, 21400002, 21400003, 21400004 })
        {
            Assert.Equal(expected: 0u, player.Bag.CountOf(id));
        }
    }

    [Fact]
    public void ServerCaseTargets_OnlyGrantNamedClues_AndRequireOwnership()
    {
        var player = Fresh();
        AtAction(player, actionId: 110037004);
        player.ReportTaskAction(taskType: 1, actionId: 110037004, progress: 1);
        Assert.Empty(player.Cases.ToCaseData().ProcessingCase.Single(c => c.CaseId == 1002).ClueStatus);
        Assert.NotEqual(expected: 0, player.Cases.PutClue(1002101).Result);
        AtAction(player, actionId: 110037005);
        Assert.Equal(expected: 0, player.ReportTaskAction(taskType: 1, actionId: 110037005, progress: 1).Code);

        Assert.Equal(expected: 1002101UL,
            Assert.Single(player.Cases.ToCaseData().ProcessingCase.Single(c => c.CaseId == 1002).ClueStatus).ClueId);
        Assert.Equal(expected: 0, player.Cases.PutClue(1002101).Result);
        AtAction(player, actionId: 1100320001);
        Assert.Empty(player.SettleServerTargets());

        foreach (var clue in new ulong[] { 1002102, 1002103 })
        {
            Assert.True(player.Cases.GiveClue(clue));
            Assert.Equal(expected: 0, player.Cases.PutClue(clue).Result);
        }
        Assert.NotEmpty(player.SettleServerTargets());
    }

    [Fact]
    public void ServerEventTargets_IgnoreClientProgress_AndCountOnlyMatchingEvents()
    {
        var player = Fresh();
        AtAction(player, actionId: 310010117);
        var report = player.ReportTaskAction(taskType: 1, actionId: 310010117, uint.MaxValue);
        Assert.Equal(expected: 0u, report.Outcome!.Progress.Progress);
        player.RecordTaskEvent(ServerTarget.BuyItem, id: 21206002);
        Assert.Empty(player.SettleServerTargets());
        player.RecordTaskEvent(ServerTarget.BuyItem, id: 21206013);
        Assert.NotEmpty(player.SettleServerTargets());
        AtAction(player, actionId: 310360601);
        player.RecordTaskEvent(ServerTarget.CompleteBattle, id: 123);
        Assert.NotEmpty(player.SettleServerTargets());
    }

    [Fact]
    public void PurchaseStep_CountsTheShopPurchase_NotEatingTheItem()
    {
        // Step 3100117 "Purchase Joyous Soda Bread": the bread is handed to Alf at the next step.
        var player = Fresh();
        AtAction(player, actionId: 310010117);
        var now = DateTimeOffset.UtcNow;
        player.Wallet.Credit(1, 10_000);
        player.Bag.Add(21206013, 1);
        player.UseItem(21206013, 1, []);
        Assert.Empty(player.SettleServerTargets());

        Assert.Equal(0, player.BuyFromShop(103, [(10300001, 1)], now).Code);
        Assert.NotEmpty(player.SettleServerTargets());
        Assert.True(player.Bag.CountOf(21206013) >= 1);
    }

    [Fact]
    public void BattlePass_CanLeaveFirstLevel_AndRetainsRemainder()
    {
        var player = Fresh();
        var result = player.BattlePasses.AddExp(passId: 1001, exp: 150);
        Assert.Equal(expected: 2u, result.Data.Level);
        Assert.Equal(expected: 50u, result.Data.Exp);
        var saved = player.BattlePasses.Passes[1001];
        player.BattlePasses.Load([(1001, saved.Level, saved.Exp, saved.AwardLevel)]);
        Assert.Equal(expected: 50u, player.BattlePasses.Passes[1001].Exp);
        Assert.Equal(Assets.BattlePasses.MaxLevel(1001), player.BattlePasses.AddExp(passId: 1001, uint.MaxValue).Data.Level);
    }

    [Fact]
    public void GameplayState_RoundTripsWithoutDatabaseAccess()
    {
        var player = Fresh();
        player.ClearSaveDirty();
        player.AdvanceGameTime(1140);
        Assert.True(player.SaveDirty);
        player.Cases.OpenCase(1002);
        player.Cases.GiveClue(1002101);
        var placed = Assets.Collections.WorldObjects(blockId: 0)[0];
        player.Collections.ApplyCollected(placed.Id, DateTimeOffset.UtcNow);
        var node = player.Collections.Entries.Single();
        var activity = Assets.SignIn.Activity(3)!;
        player.SignIn.Query(activityId: 3, DateTimeOffset.FromUnixTimeSeconds((long)activity.TimeOffsetStart).AddHours(1));
        var now = DateTimeOffset.UtcNow;
        player.Houses.MarkBought(houseId: 1, now.AddHours(-2));
        player.Houses.MarkOpened(houseId: 1, now.AddHours(-2));
        player.Houses.MarkUpgraded(houseId: 1, now);

        var json = JsonSerializer.Serialize(RoleSaveMapper.Capture(player), SaveJson.Options);
        var restored = Fresh();
        RoleSaveMapper.Apply(restored, JsonSerializer.Deserialize<RoleSaveDocument>(json, SaveJson.Options)!);
        Assert.Equal(expected: 240u, restored.GameTimeMinutes);
        Assert.Contains(expected: 1002101UL, restored.Cases.Processing[1002].OwnedClues);
        Assert.Contains(node.Value.Cfg, restored.Collections.Gathered);
        Assert.Equal(player.SignIn.LastSignInDay, restored.SignIn.LastSignInDay);
        Assert.Equal(expected: 1060u, restored.Houses.AccruedIncome(houseId: 1, now));
    }

    [Fact]
    public void AchievementRewards_ResolveTheRandomDropTable_AndRejectDuplicateClaims()
    {
        var player = Fresh();
        var achievement = Assets.Achievements.All.First();
        player.Achievements.AddProgress(achievement.FinishId, count: 1);
        player.Achievements.AddProgress(achievement.FinishId, uint.MaxValue);
        Assert.True(player.Achievements.IsEventFinished(achievement.FinishId));
        Assert.Equal(expected: 0, player.Achievements.CheckClaim([achievement.Id]));
        Assert.NotEqual(expected: 0, player.Achievements.CheckClaim([achievement.Id, achievement.Id]));
        Assert.NotEmpty(player.Achievements.RewardOf(achievement.Id));
        player.Achievements.MarkClaimed([achievement.Id]);
        Assert.NotEqual(expected: 0, player.Achievements.CheckClaim([achievement.Id]));
    }

    [Fact]
    public void MonthCard_RenewalDoesNotPayTheUnsubscribedGap()
    {
        var player = Fresh();
        var now = DateTimeOffset.FromUnixTimeSeconds(1700000000);
        player.MonthCards.Load([(2001u, now.AddDays(-10).ToUnixTimeSeconds(), now.AddDays(-20).ToUnixTimeSeconds())]);
        Assert.Single(player.MonthCards.LapsedCards(now));
        player.MonthCards.Buy(cardId: 2001, now);
        Assert.Empty(player.MonthCards.DueDailyGrants(now));
        Assert.Equal(expected: 90u, Assert.Single(Assert.Single(player.MonthCards.DueDailyGrants(now.AddDays(1))).Grant).Count);
        Assert.Single(player.MonthCards.LapsedCards(now.AddDays(31)));
    }

    [Fact]
    public void Wanted_ResumesPreviouslyBrokenSteps_AndRejectsRevivingLivingCharacters()
    {
        var player = Fresh();
        Assert.NotEqual(expected: 0, player.WantedStaminaExchange().Result);
        Assert.Equal(expected: 0, player.EnterWanted(10101));
        var run = player.Wanted.CaptureRun()!;
        player.Wanted.Load([], run with { Current = run.Current with { EventId = 0 }, MaxStep = uint.MaxValue });
        Assert.NotEqual(expected: 0u, player.Wanted.CurrentEventId);
        Assert.Equal(Assets.Wanted.MaxStep(run.RouteId), player.Wanted.CaptureRun()!.MaxStep);
        player.Wallet.Credit((int)MoneyType.ThoughtSand, amount: 1000);
        Assert.NotEqual(expected: 0, player.BuyWantedRevive(player.Teams.CurrentMemberInstIds()));
        Assert.Equal(expected: 1000, player.Wallet.Balance((int)MoneyType.ThoughtSand));
    }

    [Fact]
    public void Wanted_EventTasksStartAtEachStepAndShopCompletionAdvancesTheRun()
    {
        var player = Fresh();
        player.EnterWanted(10101);
        var first = Assets.Wanted.Event(player.Wanted.CurrentEventId)!;
        player.SettleServerTargets().ToArray();
        Assert.All(first.EventStartAddTask, id => Assert.True(player.Tasks.IsProcessing(TaskAssets.Wanted, id)));
        Assert.False(player.Wanted.OnAdventureResolved(adventureId: 0, contentId: 0, dialogId: 0, optionResult: 0).Completed);
        var run = player.Wanted.CaptureRun()!;
        player.Wanted.Load([], run with { Step = 6, Current = run.Current with { ProcessId = 7, EventId = 8 } });
        AtAction(player, actionId: 80201, TaskAssets.Wanted);
        var result = player.ReportTaskAction(TaskAssets.Wanted, actionId: 80201, progress: 1);
        Assert.Equal(expected: 0, result.Code);
        Assert.True(player.Wanted.IsEventComplete(8));
        Assert.Contains(result.Outcome!.ServerNotifications, notification => notification is SCWantedStepNtf);
    }

    [Fact]
    public void Starter_OpensTheStoryInThePrologueVenue()
    {
        Assert.Equal(901001001001ul, Assets.Starter.MapId);
        Assert.Equal(211001001001ul, Assets.Starter.FallbackMap);
        Assert.False(Assets.Maps.IsPlayable(Assets.Starter.MapId));
        Assert.True(Assets.Maps.IsPlayable(Assets.Starter.FallbackMap));
    }
}
