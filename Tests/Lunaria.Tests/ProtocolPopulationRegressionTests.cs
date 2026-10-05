using System.Threading.Channels;
using Lunaria.Game.Mail;
using Lunaria.Game.Player.Persistence;
using Lunaria.Game.Player.Persistence.Entities;
using Lunaria.Game.Resources;
using Lunaria.GameServer.Handlers.Recv;
using Lunaria.Proto;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Msg;
using Xunit;

namespace Lunaria.Tests;

public sealed partial class RoleSessionTests
{
    [Theory]
    [InlineData(EnmTmpTeamType.Dungeon, 1u)] // dungeon type of 201001
    [InlineData(EnmTmpTeamType.Wanted, 10101u)]
    [InlineData(EnmTmpTeamType.Task, 5u)]
    public async Task TemporaryGems_SurviveSelectionActivationAndRoleReload(EnmTmpTeamType type, uint source)
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var player = ctx.Player;
        player.Tasks.Load([], []);
        player.ReconcileTemporaryTeam();
        player.Bag.Add(21501001, 1);
        player.Bag.Add(21501002, 1);
        var permanent = player.Teams.CurTeamData(player.Characters);
        var proposed = player.QueryTemporaryTeam((int)type, source)!;
        proposed.MemberData[0].GemSlots.Add(new GemSlotData { GemSlotId = 1, GemItemid = 21501001, GemState = EnmGemStatus.Invalid });
        proposed.MemberData[0].GemSlots.Add(new GemSlotData { GemSlotId = 3, GemItemid = 21501002, GemState = EnmGemStatus.Invalid });
        var result = player.UpdateTemporaryTeam((int)type, source, proposed);
        Assert.Equal(0, result.Result);
        var expected = result.Team!.MemberData[0].GemSlots.Clone();
        Assert.Equal(new[] { (1u, 21501001u), (3u, 21501002u) }, expected.Select(g => (g.GemSlotId, g.GemItemid)));
        Assert.All(expected, g => Assert.Equal(EnmGemStatus.Valid, g.GemState));
        Assert.Equal(permanent, player.Teams.CurTeamData(player.Characters));

        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 2));
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        player = ctx.Player;
        Assert.Equal(expected, player.QueryTemporaryTeam((int)type, source)!.MemberData[0].GemSlots);
        switch (type)
        {
            case EnmTmpTeamType.Wanted:
                // The entry packet carries only instance IDs, so keep the saved gems.
                Assert.Equal(0, player.EnterWanted(source, proposed.MemberData.Select(m => (uint)m.InstId).ToArray()));
                break;
            case EnmTmpTeamType.Dungeon:
                player.Progress.Load(1, 0, 0, 240, player.UtcNow);
                Assert.Equal(0, player.EnterDungeon(201001).Code); // a stage of dungeon type 1
                break;
            default:
                EnterStoryTeam(player, source);
                break;
        }
        Assert.Equal(expected, player.CurrentTeamData()!.TeamData.MemberData[0].GemSlots);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 2));
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        Assert.Equal(expected, ctx.Player.CurrentTeamData()!.TeamData.MemberData[0].GemSlots);
    }

    [Theory]
    [InlineData(EnmTmpTeamType.Dungeon, 1u)] // dungeon type of 201001
    [InlineData(EnmTmpTeamType.Wanted, 10101u)]
    [InlineData(EnmTmpTeamType.Task, 5u)]
    public async Task TemporaryGems_RejectInvalidSelectionsWithoutMutation(EnmTmpTeamType type, uint source)
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var player = ctx.Player;
        player.Tasks.Load([], []);
        player.ReconcileTemporaryTeam();
        foreach (var id in new uint[] { 21501001, 21501002, 21501003 }) player.Bag.Add(id, 1);
        var original = player.QueryTemporaryTeam((int)type, source)!;
        foreach (var fault in new[] { "unknown", "unowned", "slot", "duplicate", "budget" })
        {
            var proposed = original.Clone();
            var slots = proposed.MemberData[0].GemSlots;
            slots.Add(new GemSlotData { GemSlotId = fault == "slot" ? 4u : 1u,
                GemItemid = fault == "unknown" ? uint.MaxValue : fault == "unowned" ? 21501004u : 21501001u });
            if (fault == "duplicate") slots.Add(slots[0].Clone());
            if (fault == "budget")
            {
                slots.Add(new GemSlotData { GemSlotId = 2, GemItemid = 21501002 });
                slots.Add(new GemSlotData { GemSlotId = 3, GemItemid = 21501003 });
            }
            Assert.NotEqual(0, player.UpdateTemporaryTeam((int)type, source, proposed).Result);
            Assert.Equal(original, player.QueryTemporaryTeam((int)type, source));
        }
    }

    [Fact]
    public async Task AdventureReplies_DistinguishRejectionProgressAndCompletion()
    {
        var ctx = Context();
        var handler = new HandleWantedAdventureChangeReq();
        var request = new CSWantedAdventureChangeReq { AdventureId = 100, ContentId = 10001, DialogId = 0 };
        Assert.NotEqual(0, (await handler.OnPacket(ctx, request)).Result);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        Assert.NotEqual(0, (await handler.OnPacket(ctx, request)).Result);
        Assert.Equal(0, ctx.Player.EnterWanted(10101));
        Assert.NotEqual(0, (await handler.OnPacket(ctx, request)).Result);
        var run = ctx.Player.Wanted.CaptureRun()!;
        // The bundled adventure examples use sample route 1015, outside the currently published entries.
        ctx.Player.Wanted.Load([], run with { RouteId = 1015, Step = 1,
            Current = run.Current with { ProcessId = 201, EventId = 3 } });
        Assert.Equal(3u, ctx.Player.Wanted.CurrentEventId);
        var contentId = _assets.Wanted.Adventure(100)!.ContentHeadId;
        var progressed = false;
        var completed = false;
        for (var guard = 0; guard < 50; guard++)
        {
            var content = _assets.Wanted.AdventureContent(contentId)!;
            var dialogId = content.DialogId.First();
            if (content.Type == 1)
                while (_assets.Wanted.AdventureDialog(dialogId)!.NextId is > 0 and var nextDialog) dialogId = nextDialog;
            Assert.True(_assets.Wanted.TryAdvanceAdventure(100, contentId, dialogId, out var next));
            var before = ctx.Player.Wanted.Adventures.ToArray();
            Assert.NotEqual(0, (await handler.OnPacket(ctx, new() { AdventureId = 100, ContentId = uint.MaxValue, DialogId = dialogId })).Result);
            Assert.Equal(before, ctx.Player.Wanted.Adventures);
            var valid = new CSWantedAdventureChangeReq { AdventureId = 100, ContentId = contentId, DialogId = dialogId };
            var reply = await handler.OnPacket(ctx, valid);
            Assert.Equal(0, reply.Result);
            Assert.Equal(contentId, reply.Adventure.ContentId);
            Assert.NotEqual(0, (await handler.OnPacket(ctx, valid)).Result);
            if (next == 0) { completed = true; break; }
            progressed = true;
            contentId = next;
        }
        Assert.True(progressed);
        Assert.True(completed);
    }

    [Fact]
    public async Task DungeonRewards_IncludeInventoryMarkersAndRewardQuantities()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        ctx.Player.Progress.Load(1, 0, 0, 240, ctx.Player.UtcNow);
        Assert.Equal(0, ctx.Player.EnterDungeon(201001).Code);
        var reply = await new HandleDungeonsFinish().OnPacket(ctx, new() { DungeonsId = 201001, Victory = true });
        Assert.Equal(0, reply.Result);
        Assert.NotEmpty(reply.RewardList);
        var stored = reply.RewardList.Where(g => ctx.Player.Bag.CountOf(g.ItemId) > 0).ToArray();
        Assert.NotEmpty(stored);
        Assert.All(stored, item => {
            Assert.True(item.IsNew);
            Assert.Equal((ulong)item.ItemId, item.BindId);
            Assert.True(item.ItemNum > 0);
        });
        ctx.Player.Bag.ClearNewFlags(stored.Select(g => g.ItemId));
        Assert.All(ctx.Player.RewardItems(stored.Select(g => new ItemGrant(g.ItemId, 1))), item => {
            Assert.False(item.IsNew);
            Assert.Equal(1u, item.ItemNum);
        });
    }

    [Fact]
    public async Task HordeFinish_SendsTheBestScoreAndClaimedStars()
    {
        var outbound = Channel.CreateUnbounded<byte[]>();
        var ctx = Context(outbound);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        ctx.Player.Progress.Load(1, 0, 0, 240, ctx.Player.UtcNow);
        Assert.Equal(0, ctx.Player.EnterDungeon(11120201).Code);
        ReadReplyPackets(outbound);

        var reply = await new HandleDungeonsFinish().OnPacket(ctx,
            new() { DungeonsId = 11120201, Victory = true, HordeData = new HordeFinishDataReq { KillCount = 40 } });

        Assert.Equal(0, reply.Result);
        var command = MessageCmdRegistry.CmdIdOf(new SCHordeDataNtf());
        var ntf = SCHordeDataNtf.Parser.ParseFrom(Assert.Single(ReadReplyPackets(outbound), p => p.Head.Cmd == command).Body);
        Assert.Equal(11120201u, ntf.Id);
        Assert.Equal(40u, ntf.KillCount);
        Assert.Equal(2u, ntf.StarAward);
    }

    [Fact]
    public async Task HordeSettlement_ShowsTheKillsOfThisRun()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        ctx.Player.Progress.Load(1, 0, 0, 240, ctx.Player.UtcNow);
        foreach (var (kills, shown) in new[] { (40u, 40u), (12u, 12u) })
        {
            Assert.Equal(0, ctx.Player.EnterDungeon(11120201).Code);
            var reply = await new HandleDungeonsFinish().OnPacket(ctx,
                new() { DungeonsId = 11120201, Victory = true, HordeData = new HordeFinishDataReq { KillCount = kills } });
            Assert.Equal(0, reply.Result);
            Assert.Equal(shown, reply.HordeData.KillCount);
        }
        Assert.Equal(40u, ctx.Player.Dungeons.Hordes[11120201].KillCount);
    }

    [Fact]
    public async Task MailText_AndTemplateParametersSurviveDatabaseReloadAndWireEncoding()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var player = ctx.Player;
        var now = player.UtcNow.ToUnixTimeSeconds();
        var texts = new[] { new MailText("en_US", "Rewards", "Lunaria", "Your rewards are attached."),
            new MailText("ja_JP", "報酬", "Lunaria", "アイテムを受け取ってください。") };
        var system = player.Mails.SendSystem(player.Guid, [new ItemGrant(21206001, 2)], true, now, texts).Added!;
        var template = player.Mails.SendFromTemplate(player.Guid, 1, now, ["Detective", "42"]).Added!;
        Assert.NotNull(system);
        Assert.NotNull(template);
        Assert.Null(player.Mails.SendSystem(player.Guid, [], false, now, []).Added);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 2));
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var wire = ctx.Player.Mails.ToMailData(ctx.Player.Mails.Get(system.MailId)!);
        Assert.Equal(texts.Select(t => t.Content), wire.Contents.Select(t => t.Content.ToStringUtf8()));
        Assert.Equal(texts.Select(t => t.Language), wire.Contents.Select(t => t.Language.ToStringUtf8()));
        Assert.Equal(texts.Select(t => t.Title), wire.Contents.Select(t => t.Title.ToStringUtf8()));
        Assert.Equal(new[] { "Detective", "42" }, ctx.Player.Mails.ToMailData(ctx.Player.Mails.Get(template.MailId)!)
            .TemplateContentParams.Select(p => p.Param.ToStringUtf8()));
    }

    [Fact]
    public async Task MailTextMigration_PreservesExistingAttachments()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite(connection).Options);
        await db.GetService<IMigrator>().MigrateAsync("20260907000000_AddRoleMotives");
        db.Accounts.Add(new Account { Id = 1, AccountKey = "mail-upgrade", Userid = "mail-upgrade" });
        db.Roles.Add(new Role { Id = 1, AccountId = 1, Slot = 0 });
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO role_mails (role_id, mail_id, template_id, type, important, open, has_attach, items, time, expire_time)
            VALUES (1, 7, 1, 2, 0, 0, 1, {0}, 1, 0)
            """, new object[] { "[{\"ItemId\":21206001,\"Count\":2}]" });
        await db.Database.MigrateAsync();
        var mail = await db.RoleMails.SingleAsync();
        Assert.Equal(7, mail.MailId);
        Assert.Equal("[{\"ItemId\":21206001,\"Count\":2}]", mail.Items);
        Assert.Equal("[]", mail.Contents);
        Assert.Equal("[]", mail.TemplateContentParams);
    }

    [Fact]
    public async Task WantedSettlement_ReportsExistingPassScoreWhenNoExperienceIsAdded()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        ctx.Player.BattlePasses.Load([(1001u, 1u, 25u, 0u)]);
        Assert.Equal(0, ctx.Player.EnterWanted(10101));
        var result = ctx.Player.WantedOver();
        Assert.Equal(0, result.Result);
        Assert.False(result.Settlement!.Victory);
        Assert.Equal(0u, result.Settlement.BattlePassAddScore);
        Assert.Equal(25u, result.Settlement.BattlePassTotalScore);
    }
}
