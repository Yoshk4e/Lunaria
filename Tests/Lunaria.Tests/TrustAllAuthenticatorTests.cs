using Lunaria.Game.Player.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Lunaria.Tests;

public sealed class TrustAllAuthenticatorTests
{
    private static readonly TrustAllAuthenticator Auth = new(NullLogger<TrustAllAuthenticator>.Instance);

    private static LoginAttempt Attempt(string userid = "", string channel = "", string channelUid = "", string udid = "") => new() {
        Userid = userid,
        ChannelName = channel,
        ChannelUid = channelUid,
        Udid = udid,
        Version = "0.09.70.4",
        HeiTokenLength = 0,
        ChannelTokenLength = 0
    };

    private static async Task<string> KeyOf(LoginAttempt attempt) => (await Auth.AuthenticateAsync(attempt))!.AccountKey;

    [Fact]
    public async Task SdkAccountsOnOneDevice_KeepSeparateGameAccounts()
    {
        // The SDK login sends no userid; the device is shared by every account signed in on this PC.
        var first = await KeyOf(Attempt(channel: "email", channelUid: "01a0f3f5774e744fa6922a7b0dc1b96e", udid: "pc"));
        var second = await KeyOf(Attempt(channel: "email", channelUid: "01a1069b0a5579f39b2dc40cb0e9fcef", udid: "pc"));

        Assert.Equal("email:01a0f3f5774e744fa6922a7b0dc1b96e", first);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task Userid_ThenChannel_ThenDevice_IdentifyTheAccount()
    {
        Assert.Equal("u1", await KeyOf(Attempt(userid: "u1", channel: "email", channelUid: "c1", udid: "pc")));
        Assert.Equal("device:pc", await KeyOf(Attempt(udid: "pc")));
        Assert.Equal("device:pc", await KeyOf(Attempt(channelUid: "c1", udid: "pc")));
        Assert.StartsWith("anon:", await KeyOf(Attempt()));
    }
}
