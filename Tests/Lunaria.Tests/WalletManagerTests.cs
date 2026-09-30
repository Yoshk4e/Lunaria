using Lunaria.Game.Inventory;
using Lunaria.Tests.Support;
using Msg;
using Xunit;

namespace Lunaria.Tests;

[Collection(AssetsCollection.Name)]
public sealed class WalletManagerTests(TestAssets assets)
{
    private WalletManager Manager() => new(assets.Data);

    [Fact]
    public void GrantStarter_CreditsEveryCurrencyTheTableLists_Once()
    {
        var wallet = Manager();

        Assert.True(wallet.GrantStarter());
        Assert.Equal(expected: 100, wallet.Coin);
        Assert.Equal(TestAssets.CoinMoneyType, wallet.CoinMoneyType);

        Assert.False(wallet.GrantStarter());
        Assert.Equal(expected: 100, wallet.Coin);
    }

    [Fact]
    public void Credit_RefusesUnknownTypesAndNegativeAmounts()
    {
        var wallet = Manager();

        Assert.Equal(
            (int)EnmTextCode.EnmTextWrongParam,
            wallet.Credit(TestAssets.UnknownMoneyType, amount: 10));
        Assert.Equal((int)EnmTextCode.EnmTextInvalidArgs, wallet.Credit(TestAssets.CoinMoneyType, amount: -10));
        Assert.Equal(expected: 0, wallet.Coin);
        Assert.False(wallet.IsDirty);
    }

    [Fact]
    public void Credit_SaturatesInsteadOfOverflowing()
    {
        var wallet = Manager();
        Assert.Equal(expected: 0, wallet.Credit(TestAssets.CoinMoneyType, long.MaxValue - 5));

        Assert.Equal(expected: 0, wallet.Credit(TestAssets.CoinMoneyType, amount: 100));

        Assert.Equal(long.MaxValue, wallet.Coin);
    }

    [Fact]
    public void Debit_LeavesTheBalanceAloneWhenItCannotCover()
    {
        var wallet = Manager();
        wallet.Credit(TestAssets.CoinMoneyType, amount: 50);

        Assert.Equal((int)EnmTextCode.EnmTextItemNotEnough, wallet.Debit(TestAssets.CoinMoneyType, amount: 51));
        Assert.Equal(expected: 50, wallet.Coin);

        Assert.Equal((int)EnmTextCode.EnmTextInvalidArgs, wallet.Debit(TestAssets.CoinMoneyType, amount: -1));
        Assert.Equal(expected: 50, wallet.Coin);
    }

    [Fact]
    public void Debit_ToZero_DropsTheEntry()
    {
        var wallet = Manager();
        wallet.Credit(TestAssets.CoinMoneyType, amount: 50);

        Assert.Equal(expected: 0, wallet.DebitCoin(50));

        Assert.True(wallet.IsEmpty);
        Assert.Empty(wallet.MoneyData());
    }

    [Fact]
    public void Load_DropsTypesTheTableNoLongerKnows()
    {
        var wallet = Manager();

        wallet.Load([(TestAssets.CoinMoneyType, 25), (TestAssets.UnknownMoneyType, 999), (2, 0)]);

        Assert.Equal(expected: 25, wallet.Coin);
        Assert.Equal(expected: 0, wallet.Balance(TestAssets.UnknownMoneyType));
        Assert.Equal(expected: 0, wallet.Balance(2));
        Assert.False(wallet.IsDirty);
    }
}
