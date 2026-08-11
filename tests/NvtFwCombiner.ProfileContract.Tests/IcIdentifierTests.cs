using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Contract tests for IC identifier syntax normalization.</summary>
public sealed class IcIdentifierTests
{
    /// <summary>Canonicalizes prefix casing, whitespace, and missing NT prefixes.</summary>
    [Theory]
    [InlineData("51950", "NT51950")]
    [InlineData("NT51950", "NT51950")]
    [InlineData("nt51950", "NT51950")]
    [InlineData(" 51950 ", "NT51950")]
    public void NormalizeReturnsCanonicalNtPrefixedIdentifier(
        string value,
        string expected)
    {
        Assert.Equal(expected, IcIdentifier.Normalize(value));
    }

    /// <summary>Rejects identifiers that do not contain a value.</summary>
    [Fact]
    public void NormalizeRejectsMissingIdentifier()
    {
        _ = Assert.Throws<ArgumentNullException>(() =>
            IcIdentifier.Normalize(null!));
        _ = Assert.Throws<ArgumentException>(() =>
            IcIdentifier.Normalize(" "));
    }
}
