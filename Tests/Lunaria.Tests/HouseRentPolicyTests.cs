using Lunaria.Game.Player.Managers;
using Lunaria.Game.Resources;
using Xunit;

namespace Lunaria.Tests;

[Collection("bundled-gameplay")]
public sealed class HouseRentPolicyTests(BundledGameplayFixture fixture)
{
    [Fact]
    public void IndependentSchedule_PreservesPartialIntervals_AndRejectsClockRollback()
    {
        var houses = new HouseManager(fixture.Data, new HouseRentPolicy { IntervalSeconds = 60, MaxIntervals = 2 });
        var id = fixture.Data.Houses.All.First().Id;
        var start = new DateTimeOffset(2026, 9, 25, 0, 0, 0, TimeSpan.Zero);
        houses.MarkBought(id, start);
        houses.MarkOpened(id, start);
        var rate = fixture.Data.Houses.IncomePerInterval(id, 1);
        Assert.True(rate > 0);
        Assert.Equal(0u, houses.AccruedIncome(id, start.AddSeconds(59)));
        Assert.Equal(rate, houses.AccruedIncome(id, start.AddSeconds(90)));
        houses.MarkIncomeClaimed([id], start.AddSeconds(90));
        Assert.Equal(rate, houses.AccruedIncome(id, start.AddSeconds(120)));
        houses.MarkIncomeClaimed([id], start.AddSeconds(10));
        Assert.Equal(rate, houses.AccruedIncome(id, start.AddSeconds(120)));
        Assert.Equal(2 * rate, houses.AccruedIncome(id, start.AddDays(1)));
    }
}
