using System.Reflection;
using System.Threading.Channels;
using Google.Protobuf;
using Lunaria.Game.Player;
using Lunaria.Game.Resources;
using Lunaria.GameServer.Net;
using Lunaria.Proto;
using Lunaria.Silver;
using Microsoft.Extensions.DependencyInjection;
using Msg;
using Xunit;

namespace Lunaria.Tests;

public sealed partial class RoleSessionTests
{
    private const int LoginRequired = (int)EnmTextCode.EnmTextNotAccLogin;

    private async Task<TReply> DispatchReplyAsync<TReply>(NetContext ctx, Channel<byte[]> outbound,
        IMessage request, MessageParser<TReply> parser) where TReply : IMessage<TReply>
    {
        using var services = RouterServices();
        var router = services.GetRequiredService<Router>();
        await router.DispatchAsync(ctx, RequestPacket(MessageCmdRegistry.CmdIdOf(request)!.Value, request));
        var expectedCommand = MessageCmdRegistry.CmdIdOf(parser.ParseFrom(ByteString.Empty));
        var reply = Assert.Single(ReadReplyPackets(outbound).Where(packet => packet.Head.Cmd == expectedCommand));
        return parser.ParseFrom(reply.Body);
    }

    private static List<CSMsgPkg> ReadReplyPackets(Channel<byte[]> outbound)
    {
        var packets = new List<CSMsgPkg>();
        var aes = new AesSession(new byte[16]);
        while (outbound.Reader.TryRead(out var bytes))
        {
            Assert.True(Frame.TryDecode(bytes, out var frame));
            Assert.Equal(FrameType.Data, frame.MessageType);
            packets.Add(CSMsgPkg.Parser.ParseFrom(aes.Decrypt(frame.Payload).AsSpan(0, frame.LengthB)));
        }
        return packets;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EveryProtectedHandler_RejectsMissingLoginWithoutChangingState(bool accountBound)
    {
        using var services = RouterServices();
        var router = services.GetRequiredService<Router>();
        var methods = typeof(Router).Assembly.GetTypes().SelectMany(type => type.GetMethods())
            .Where(method => method.GetCustomAttribute<GameHandlerAttribute>() is not null).ToArray();
        Assert.True(methods.Length > 100);
        foreach (var method in methods)
        {
            var guard = method.GetCustomAttribute<RequireLoginAttribute>();
            if (guard is null)
            {
                Assert.Contains((EClientServerCmds)method.GetCustomAttribute<GameHandlerAttribute>()!.CmdId,
                    new[] { EClientServerCmds.CsAccountLogin, EClientServerCmds.CsSchemaInfoSync, EClientServerCmds.CsCheckText });
                continue;
            }
            if (accountBound && guard.Requirement == LoginRequirement.Account) continue;
            var outbound = Channel.CreateUnbounded<byte[]>();
            var ctx = accountBound ? Context(outbound) : new NetContext(new Player(1, _assets), null!,
                new SessionCodec(new AesSession(new byte[16])), outbound.Writer, _assets, _metrics);
            var map = ctx.Player.Map.SpawnMap;
            var time = ctx.Player.GameTimeMinutes;
            var request = (IMessage)Activator.CreateInstance(method.GetParameters()[1].ParameterType)!;
            var command = method.GetCustomAttribute<GameHandlerAttribute>()!.CmdId;
            await router.DispatchAsync(ctx, RequestPacket(command, request));

            Assert.False(ctx.Player.HasActiveRole);
            Assert.False(ctx.Player.IsDirty);
            Assert.False(ctx.Player.TasksBootstrapped);
            Assert.False(ctx.PersistenceFaulted);
            Assert.Equal(map, ctx.Player.Map.SpawnMap);
            Assert.Equal(time, ctx.Player.GameTimeMinutes);
            var replies = ReadReplyPackets(outbound);
            var replyType = method.ReturnType.IsGenericType ? method.ReturnType.GenericTypeArguments[0] : guard.Reply;
            if (replyType is null)
            {
                Assert.Empty(replies);
                continue;
            }
            var reply = Assert.Single(replies);
            var prototype = (IMessage)Activator.CreateInstance(replyType)!;
            Assert.Equal(MessageCmdRegistry.CmdIdOf(prototype), reply.Head.Cmd);
            var message = prototype.Descriptor.Parser.ParseFrom(reply.Body);
            var statusFields = message.Descriptor.Fields.InFieldNumberOrder()
                .Where(field => field.Name is "result" or "ret" or "gender_result" or "role_name_result" or "second_role_name_result");
            foreach (var status in statusFields)
                Assert.Equal(LoginRequired, Convert.ToInt32(status.Accessor.GetValue(message)));
        }
    }

    [Fact]
    public async Task AccountLogin_AllowsRoleSelectionAndRoleLoginBeforeGameplay()
    {
        var outbound = Channel.CreateUnbounded<byte[]>();
        var ctx = Context(outbound);
        var list = await DispatchReplyAsync(ctx, outbound, new CSCharacterListReq(), SCCharacterListRsp.Parser);
        Assert.Equal(0, list.Result);
        Assert.False(ctx.Player.HasActiveRole);
        var denied = await DispatchReplyAsync(ctx, outbound, new CSItemBagGetList(), SCItemBagGetList.Parser);
        Assert.Equal(LoginRequired, denied.Result);
        var login = await DispatchReplyAsync(ctx, outbound, new CSRoleLogin { RoleId = 1 }, SCRoleLogin.Parser);
        Assert.Equal(0, login.Result);
        Assert.True(ctx.Player.HasActiveRole);
        var bag = await DispatchReplyAsync(ctx, outbound, new CSItemBagGetList(), SCItemBagGetList.Parser);
        Assert.Equal(0, bag.Result);
        var logout = await DispatchReplyAsync(ctx, outbound, new CSRoleLogout { RoleId = 1 }, SCRoleLogout.Parser);
        Assert.Equal(0, logout.Result);
        Assert.Equal(1ul, logout.RoleId);
        denied = await DispatchReplyAsync(ctx, outbound, new CSItemBagGetList(), SCItemBagGetList.Parser);
        Assert.Equal(LoginRequired, denied.Result);
    }

    [Fact]
    public async Task LoginRejection_PreservesEchoedIdsNestedDataAndUnsignedStatus()
    {
        var outbound = Channel.CreateUnbounded<byte[]>();
        var ctx = Context(outbound);
        var collection = await DispatchReplyAsync(ctx, outbound, new CSCollectionOperate { UniqId = 123 }, SCCollectionOperate.Parser);
        Assert.Equal(LoginRequired, collection.Result);
        Assert.Equal(123u, collection.CollectionItem.UniqId);
        var dungeon = await DispatchReplyAsync(ctx, outbound, new CSDungeonsFullData(), SCDungeonsFullData.Parser);
        Assert.Equal(LoginRequired, dungeon.Result);
        Assert.NotNull(dungeon.Data.CommonData);
        Assert.NotNull(dungeon.Data.HordeData);
        var pass = await DispatchReplyAsync(ctx, outbound, new CSBattlePassData { BattlePassId = { 7, 8 } }, SCBattlePassData.Parser);
        Assert.Equal(LoginRequired, pass.Result);
        Assert.Equal(new uint[] { 7, 8 }, pass.Datas.Select(data => data.Id));
        var daily = await DispatchReplyAsync(ctx, outbound, new CSDailyMissionQuery(), SCDailyMissionQuery.Parser);
        Assert.Equal(LoginRequired, daily.Result);
        Assert.NotNull(daily.Data.MissionListData);
        Assert.NotNull(daily.Data.RewardData);
        var signin = await DispatchReplyAsync(ctx, outbound, new CSSignInActivityData { ActivityId = 15 }, SCSignInActivityData.Parser);
        Assert.Equal(LoginRequired, signin.Result);
        Assert.Equal(15u, signin.ActivityData.ActivityId);
        var region = await DispatchReplyAsync(ctx, outbound, new CSReqClaimReward { SubRegionId = 29 }, SCResClaimReward.Parser);
        Assert.Equal((uint)LoginRequired, region.Result);
        Assert.Equal(29ul, region.SubRegionId);
        var battle = await DispatchReplyAsync(ctx, outbound,
            new CSEnterBattle { BattleType = EBattleType.EnmBattleTypeWanted, BattleFieldId = 71 }, SCEnterBattle.Parser);
        Assert.Equal(LoginRequired, battle.Ret);
        Assert.Equal(EBattleType.EnmBattleTypeWanted, battle.BattleType);
        Assert.Equal(71u, battle.BattleFieldId);
        var mail = await DispatchReplyAsync(ctx, outbound, new CSMailGetList { FromMailId = 31, Count = 5 }, SCMailGetList.Parser);
        Assert.Equal(31u, mail.FromMailId);
        Assert.Equal(5, mail.Count);
        Assert.Empty(mail.Mails);
    }

    [Fact]
    public async Task LoginRejection_PreservesNamingResultsAndTaskHandlerReply()
    {
        var outbound = Channel.CreateUnbounded<byte[]>();
        var ctx = new NetContext(new Player(1, _assets), null!, new SessionCodec(new AesSession(new byte[16])),
            outbound.Writer, _assets, _metrics);
        var request = new CSInitRoleGenderAndName {
            Gender = (EnmGender)1, RoleName = ByteString.CopyFromUtf8("First"), SecondRoleName = ByteString.CopyFromUtf8("Second")
        };
        var naming = await DispatchReplyAsync(ctx, outbound, request, SCInitRoleGenderAndName.Parser);
        Assert.Equal(LoginRequired, naming.GenderResult);
        Assert.Equal(LoginRequired, naming.RoleNameResult);
        Assert.Equal(LoginRequired, naming.SecondRoleNameResult);
        Assert.Equal(request.Gender, naming.Gender);
        Assert.Equal(request.RoleName, naming.RoleName);
        Assert.Equal(request.SecondRoleName, naming.SecondRoleName);
        var task = await DispatchReplyAsync(ctx, outbound, new CSTaskActionUpdate { TaskType = 2, ActionId = 43 }, SCTaskActionUpdate.Parser);
        Assert.Equal(LoginRequired, task.Result);
        Assert.Equal(2u, task.TaskType);
        Assert.Equal(43u, task.ActionId);
        Assert.Equal(1u, task.MaxProgress);
        Assert.False(ctx.Player.TasksBootstrapped);
    }

    [Fact]
    public async Task RejectedGameplay_DoesNotSaveOrDrainPendingNotifications()
    {
        var outbound = Channel.CreateUnbounded<byte[]>();
        var ctx = Context(outbound);
        ctx.Player.GrantRewards([new ItemGrant(1, 42)], EnmItemReason.EnmItemChangeNormal);
        var balance = ctx.Player.Wallet.Balance(1);
        // A persistence attempt would open a new empty in-memory database and fail.
        _connection.Close();
        var reply = await DispatchReplyAsync(ctx, outbound, new CSGameTimeSetupReq { PassTime = 100 }, SCGameTimeSetupRes.Parser);
        Assert.Equal(LoginRequired, reply.Result);
        Assert.Equal(balance, ctx.Player.Wallet.Balance(1));
        Assert.False(ctx.PersistenceFaulted);
        Assert.NotEmpty(ctx.Player.DrainGameplayChanges());
    }
}
