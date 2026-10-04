using Lunaria.Game.Player;
using Lunaria.Game.Resources;
using Msg;
using Xunit;

namespace Lunaria.Tests;

[Collection("bundled-gameplay")]
public sealed class AutoUseBuffItemTests(BundledGameplayFixture fixture)
{
    [Fact]
    public void ReceivedCafeDish_AppliesItsBuffInsteadOfStayingInTheBag()
    {
        const uint dish = 21207003; // AutoUse, AddBuffEffect, item effect 7003 -> buff 130003
        var player = new Player(1, fixture.Data);
        player.Characters.GrantStarter(player.Guid);
        player.Teams.GrantStarter(player.Characters);

        player.GrantRewards([new ItemGrant(dish, 1)], EnmItemReason.EnmItemChangeShopBuy);

        Assert.Equal(0u, player.Bag.CountOf(dish));
        Assert.Contains(130003u, player.Buffs.Buffs.Keys);
    }
}
