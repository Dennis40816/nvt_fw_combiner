using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

/// <summary>Tests stable IC number selector tokens shared across adapters.</summary>
public sealed class IcNumberSelectionTokensTests
{
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
