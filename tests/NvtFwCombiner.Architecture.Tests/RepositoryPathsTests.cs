using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Architecture.Tests;

/// <summary>Shared resolver validation does not need process-wide environment mutation.</summary>
public sealed class RepositoryPathsTests
{
    /// <summary>Explicit empty configuration is invalid even on platforms that unset empty variables.</summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("relative/example")]
    public void InvalidTestDataConfigurationFailsClosed(string configuredRoot)
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => RepositoryPaths.FindTestDataRoot(configuredRoot));

        Assert.Contains("NFC_TESTDATA_ROOT", exception.Message, StringComparison.Ordinal);
    }
}
