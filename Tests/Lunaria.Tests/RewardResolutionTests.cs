using Lunaria.Game.Player.Rewards;
using Lunaria.Game.Resources;
using Lunaria.Game.Resources.Tables;
using Xunit;

namespace Lunaria.Tests;

public sealed class RewardResolutionTests
{
    private static ItemAssets Items() => new(new Dictionary<string, PItemTable> {
        ["10"] = new() { Id = 10, AutoUse = true, UseType = (int)ItemUseType.AddDrop, Param = [100] },
        ["11"] = new() { Id = 11, AutoUse = true, UseType = (int)ItemUseType.AddDrop, Param = [101] },
        ["12"] = new() { Id = 12, AutoUse = true, UseType = (int)ItemUseType.AddDrop, Param = [102] },
        ["20"] = new() { Id = 20 }
    }, new Dictionary<string, PItemTypeTable>(), new Dictionary<string, PMoneyTable> {
        ["1"] = new() { Id = 1, MoneyType = 1 }
    }, new Dictionary<string, PCharacterConstTable>());

    private static DropAssets Drops(params (uint Drop, uint Item, uint Count)[] rows) => new(rows
        .Select((r, i) => new PFixedDropTable { Id = (uint)i + 1, DropId = r.Drop, ItemId = r.Item, ItemCount = r.Count })
        .ToDictionary(r => r.Id.ToString()));

    [Fact]
    public void NestedPacks_MultiplyQuantities_AndKeepIndependentGrants()
    {
        var result = RewardResolver.Resolve(Items(), Drops((100, 11, 3), (101, 20, 5)), [new(10, 2), new(20, 7)]);
        Assert.Equal(new[] { new ItemGrant(20, 30), new ItemGrant(20, 7) }, result.Items);
        Assert.Empty(result.Unresolved);
    }

    [Theory]
    [InlineData(10u)] // direct cycle
    [InlineData(11u)] // indirect cycle
    [InlineData(12u)] // missing nested drop
    public void InvalidPack_RetainsWholeRootWithoutLeakingPartialRewards(uint nested)
    {
        var result = RewardResolver.Resolve(Items(), Drops((100, 20, 5), (100, nested, 1), (101, 10, 1)),
            [new(10, 2), new(20, 7)]);
        Assert.Equal(new ItemGrant(20, 7), Assert.Single(result.Items));
        Assert.Equal(new ItemGrant(10, 2), Assert.Single(result.Unresolved));
    }

    [Fact]
    public void ExpansionLimit_RetainsHugePackWithoutOverflowOrPartialDelivery()
    {
        var result = RewardResolver.Resolve(Items(), Drops((100, 20, uint.MaxValue)), [new(10, uint.MaxValue)]);
        Assert.Empty(result.Items);
        Assert.Equal(new ItemGrant(10, uint.MaxValue), Assert.Single(result.Unresolved));
    }
}
