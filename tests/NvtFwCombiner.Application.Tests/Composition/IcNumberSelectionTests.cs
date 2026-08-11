using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Composition;

/// <summary>Tests canonical UI/CLI number-token admission.</summary>
public sealed class IcNumberSelectionTests
{
    /// <summary>Preserves single, numeric, and range-cascade selection modes.</summary>
    [Theory]
    [InlineData(IcNumberSelectionTokens.SingleChip, IcNumberInputMode.SingleSelector)]
    [InlineData("3", IcNumberInputMode.NumericSelector)]
    [InlineData(IcNumberSelectionTokens.Cascade, IcNumberInputMode.CascadeSelector)]
    [InlineData(IcNumberSelectionTokens.CascadeTwoToEight, IcNumberInputMode.CascadeSelector)]
    public void FromTokenUsesOneCanonicalModeParser(string token, IcNumberInputMode expectedMode)
    {
        var selection = IcNumberSelection.FromToken(token);

        Assert.Equal(expectedMode, selection.Mode);
        Assert.Equal([token], selection.Parts);
    }
}
