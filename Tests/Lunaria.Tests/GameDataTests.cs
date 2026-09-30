using Lunaria.Game.Resources;
using Lunaria.Tests.Support;
using Xunit;

namespace Lunaria.Tests;

[Collection(AssetsCollection.Name)]
public sealed class GameDataTests(TestAssets assets)
{
    private GameData Data => assets.Data;

    [Fact]
    public void Starter_ReadsTheTableRatherThanConstants()
    {
        Assert.Equal(TestAssets.StarterMap, Data.Starter.MapId);
        Assert.Equal((1, 2, 3), Data.Starter.SpawnPos);
        Assert.Equal([TestAssets.CharacterId], Data.Starter.Characters);
        Assert.Equal([TestAssets.CharacterId], Data.Starter.Team);
        Assert.Equal(expected: 110, Data.Starter.Satiety);
        Assert.Equal(Data.GlobalConfig.StaminaRegenMax, Data.Starter.Stamina);
        Assert.Equal(expected: 540u, Data.Starter.GameTime);
    }

    [Fact]
    public void Starter_SplitsCurrencyFromBagItems()
    {
        Assert.Equal(TestAssets.CurrencyItem, Data.Starter.Currency.ItemId);
        Assert.Equal(expected: 100u, Data.Starter.Currency.Count);
        Assert.Equal(TestAssets.CoinMoneyType, Data.Starter.CoinMoneyType);

        Assert.Equal(
            [TestAssets.MaterialItem, TestAssets.UnknownItem],
            Data.Starter.Items.Select(grant => grant.ItemId).ToList());
    }

    [Fact]
    public void DefaultSavepoint_IsLowestOfTheDefaultRows()
    {
        Assert.Equal(TestAssets.DefaultSavepoint, Data.Maps.DefaultSavepoint);
        Assert.Equal(TestAssets.DefaultSavepoint, Data.Starter.Savepoint);
    }

    [Fact]
    public void Maps_ExcludeClientOnly_AndKnowWhichArePlayable()
    {
        Assert.True(Data.Maps.MapExists(TestAssets.StarterMap));
        Assert.False(Data.Maps.MapExists(TestAssets.ClientOnlyMap));
        Assert.True(Data.Maps.IsPlayable(TestAssets.StarterMap));
        Assert.False(Data.Maps.IsPlayable(TestAssets.UnplayableMap));
        Assert.Equal((4, 5, 6), Data.Maps.SpawnPos(TestAssets.SecondMap));
    }

    [Fact]
    public void Items_MapCurrencyItemsToMoneyTypes()
    {
        Assert.True(Data.Items.IsCurrency(TestAssets.CurrencyItem));
        Assert.Equal(TestAssets.CoinMoneyType, Data.Items.MoneyTypeOf(TestAssets.CurrencyItem));
        Assert.False(Data.Items.IsCurrency(TestAssets.MaterialItem));
        Assert.Null(Data.Items.MoneyTypeOf(TestAssets.MaterialItem));
        Assert.Equal(expected: 10u, Data.Items.HoldLimit(TestAssets.MaterialItem));
        Assert.Equal(expected: 0u, Data.Items.HoldLimit(TestAssets.UnknownItem));
        Assert.False(Data.Items.IsMoneyType(TestAssets.UnknownMoneyType));
    }

    [Fact]
    public void Characters_ReadBreakAndLevelLaddersPerCharacter()
    {
        Assert.Equal(TestAssets.CapAtBreakZero, Data.Characters.LevelCap(TestAssets.CharacterId, breakLevel: 0));
        Assert.Equal(TestAssets.CapAtBreakOne, Data.Characters.LevelCap(TestAssets.CharacterId, breakLevel: 1));

        Assert.Equal(TestAssets.CapAtBreakOne, Data.Characters.LevelCap(TestAssets.CharacterId, breakLevel: 99));
        Assert.Equal(TestAssets.CapAtBreakOne, Data.Characters.MaxLevel(TestAssets.CharacterId));

        var step = Data.Characters.NextBreak(TestAssets.CharacterId, breakLevel: 0);
        Assert.NotNull(step);
        Assert.Equal(expected: 1u, step.BreakLevel);
        Assert.Equal(TestAssets.BreakWorldLevel, step.NeedWorldLevel);
        Assert.Equal(expected: 500u, step.CostCurrency);
        Assert.Equal([new ItemGrant(TestAssets.BreakItem, Count: 2)], step.CostItems);

        Assert.Null(Data.Characters.NextBreak(TestAssets.CharacterId, breakLevel: 1));
        Assert.True(Data.Characters.IsFullyBroken(TestAssets.CharacterId, breakLevel: 1));
    }

    [Fact]
    public void DevelopAttributeId_ComesFromTheTable_NotADerivedKey()
    {
        Assert.Equal(expected: 100103u, Data.Characters.DevelopAttributeId(TestAssets.CharacterId, level: 3));

        Assert.Equal(expected: 100109u, Data.Characters.DevelopAttributeId(TestAssets.CharacterId, level: 9));
        Assert.NotEmpty(Data.Attribs.ForCharacter(TestAssets.CharacterId, developAttributeId: 100101));
    }

    [Fact]
    public void Skills_CeilingIsWhateverTheCostTablePrices()
    {
        Assert.Equal(expected: 1u, Data.Skills.InitLevel(TestAssets.PricedGroup));
        Assert.Equal(expected: 3u, Data.Skills.MaxLevel(TestAssets.PricedGroup));

        Assert.Equal(expected: 0u, Data.Skills.InitLevel(TestAssets.LockedGroup));
        Assert.Equal(expected: 0u, Data.Skills.MaxLevel(TestAssets.LockedGroup));
        Assert.Null(Data.Skills.CostOf(TestAssets.LockedGroup, level: 1));

        var cost = Data.Skills.CostOf(TestAssets.PricedGroup, level: 2);
        Assert.NotNull(cost);
        Assert.Equal(expected: 100u, cost.Coin);
        Assert.Equal([new ItemGrant(TestAssets.MaterialItem, Count: 1)], cost.Items);
    }

    [Fact]
    public void Progression_ExposesBothLaddersWithOneConvention()
    {
        Assert.Equal(expected: 10u, Data.Progression.CharacterExpToAdvance(1));
        Assert.Equal(expected: 40u, Data.Progression.CharacterExpToAdvance(4));
        Assert.Null(Data.Progression.CharacterExpToAdvance(5));

        Assert.Equal(expected: 100u, Data.Progression.TeamExpToAdvance(1));
        Assert.Null(Data.Progression.TeamExpToAdvance(3));

        Assert.Equal(expected: 1u, Data.Progression.WorldLevelFor(1));
        Assert.Equal(expected: 2u, Data.Progression.WorldLevelFor(2));
        Assert.Equal(expected: 2u, Data.Progression.TeamLevelCeiling(1));
        Assert.Equal(expected: 3u, Data.Progression.TeamLevelCeiling(2));
    }

    [Fact]
    public void Guides_AndTalents_AreFenced()
    {
        Assert.True(Data.Guides.Exists(TestAssets.Guide));
        Assert.False(Data.Guides.Exists(TestAssets.UnknownGuide));
        Assert.Equal(expected: 2, Data.Guides.Count);

        Assert.True(Data.Talents.NodeExists(0));
        Assert.False(Data.Talents.NodeExists(2));
        Assert.Equal([0u], Data.Talents.Prerequisites(1));
        Assert.Equal(expected: 3u, Data.Talents.UnlockLevel(1));
    }

    [Fact]
    public async Task StartAsync_MissingDump_ThrowsNamedError()
    {
        var data = new GameData(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        var ex = await Assert.ThrowsAsync<ResourceException>(() => data.StartAsync(CancellationToken.None));
        Assert.Contains("tables", ex.Path);
    }
}
