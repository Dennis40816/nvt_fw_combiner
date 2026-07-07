using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests.FlashMaps;

/// <summary>Golden-backed checks for gen_flash version-byte rules.</summary>
public sealed class GenFlashVersionCatalogTests
{
    /// <summary>Reads DP version bytes from owner-approved gen_flash standard-merge DP inputs.</summary>
    [Theory]
    [InlineData("51920", "01")]
    [InlineData("51923", "81")]
    [InlineData("51926", "01")]
    [InlineData("51927", "54")]
    [InlineData("51928", "82")]
    [InlineData("51929", "02")]
    [InlineData("51931", "8D")]
    [InlineData("51932", "82")]
    public void GoldenDpInputsExposeExpectedGenFlashVersion(string ic, string expectedToken)
    {
        byte[] image = File.ReadAllBytes(RepositoryPaths.FromRepositoryRoot(
            "testdata",
            "golden",
            "standard-merge-gen-flash",
            "inputs",
            ic,
            "dp.bin"));

        Assert.True(GenFlashVersionCatalog.TryReadDpVersion(
            $"NT{ic}",
            image,
            out GenFlashDpVersionMetadata metadata));

        Assert.Equal(expectedToken, metadata.VersionToken);
        Assert.Equal($"D{expectedToken}", metadata.DisplayVersion);
    }

    /// <summary>ICs without gen_flash DP version evidence stay unclassified instead of guessing offsets.</summary>
    [Theory]
    [InlineData("NT51930")]
    [InlineData("NT51950")]
    [InlineData("NT51951")]
    public void MissingDpVersionEvidenceDoesNotCreateRule(string ic)
    {
        Assert.False(GenFlashVersionCatalog.TryGetDpVersionRule(ic, out _));
    }
}
