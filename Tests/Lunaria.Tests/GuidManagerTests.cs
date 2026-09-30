using Lunaria.Common;
using Xunit;

namespace Lunaria.Tests;

public sealed class GuidManagerTests
{
    [Fact]
    public void ExhaustedBatch_DoesNotConsumeTheRemainingIds()
    {
        var guids = new GuidManager();
        guids.Adopt(ulong.MaxValue - 2);
        Assert.Throws<OverflowException>(() => guids.NextMany(3));
        Assert.Equal(ulong.MaxValue - 2, guids.LastMinted);
        Assert.Equal(ulong.MaxValue - 1, guids.NextMany(2));
        Assert.Equal(ulong.MaxValue, guids.LastMinted);
        Assert.Throws<OverflowException>(() => guids.Next());
        Assert.Equal(ulong.MaxValue, guids.LastMinted);
    }

    [Fact]
    public void ExhaustedPeek_CannotReturnTheReservedZeroId()
    {
        var guids = new GuidManager();
        guids.Adopt(ulong.MaxValue);
        Assert.Throws<OverflowException>(() => guids.Peek);
    }

    [Fact]
    public void FirstMintIsOne_ZeroStaysReserved()
    {
        var guids = new GuidManager();
        Assert.Equal(expected: 1UL, guids.Next());
    }

    [Fact]
    public void MintRunsNeverCollide()
    {
        var guids = new GuidManager();
        Assert.Equal(expected: 1UL, guids.Next());
        Assert.Equal(expected: 2UL, guids.NextMany(3));
        Assert.Equal(expected: 5UL, guids.Next());
        Assert.Equal(expected: 6UL, guids.Peek);
        Assert.Equal(expected: 5UL, guids.LastMinted);
    }

    [Fact]
    public void Adopt_MovesForward_NeverBackward()
    {
        var guids = new GuidManager();
        guids.Adopt(41);
        Assert.Equal(expected: 42UL, guids.Next());

        guids.Adopt(7);
        Assert.Equal(expected: 43UL, guids.Next());
        Assert.Equal(expected: 43UL, guids.LastMinted);
    }

    [Fact]
    public void Adopt_Zero_IsNoop()
    {
        var guids = new GuidManager();
        guids.Adopt(0);
        Assert.Equal(expected: 1UL, guids.Next());
    }
}
