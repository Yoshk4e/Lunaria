using Lunaria.Game.Characters;
using Xunit;

namespace Lunaria.Tests;

public sealed class TalentMasksTests
{
    [Fact]
    public void FreshMasks_NothingUnlocked()
    {
        var masks = new TalentMasks();
        Assert.False(masks.IsUnlocked(0));
        Assert.False(masks.IsUnlocked(127));
        Assert.False(masks.IsUnlocked(128));
    }

    [Fact]
    public void Unlock_SetsOnlyThatNode()
    {
        var masks = new TalentMasks().WithUnlock(5).WithUnlock(70);
        Assert.True(masks.IsUnlocked(5));
        Assert.True(masks.IsUnlocked(70));
        Assert.False(masks.IsUnlocked(6));
        Assert.False(masks.IsUnlocked(69));
    }
}
