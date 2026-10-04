using System.Threading.Channels;
using Lunaria.GameServer.Net;
using Lunaria.Proto;
using Microsoft.Extensions.DependencyInjection;
using Msg;
using Xunit;

namespace Lunaria.Tests;

public sealed partial class RoleSessionTests
{
    [Fact]
    public async Task ClaimingARegionReward_SendsTheSubRegionWithTheClaimedValue()
    {
        const ulong subRegionId = 100001004002;
        var outbound = Channel.CreateUnbounded<byte[]>();
        var ctx = Context(outbound);
        Assert.Equal(0, await _sessions.ActivateAsync(ctx, 1));
        foreach (var sequenceId in _assets.RegionProgress.Sequences(subRegionId))
            ctx.Player.RegionProgress.SetSequence(subRegionId, sequenceId,
                _assets.RegionProgress.SequenceRow(subRegionId, sequenceId)!.ParamNum);
        ReadReplyPackets(outbound);

        using var services = RouterServices();
        var request = new CSReqClaimReward { SubRegionId = subRegionId };
        await services.GetRequiredService<Router>().DispatchAsync(ctx, RequestPacket(MessageCmdRegistry.CmdIdOf(request)!.Value, request));
        var packets = ReadReplyPackets(outbound);

        var reply = SCResClaimReward.Parser.ParseFrom(Assert.Single(packets,
            packet => packet.Head.Cmd == MessageCmdRegistry.CmdIdOf(new SCResClaimReward())).Body);
        Assert.Equal(0u, reply.Result);
        var update = SCSubRegionUpdateNtf.Parser.ParseFrom(Assert.Single(packets,
            packet => packet.Head.Cmd == MessageCmdRegistry.CmdIdOf(new SCSubRegionUpdateNtf())).Body);
        Assert.Equal(subRegionId, update.SubRegionData.SubRegionId);
        Assert.Equal(reply.ProgressValues, update.SubRegionData.ProgressValue);
    }
}
