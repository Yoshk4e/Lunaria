using System.Text;
using Lunaria.Game.Player.Text;
using Xunit;

namespace Lunaria.Tests;

public sealed class TextValidatorTests
{
    [Fact]
    public void AsciiName_WeightIsLength()
    {
        Assert.Equal(expected: 3, TextValidator.NameWeight("abc"));
    }

    [Fact]
    public void MultibyteChar_CountsDouble()
    {
        Assert.Equal(expected: 2, TextValidator.NameWeight("あ"));
    }

    [Fact]
    public void EmptyName_IsRejected()
    {
        Assert.NotNull(TextValidator.ValidatePlayerName(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void OverBudgetName_IsRejected()
    {
        var raw = Encoding.UTF8.GetBytes(new string(c: 'a', TextValidator.MaxNameWeight + 1));
        Assert.NotNull(TextValidator.ValidatePlayerName(raw));
    }

    [Fact]
    public void InBudgetName_Passes()
    {
        var raw = Encoding.UTF8.GetBytes("Luna");
        Assert.Null(TextValidator.ValidatePlayerName(raw));
    }
}
