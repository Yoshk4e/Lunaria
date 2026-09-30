using System.Reflection;
using System.Threading.Channels;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Lunaria.Game.Player;
using Lunaria.Game.Player.Auth;
using Lunaria.Game.Player.Persistence;
using Lunaria.GameServer;
using Lunaria.GameServer.Net;
using Lunaria.Silver;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Msg;
using Xunit;

namespace Lunaria.Tests;

public sealed partial class RoleSessionTests
{
    private ServiceProvider RouterServices() => new ServiceCollection()
        .AddLogging()
        .AddSingleton(_assets)
        .AddSingleton(_store)
        .AddSingleton(_sessions)
        .AddSingleton<IDbContextFactory<GameDbContext>>(new TestFactory(_options))
        .AddSingleton<AccountRepository>()
        .AddSingleton<RoleRepository>()
        .AddSingleton(new GameServerOptions())
        .AddSingleton<IAuthenticator, TrustAllAuthenticator>()
        .AddSingleton<GameServerRuntime>()
        .AddSingleton<Router>()
        .BuildServiceProvider();

    private static byte[] RequestPacket(uint command, IMessage message) => new CSMsgPkg {
        Head = new CSMsgHead { Cmd = command }, Body = message.ToByteString()
    }.ToByteArray();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RegisteredRequests_HandleMissingFieldsAndNumericExtremes_WithoutFaulting(bool activeRole, bool extremes)
    {
        using var services = RouterServices();
        var router = services.GetRequiredService<Router>();
        var failures = new List<string>();
        var commands = typeof(Router).Assembly.GetTypes().SelectMany(t => t.GetMethods())
            .Where(m => m.GetCustomAttribute<GameHandlerAttribute>() is not null).ToArray();
        Assert.True(commands.Length > 100);
        foreach (var method in commands)
        {
            var command = method.GetCustomAttribute<GameHandlerAttribute>()!.CmdId;
            if (command == (uint)EClientServerCmds.CsAccountLogin || command == (uint)EClientServerCmds.CsCreateRole)
                continue;
            var ctx = Context();
            if (activeRole) Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
            var request = (IMessage)Activator.CreateInstance(method.GetParameters()[1].ParameterType)!;
            if (extremes)
                foreach (var field in request.Descriptor.Fields.InFieldNumberOrder().Where(f => !f.IsRepeated && !f.IsMap))
                {
                    object? value = field.FieldType switch {
                        FieldType.UInt32 or FieldType.Fixed32 => uint.MaxValue,
                        FieldType.UInt64 or FieldType.Fixed64 => ulong.MaxValue,
                        FieldType.Int32 or FieldType.SInt32 or FieldType.SFixed32 => int.MaxValue,
                        FieldType.Int64 or FieldType.SInt64 or FieldType.SFixed64 => long.MaxValue,
                        _ => null
                    };
                    if (value is not null) field.Accessor.SetValue(request, value);
                }
            try
            {
                await router.DispatchAsync(ctx, RequestPacket(command, request));
                Assert.False(ctx.PersistenceFaulted);
                if (!activeRole)
                {
                    Assert.False(ctx.Player.HasActiveRole);
                    Assert.False(ctx.Player.IsDirty);
                    Assert.Empty(ctx.Player.InventoryItems());
                }
            }
            catch (Exception ex) { failures.Add($"{request.Descriptor.Name}: {ex}"); }
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public async Task MalformedProtobuf_IsRejectedBeforeAnyGameplayMutation()
    {
        using var services = RouterServices();
        var router = services.GetRequiredService<Router>();
        var outbound = Channel.CreateUnbounded<byte[]>();
        var ctx = Context(outbound);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        await _store.SaveAsync(ctx.Player);
        var balance = ctx.Player.Wallet.Balance(1);
        byte[][] packets = [
            [0x0a, 0xff],
            new CSMsgPkg { Head = new CSMsgHead { Cmd = (uint)EClientServerCmds.CsShopBuy },
                Body = ByteString.CopyFrom([0x80]) }.ToByteArray()
        ];
        foreach (var packet in packets)
            await Assert.ThrowsAsync<IOException>(() => router.DispatchAsync(ctx, packet));
        Assert.False(ctx.PersistenceFaulted);
        Assert.False(ctx.Player.IsDirty);
        Assert.Equal(balance, ctx.Player.Wallet.Balance(1));
        Assert.False(outbound.Reader.TryPeek(out _));
    }
}
