using System.Threading.Channels;
using Lunaria.Game.Characters;
using Lunaria.Game.Characters.Teams;
using Lunaria.Game.Player;
using Lunaria.Game.Player.Persistence.Saves;
using Lunaria.Game.Resources;
using Lunaria.Game.Resources.Tables;
using Lunaria.Game.Tasks;
using Lunaria.GameServer.Handlers.Recv;
using Microsoft.Extensions.Logging.Abstractions;
using Msg;
using Xunit;

namespace Lunaria.Tests;

public sealed partial class RoleSessionTests
{
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
    public void EveryBundledTrialTeam_ResolvesAllConfiguredMembersAndLivingAttributes(uint source)
    {
        var player = new Player(1, _assets);
        var table = _assets.TmpTeams.GetBySrc(source)!;
        Assert.Equal(table.TmpCharacters.Count, _assets.TmpTeams.MembersOf(table).Count);
        EnterStoryTeam(player, source);
        var current = player.CurrentTeamData()!;
        Assert.Equal(table.TmpCharacters.Count, current.TeamData.MemberData.Count);
        Assert.All(current.AttribData, character => {
            Assert.True(Assert.Single(character.AttribData, a => a.AttribType == _assets.Inside.Attr.Maxhp).FinalValue > 0);
            Assert.True(Assert.Single(character.AttribData, a => a.AttribType == _assets.Inside.Attr.Hp).FinalValue > 0);
        });
        if (source == 10)
        {
            Assert.Equal(1101u, Assert.Single(current.TeamData.MemberData).CharacterId);
            Assert.Equal(_assets.Characters.DevelopAttributeId(1001, 15), _assets.Characters.DevelopAttributeId(1101, 15));
        }
    }

    [Fact]
    public void AcceptedTaskStep_ActivatesTheNextTrialTeamThroughItsHook_WithoutReplay()
    {
        var player = new Player(1, _assets);
        player.Tasks.Load([(1u, 10001004u, 10001001002UL, Array.Empty<(ulong, uint, uint)>())], []);
        player.ReconcileTemporaryTeam();
        Assert.Equal(1u, player.ActiveTemporaryTeam!.Source);
        player.DrainGameplayChanges();
        Assert.Equal(0, player.ReportTaskAction(1, 1000100100201, 1).Code);
        Assert.Equal(2u, player.ActiveTemporaryTeam!.Source);
        Assert.Single(player.DrainGameplayChanges().OfType<SCCharacterTempTeamNtf>());
        player.ReportTaskAction(1, 1000100100201, 1);
        Assert.Empty(player.DrainGameplayChanges().OfType<SCCharacterTempTeamNtf>());
    }

    private void EnterStoryTeam(Player player, uint source = 5)
    {
        var row = _assets.TmpTeams.GetBySrc(source)!;
        var step = row.StepId.First(s => _assets.Tasks.TaskOfStep(row.TaskType, s) != 0);
        var map = _assets.Tasks.Actions(row.TaskType, step).Select(id => _assets.Tasks.Action(row.TaskType, id)!)
            .Where(action => action.MapId > 0).Select(action => action.MapId).FirstOrDefault();
        if (map != 0)
        {
            var playable = _fixture.Rows("P_MapDataTable").Select(r => r.GetProperty("id").GetUInt64())
                .First(id => _assets.Maps.MapExists(id) && TaskManager.MatchesMap(map, id));
            player.Map.Load(playable, _assets.Starter.Savepoint, [_assets.Starter.Savepoint], [], (0, 0, 0));
        }
        player.Tasks.Load([(row.TaskType, _assets.Tasks.TaskOfStep(row.TaskType, step), step, Array.Empty<(ulong, uint, uint)>())], []);
        Assert.True(player.ReconcileTemporaryTeam());
        Assert.Equal(source, player.ActiveTemporaryTeam!.Source);
    }

    [Theory]
    [InlineData(EnmTmpTeamType.Dungeon, 1u)] // dungeon type of 201001
    [InlineData(EnmTmpTeamType.Wanted, 10101u)]
    public async Task TemporarySelection_UsesOwnedInstances_AndSurvivesRoleSwitch(EnmTmpTeamType type, uint source)
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var player = ctx.Player;
        var query = await new HandleCharacterTmpTeamQuery().OnPacket(ctx, new() { TeamType = (int)type, TeamSrc = source });
        Assert.Equal(0, query.Result);
        Assert.Equal(player.Teams.CurrentMemberInstIds(), query.TeamData.MemberData.Select(m => m.InstId));
        var member = player.Characters.All.First();
        var selected = new TeamData { TeamId = source, MemberData = { new TeamMemberData {
            InstId = member.InstId, CharacterId = member.CharacterId, MemberSlotId = 4 } } };
        Assert.Equal(0, (await new HandleCharacterUpdateTmpTeam().OnPacket(ctx, new() {
            TeamType = (int)type, TeamSrc = source, TeamData = selected })).Result);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 2));
        Assert.Empty(ctx.Player.TemporarySelections);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        Assert.Equal(selected.MemberData, ctx.Player.QueryTemporaryTeam((int)type, source)!.MemberData);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("mismatch")]
    [InlineData("duplicate")]
    [InlineData("slot")]
    [InlineData("source")]
    [InlineData("empty")]
    public async Task TemporarySelection_RejectsInvalidCompositionWithoutMutation(string fault)
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var player = ctx.Player;
        var request = player.QueryTemporaryTeam((int)EnmTmpTeamType.Wanted, 10101)!.Clone();
        var member = request.MemberData[0];
        switch (fault)
        {
            case "foreign": member.InstId = ulong.MaxValue; break;
            case "mismatch": member.CharacterId = uint.MaxValue; break;
            case "duplicate": request.MemberData.Add(member.Clone()); break;
            case "slot": member.MemberSlotId = 0; break;
            case "source": request.TeamId++; break;
            case "empty": request.MemberData.Clear(); break;
        }
        Assert.NotEqual(0, player.UpdateTemporaryTeam((int)EnmTmpTeamType.Wanted, 10101, request).Result);
        Assert.Empty(player.TemporarySelections);
    }

    [Fact]
    public async Task WantedRunLeftOnItsMap_HasNoActiveTeamAfterLogin()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var player = ctx.Player;
        Assert.Equal(0, player.EnterWanted(10101, [(uint)player.Characters.All.Last().InstId]));
        Assert.Equal(EnmTmpTeamType.Wanted, player.ActiveTemporaryTeam?.Type);
        player.Map.Load(206001001001, player.Map.Savepoint, [], [], (1, 2, 3),
            returnPoint: new Lunaria.Game.World.MapReturnPoint(100001001001, 124389, 62649, 32272, IsSynced: true));

        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 2));
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));

        // The client disables Partners while a temporary team is active, and the entry request edits the wanted
        // selection, which an active team refuses.
        Assert.Equal(100001001001ul, ctx.Player.Map.MapId);
        Assert.True(ctx.Player.Wanted.IsRunning);
        Assert.Null(ctx.Player.ActiveTemporaryTeam);
    }

    [Fact]
    public async Task WantedRunLeftOnItsMap_DoesNotBlockOpenWorldBattles()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var player = ctx.Player;
        Assert.Equal(0, player.EnterWanted(10101, [(uint)player.Characters.All.Last().InstId]));
        player.Map.Load(206001001001, player.Map.Savepoint, [], [], (1, 2, 3),
            returnPoint: new Lunaria.Game.World.MapReturnPoint(100001001001, 124389, 62649, 32272, IsSynced: true));

        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 2));
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));

        const uint field = 109100101;
        Assert.True(ctx.Player.Wanted.IsRunning);
        Assert.Equal(0, ctx.Player.EnterBattle(EBattleType.EnmBattleTypePatrol, field, 0, EnmMonsterFromType.EmonsterFromInvalid));
        Assert.Equal(0, ctx.Player.StartBattle(EBattleType.EnmBattleTypePatrol, field));
        Assert.Null(ctx.Player.ActiveTemporaryTeam);
    }

    [Fact]
    public async Task WantedEntry_KeepsTheLineupPickedOnTheWantedScreen()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var player = ctx.Player;
        var main = player.CurrentTeamMembers().Select(id => (uint)id).ToArray();
        var picked = player.Characters.All.Last(c => !main.Contains((uint)c.InstId));
        var selection = new TeamData { TeamId = 10101 };
        selection.MemberData.Add(new TeamMemberData { MemberSlotId = 1, InstId = picked.InstId, CharacterId = picked.CharacterId });
        Assert.Equal(0, player.UpdateTemporaryTeam((int)EnmTmpTeamType.Wanted, 10101, selection).Result);

        // The entry request still carries the main team.
        Assert.Equal(0, player.EnterWanted(10101, main));

        Assert.Equal(picked.InstId, Assert.Single(player.CurrentTeamMembers()));
    }

    [Fact]
    public async Task WantedEntry_UsesRequestedInstances_RejectsForgeries_AndRestoresPermanentTeam()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var player = ctx.Player;
        var original = player.Teams.CurTeamData(player.Characters);
        var member = player.Characters.All.Last();
        Assert.NotEqual(0, player.EnterWanted(10101, [uint.MaxValue]));
        Assert.NotEqual(0, player.EnterWanted(10101, [0, 0]));
        Assert.NotEqual(0, player.EnterWanted(10101, [(uint)member.InstId, (uint)member.InstId]));
        Assert.False(player.Wanted.IsRunning);
        Assert.Equal(0, player.EnterWanted(10101, [0, (uint)member.InstId]));
        Assert.Equal(member.InstId, Assert.Single(player.CurrentTeamMembers()));
        Assert.Equal(2u, player.CurrentTeamData()!.UsingMemberSlot);
        Assert.NotEqual(0, player.SwitchTeam(player.Teams.Current).Result);
        Assert.NotEqual(0, player.EnterDungeon(201001).Code);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 2));
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        Assert.Equal(member.InstId, Assert.Single(ctx.Player.CurrentTeamMembers()));
        Assert.Equal((int)EnmTmpTeamType.Wanted, ctx.Player.CurrentTeamData()!.TeamType);
        Assert.Equal(0, ctx.Player.LeaveWanted());
        Assert.Equal(original, ctx.Player.Teams.CurTeamData(ctx.Player.Characters));
        Assert.Equal(EnmTmpTeamType.Task, ctx.Player.ActiveTemporaryTeam!.Type);
        ctx.Player.Tasks.Load([], []);
        ctx.Player.ReconcileTemporaryTeam();
        Assert.Null(ctx.Player.ActiveTemporaryTeam);
        Assert.Contains(ctx.Player.DrainGameplayChanges().OfType<SCCharacterTempTeamNtf>(), n => n.CurTeam.TeamType == (int)EnmTmpTeamType.None);
    }

    [Fact]
    public async Task TrialTeam_BattleVitalsAndReconnectStaySeparateFromOwnedRoster()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var player = ctx.Player;
        var permanent = player.Teams.CurTeamData(player.Characters);
        EnterStoryTeam(player);
        var active = player.CurrentTeamData()!;
        Assert.Equal(4, active.TeamData.MemberData.Count);
        Assert.All(active.TeamData.MemberData, m => Assert.False(player.Characters.Owns(m.InstId)));
        Assert.All(player.ActiveTemporaryTeam!.Trials, t => Assert.Equal(0, t.Liquid));
        var trial = active.TeamData.MemberData[0];
        Assert.Equal(0, player.SetCurrentTeamSlot(4));
        Assert.Equal(0, player.Battles.Enter(EBattleType.EnmBattleTypeExpose, 1, 2, default));
        Assert.Equal(0, player.Battles.Start(EBattleType.EnmBattleTypeExpose, 1));
        var report = new CSLeaveBattle { BattleType = EBattleType.EnmBattleTypeExpose, BattleFieldId = 1,
            BattleInstId = 2, BattleResult = EBattleResultType.EnmBattleResultTypeSuccess,
            TemporaryLiquid = new() { Fire = 1234 },
            CharacterData = {
                new CharacterAttribInfo { InstId = trial.InstId, CurrentHp = 1, PermanentLiquid = 12 },
                new CharacterAttribInfo { InstId = player.Characters.All.First().InstId, CurrentHp = 0, PermanentLiquid = 0 }
            } };
        player.DrainGameplayChanges();
        Assert.Equal(0, player.LeaveBattle(report).Result);
        Assert.Equal(1, player.TeamCharacterHp(trial.InstId));
        Assert.Equal(12, player.TeamCharacterLiquid(trial.InstId));
        Assert.Equal(permanent, player.Teams.CurTeamData(player.Characters));
        Assert.Contains(player.DrainGameplayChanges().OfType<SCOutsideAttribNtf>(), n => n.Data.InstId == trial.InstId);
        var expected = player.CurrentTeamData();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 2));
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var reply = await new HandleCharacterTeams(NullLogger<HandleCharacterTeams>.Instance).OnPacket(ctx, new());
        Assert.Equal(expected, reply.CurTeam);
        var attributes = await new HandleOutsideAttribQuery(NullLogger<HandleOutsideAttribQuery>.Instance).OnPacket(ctx, new());
        Assert.Equal(reply.CurTeam.AttribData, attributes.Data);
        Assert.True(ctx.Player.Guid.Next() > reply.CurTeam.TeamData.MemberData.Max(m => m.InstId));
        ctx.Player.Tasks.Load([], []);
        Assert.True(ctx.Player.ReconcileTemporaryTeam());
        Assert.Equal(permanent, ctx.Player.CurrentTeamData());
    }

    [Fact]
    public async Task DungeonTeam_SelectionIsFrozenUntilSettlement_AndUnselectedRosterIsUntouched()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var player = ctx.Player;
        var member = player.Characters.All.First();
        var data = new TeamData { TeamId = 1, MemberData = { new TeamMemberData {
            InstId = member.InstId, CharacterId = member.CharacterId, MemberSlotId = 3 } } };
        Assert.Equal(0, player.UpdateTemporaryTeam((int)EnmTmpTeamType.Dungeon, 1, data).Result);
        player.Progress.Load(1, 0, 0, 240, DateTimeOffset.UtcNow);
        Assert.Equal(0, player.EnterDungeon(201001).Code);
        Assert.Equal(3u, player.CurrentTeamData()!.UsingMemberSlot);
        Assert.NotEqual(0, player.UpdateTemporaryTeam((int)EnmTmpTeamType.Dungeon, 1, data).Result);
        Assert.NotEqual(0, player.EnterWanted(10101));
        Assert.Equal(0, player.FinishDungeon(201001, false, true, 0).Code);
        Assert.Equal(EnmTmpTeamType.Task, player.ActiveTemporaryTeam!.Type);
    }

    [Fact]
    public void TrialData_UsesRatiosAndTaskNamespace_AndSanitizesLegacyArrangements()
    {
        var row = _assets.TmpTeams.GetBySrc(5)!;
        var original = _assets.TmpTeams.MembersOf(row)[0];
        var manager = new TempTeamManager(_assets);
        var adjusted = original with { CharacterHpRatio = 2500, CharacterPermanentLiquidRatio = 5000 };
        var attribs = manager.AttribDataOf(adjusted, 42).AttribData.ToDictionary(a => a.AttribType, a => a.FinalValue);
        var max = _assets.Attribs.MaxHp(adjusted.CharacterId, _assets.Characters.DevelopAttributeId(adjusted.CharacterId, adjusted.Level));
        Assert.Equal(_assets.Inside.Scale(_assets.Inside.Attr.Hp, max / 4), attribs[_assets.Inside.Attr.Hp]);
        Assert.Equal(_assets.Inside.Scale(_assets.Inside.Attr.PermanentLiquid, _assets.Attribs.PermanentLiquidMax(adjusted.CharacterId) / 2), attribs[_assets.Inside.Attr.PermanentLiquid]);
        Assert.Equal(6000, TempTeamManager.InitialLiquid(row with { TemporaryLiquidRatio = 6000 }).Thunder);
        Assert.Null(_assets.TmpTeams.Get((int)EnmTmpTeamType.Dungeon, row.Id));
        Assert.Empty(_assets.TmpTeams.TeamsOfStep(TaskAssets.Wanted, row.StepId[0]));
        manager.Load([(row.Id, new[] { (0u, original.CharacterId), (1u, original.CharacterId), (2u, original.CharacterId), (1u, uint.MaxValue) })]);
        Assert.Single(manager.Teams[row.Id].Members);
        Assert.Equal(1u, manager.Teams[row.Id].Members[0].Slot);
    }

    [Fact]
    public async Task FinishMap_RequiresMatchingActiveRole_AndRetainsPendingEntryOnRejection()
    {
        var outbound = Channel.CreateUnbounded<byte[]>();
        var ctx = Context(outbound);
        Assert.NotEqual(0, (await DispatchReplyAsync(ctx, outbound, new CSFinEnterMap { RoleId = 1 }, SCFinEnterMap.Parser)).Result);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        Assert.Equal(0, ctx.Player.Map.BeginEnter(0, 0).Code);
        var phase = ctx.Player.Map.Phase;
        Assert.NotEqual(0, (await DispatchReplyAsync(ctx, outbound, new CSFinEnterMap { RoleId = 2 }, SCFinEnterMap.Parser)).Result);
        Assert.Equal(phase, ctx.Player.Map.Phase);
        Assert.Equal(0, (await DispatchReplyAsync(ctx, outbound, new CSFinEnterMap { RoleId = 1 }, SCFinEnterMap.Parser)).Result);
    }

    [Fact]
    public void RoleReplacement_DoesNotRestartTheRewardRandomStream()
    {
        var player = new Player(123, _assets);
        Assert.Same(player.RandomSources, player.CreateRoleSession().RandomSources);
    }
}
