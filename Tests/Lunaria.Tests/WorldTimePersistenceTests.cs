using System.Threading.Channels;
using Lunaria.Game.Player.Persistence.Saves;
using Lunaria.Game.Resources;
using Lunaria.Game.Tasks;
using Lunaria.GameServer.Handlers.Recv;
using Microsoft.Extensions.Logging.Abstractions;
using Msg;
using Xunit;

namespace Lunaria.Tests;

public sealed partial class RoleSessionTests
{
    [Fact]
    public async Task ScriptedTimeAndWeather_PersistWithTaskProgressAndDoNotReplayAfterReload()
    {
        var outbound = Channel.CreateUnbounded<byte[]>();
        var ctx = Context(outbound);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        LoadTaskAction(ctx.Player, 110030401);
        ctx.Player.LoadGameTime(600);
        ctx.Player.LoadWeather((uint)WeatherType.Foggy);
        await ctx.RunCommittedAsync(() => new HandleTaskActionUpdate(NullLogger<HandleTaskActionUpdate>.Instance)
            .OnPacket(ctx, new CSTaskActionUpdate { TaskType = TaskAssets.QuestMain, ActionId = 110030401, Progress = 1 }),
            player => _store.SaveAsync(player));
        Assert.True(outbound.Reader.TryPeek(out _));
        Assert.Equal(720u, ctx.Player.GameTimeMinutes);
        Assert.Equal(WeatherType.Sunny, ctx.Player.CurrentWeather);
        Assert.False(ctx.Player.SaveDirty);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 2));
        Assert.Equal(_assets.Starter.GameTime, ctx.Player.GameTimeMinutes);
        Assert.Equal((WeatherType)_assets.Starter.Weather, ctx.Player.CurrentWeather);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        Assert.Equal(720u, ctx.Player.GameTimeMinutes);
        Assert.Equal(WeatherType.Sunny, ctx.Player.CurrentWeather);
        var mapInfo = ctx.Player.RoleInfo(ctx.Player.Roles.Active()!).MapInfo;
        Assert.Equal(720u, mapInfo.CurrentGametime);
        Assert.Equal((uint)WeatherType.Sunny, mapInfo.CurrentWeather);
        Assert.Empty(ctx.Player.ReportTaskAction(TaskAssets.QuestMain, 110030401, 1).Outcome!.AllNotifications);
    }

    [Fact]
    public async Task ManualWeatherChange_PersistsEvenWhenTheClockDoesNotMove()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        await _store.SaveAsync(ctx.Player);
        var handler = new HandleGameTimeSetup();
        var reply = await handler.OnPacket(ctx, new CSGameTimeSetupReq { Weather = (uint)WeatherType.Rainy });
        Assert.Equal(0, reply.Result);
        Assert.Equal((uint)WeatherType.Rainy, reply.Weather);
        Assert.True(ctx.Player.SaveDirty);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        Assert.Equal(_assets.Starter.GameTime, ctx.Player.GameTimeMinutes);
        Assert.Equal(WeatherType.Rainy, ctx.Player.CurrentWeather);
    }

    [Fact]
    public async Task LegacyClockSavedAtMidnight_RestoresTheInitTablesStartTime()
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var legacy = RoleSaveMapper.Capture(ctx.Player) with { GameTimeMinutes = 0 };
        RoleSaveMapper.Apply(ctx.Player, legacy);
        Assert.Equal(_assets.Starter.GameTime, ctx.Player.GameTimeMinutes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(999u)]
    public async Task MissingOrInvalidSavedWeather_UsesStarterWeather(uint? saved)
    {
        var ctx = Context();
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        var legacy = RoleSaveMapper.Capture(ctx.Player) with { GameTimeMinutes = 900, CurrentWeather = saved };
        ctx.Player.LoadWeather((uint)WeatherType.Foggy);
        RoleSaveMapper.Apply(ctx.Player, legacy);
        Assert.Equal(900u, ctx.Player.GameTimeMinutes);
        Assert.Equal((WeatherType)_assets.Starter.Weather, ctx.Player.CurrentWeather);
    }

    [Fact]
    public async Task TimeRequest_RejectsWithoutAnActiveRole()
    {
        var ctx = Context();
        var before = ctx.Player.GameTimeMinutes;
        var reply = await new HandleGameTimeSetup().OnPacket(ctx, new CSGameTimeSetupReq {
            PassTime = 100, Weather = (uint)WeatherType.Foggy
        });
        Assert.NotEqual(0, reply.Result);
        Assert.Equal(before, ctx.Player.GameTimeMinutes);
        Assert.Equal((WeatherType)_assets.Starter.Weather, ctx.Player.CurrentWeather);
    }
}
