using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

/// <summary>Tests stable IC number selector tokens shared across adapters.</summary>
public sealed class IcNumberSelectionTokensTests
{
    /// <summary>Pins the reviewed non-numeric cascade range token.</summary>
    [Fact]
    public void CascadeRangeTokenRemainsCanonical()
    {
        Assert.Equal("cascade_2to8", IcNumberSelectionTokens.CascadeTwoToEight);
    }

    /// <summary>Verifies the single selector comparison is case-insensitive and token-specific.</summary>
    [Theory]
    [InlineData("single", true)]
    [InlineData("SINGLE", true)]
    [InlineData("cascade", false)]
    [InlineData("1", false)]
    public void IsSingleRecognizesOnlyTheSingleSelectorToken(string value, bool expected)
    {
        Assert.Equal(expected, IcNumberSelectionTokens.IsSingle(value));
    }
}
