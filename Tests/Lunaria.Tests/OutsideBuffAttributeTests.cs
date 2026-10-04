using Lunaria.Game.Player;
using Lunaria.Game.Resources;
using Msg;
using Xunit;

namespace Lunaria.Tests;

[Collection("bundled-gameplay")]
public sealed class OutsideBuffAttributeTests(BundledGameplayFixture fixture)
{
    [Fact]
    public void ActiveTeamBuff_AddsItsTempAttributesToTheSentAttributes()
    {
        var player = new Player(1, fixture.Data);
        player.Characters.GrantStarter(player.Guid);
        player.Teams.GrantStarter(player.Characters);
        var instId = player.Characters.All.First().InstId;
        int Final(int id) => player.Characters.AttribData(instId).AttribData.SingleOrDefault(a => a.AttribType == id)?.FinalValue ?? 0;
        var crit = Final(104);
        var damage = Final(301);

        // Buff 110003: TempAttribute1 [104, 600, 0] (CRIT Rate +6%), TempAttribute2 [301, 700, 0] (DMG Bonus +7%).
        var (code, _, _, _) = player.Buffs.Apply(110003, DateTimeOffset.UtcNow);
        Assert.Equal(0, code);

        Assert.Equal(crit + 600, Final(104));
        Assert.Equal(damage + 700, Final(301));
    }
}
