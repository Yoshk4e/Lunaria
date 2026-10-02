using System.Text.Json;
using Lunaria.Game.Motives;
using Lunaria.Game.Player.Persistence;
using Lunaria.Game.Player.Persistence.Entities;
using Lunaria.Game.Player.Persistence.Saves;
using Lunaria.Game.Resources;
using Lunaria.Game.Wanted;
using Lunaria.GameServer.Handlers.Recv;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Msg;
using Xunit;

namespace Lunaria.Tests;

public sealed partial class RoleSessionTests
{
    [Fact]
    public async Task BattleHandlers_ValidateWantedField_AndAcceptDuplicateRequestsWithoutResettingBattle()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        Assert.Equal(0, ctx.Player.EnterWanted(10101));
        var run = ctx.Player.Wanted.CaptureRun()!;
        var process = _assets.Wanted.StepsOf(run.RouteId, 1).First();
        var eventId = _assets.Policy.Wanted.EventPools[process.Pool]
            .First(id => _assets.Wanted.Npc(id)?.NpcType == (uint)WantedNpcType.NormalBattle);
        ctx.Player.Wanted.Load([], run with {
            Current = new WantedStepSnapshot(EnmWantedStepStatus.EnmWssStart, process.Id, eventId, false, [])
        });
        var field = _assets.Wanted.Npc(eventId)!.Params;
        var enter = new HandleEnterBattle();
        var request = new CSEnterBattle { BattleType = EBattleType.EnmBattleTypeWanted, BattleFieldId = uint.MaxValue, BattleInstId = 12 };
        Assert.NotEqual(0, (await enter.OnPacket(ctx, request)).Ret);
        Assert.Null(ctx.Player.CurrentBattle);
        request.BattleFieldId = field;
        Assert.Equal(0, (await enter.OnPacket(ctx, request)).Ret);
        var start = new CSStartBattle { BattleType = request.BattleType, BattleFieldId = field };
        Assert.Equal(0, (await new HandleStartBattle().OnPacket(ctx, start)).Ret);
        Assert.Equal(0, (await enter.OnPacket(ctx, request)).Ret);
        Assert.True(ctx.Player.CurrentBattle!.Started);
        Assert.Equal(0, (await new HandleStartBattle().OnPacket(ctx, start)).Ret);
        var pause = new CSPauseBattle { BattleType = request.BattleType, BattleFieldId = uint.MaxValue, Pause = true };
        Assert.NotEqual(0, (await new HandlePauseBattle().OnPacket(ctx, pause)).Result);
        Assert.False(ctx.Player.CurrentBattle.Paused);
        Assert.False(ctx.Player.Wanted.IsEventComplete(eventId));
    }

    [Fact]
    public async Task BattleHandler_RequiresTheCurrentDungeonField()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var handler = new HandleEnterBattle();
        var req = new CSEnterBattle { BattleType = EBattleType.EnmBattleTypeRepeatDungeon, BattleFieldId = 203002001, BattleInstId = 12 };
        Assert.NotEqual(0, (await handler.OnPacket(ctx, req)).Ret);
        ctx.Player.Dungeons.Load([], [], [], (201001, 203002001), ctx.Player.UtcNow);
        req.BattleFieldId = uint.MaxValue;
        Assert.NotEqual(0, (await handler.OnPacket(ctx, req)).Ret);
        Assert.Null(ctx.Player.CurrentBattle);
        req.BattleFieldId = 203002001;
        Assert.Equal(0, (await handler.OnPacket(ctx, req)).Ret);
    }

    [Fact]
    public async Task MotiveOverflow_SaveReloadDecomposeAndReplay_ConservesEquipmentAndExperience()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        const uint itemId = 12051001;
        ctx.Player.GrantRewards([new(12031001, MotiveManager.MaxMotives - 1)], EnmItemReason.EnmItemChangeNormal);
        ctx.Player.GrantRewards([new(itemId, 3)], EnmItemReason.EnmItemChangeNormal);
        var expectedExperience = ctx.Player.LevelData();
        Assert.Equal(2u, ctx.Player.InventoryCount(itemId));
        await _store.SaveAsync(ctx.Player);
        var loaded = await _store.LoadAsync(ctx.Player, 1, ctx.Player.UtcNow);
        Assert.Equal(3UL, loaded.OwnedItemCount(itemId));
        var removed = loaded.Motives.All.First(m => m.ItemId == 12031001).UniqId;
        Assert.Equal(0, loaded.DecomposeMotives([removed]).Code);
        Assert.Equal(MotiveManager.MaxMotives, loaded.Motives.Count);
        Assert.Equal(1u, loaded.InventoryCount(itemId));
        var acquisition = loaded.Achievements.Events.Values.Where(e => _assets.Unlocks.MotiveRareEvents(_assets.Motives.Rarity(itemId)).Contains(e.EventId)).ToArray();
        Assert.NotEmpty(acquisition);
        Assert.Equal(2UL, loaded.Achievements.Events[203001].Progress);
        await _store.SaveAsync(loaded);
        loaded = await _store.LoadAsync(loaded, 1, loaded.UtcNow);
        Assert.NotEqual(0, loaded.DecomposeMotives([removed]).Code);
        loaded.AdvanceTime(loaded.UtcNow.AddSeconds(1));
        Assert.Equal((ulong)MotiveManager.MaxMotives + 1, (ulong)loaded.Motives.Count + loaded.InventoryCount(itemId));
        Assert.Equal(3UL, loaded.OwnedItemCount(itemId));
        Assert.Equal(expectedExperience, loaded.LevelData());
        foreach (var state in acquisition) Assert.Equal(state, loaded.Achievements.Events[state.EventId]);
    }

    [Fact]
    public async Task LegacyFixture_LoadMutateSaveAndReload_WritesCurrentVersionWithoutRepeatingAttendance()
    {
        var ctx = Context();
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "role-save-v0.json"));
        using (var db = new GameDbContext(_options))
        {
            db.RoleSaves.Add(new RoleSave { RoleId = 1, State = json });
            await db.SaveChangesAsync();
        }
        var now = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var loaded = await _store.LoadAsync(ctx.Player, 1, now);
        Assert.Equal(123UL, loaded.OwnedItemCount(1));
        Assert.Equal(3u, loaded.SignIn.AttendanceDays);
        using (loaded.BeginOperation(now)) loaded.GrantRewards([new(1, 7)], EnmItemReason.EnmItemChangeNormal);
        await _store.SaveAsync(loaded);
        using (var db = new GameDbContext(_options))
        {
            using var document = JsonDocument.Parse((await db.RoleSaves.SingleAsync()).State);
            Assert.Equal(RoleSaveMigrations.CurrentVersion, document.RootElement.GetProperty("schema_version").GetInt32());
        }
        loaded = await _store.LoadAsync(loaded, 1, now);
        Assert.Equal(130UL, loaded.OwnedItemCount(1));
        Assert.Equal(3u, loaded.SignIn.AttendanceDays);
        Assert.Equal(2u, Assert.Single(Assert.Single(loaded.PendingRewardMail)).Count);
    }

    [Fact]
    public async Task FutureSave_RefusesRoleSwitchAndOverwrite_AndPreservesStoredBytes()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        const string future = "{\"schema_version\":999,\"wallet\":\"future format\"}";
        using (var db = new GameDbContext(_options))
        {
            db.RoleSaves.Add(new RoleSave { RoleId = 2, State = future });
            await db.SaveChangesAsync();
        }
        var current = ctx.Player;
        Assert.NotEqual(0, await _sessions.ActivateAsync(ctx, 2));
        Assert.Same(current, ctx.Player);
        var repository = new RoleSaveRepository(new TestFactory(_options), NullLogger<RoleSaveRepository>.Instance);
        await Assert.ThrowsAsync<InvalidDataException>(() => repository.SaveAsync(2, current));
        using var verify = new GameDbContext(_options);
        Assert.Equal(future, (await verify.RoleSaves.SingleAsync(r => r.RoleId == 2)).State);
    }
}
