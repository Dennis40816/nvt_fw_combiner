using NvtFwCombiner.Application.Composition;

namespace NvtFwCombiner.Application.Tests.Composition;

/// <summary>Verifies focused workbench contracts owned by Application.</summary>
public sealed class WorkbenchCompositionModelsTests
{
    /// <summary>DP display keeps the canonical split while retaining a readable fallback for short tokens.</summary>
    [Theory]
    [InlineData("0102", "D01-02")]
    [InlineData("01", "D01")]
    public void DpVersionDisplayFormatsCanonicalAndFallbackTokens(
        string token,
        string expected)
    {
        Assert.Equal(expected, WorkbenchDpVersionMetadata.FormatDisplayValue(token));
    }
}
