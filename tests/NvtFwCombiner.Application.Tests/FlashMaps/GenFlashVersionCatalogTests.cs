using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests.FlashMaps;

/// <summary>Golden-backed checks for gen_flash contiguous DP main/sub version-byte rules.</summary>
public sealed class GenFlashVersionCatalogTests
{
    /// <summary>Reads contiguous DP main/sub version bytes from owner-approved gen_flash standard-merge DP inputs.</summary>
    [Theory]
    [InlineData("51920", "0101")]
    [InlineData("51923", "8100")]
    [InlineData("51926", "0102")]
    [InlineData("51927", "5401")]
    [InlineData("51928", "8211")]
    [InlineData("51929", "0200")]
    [InlineData("51931", "8D60")]
    [InlineData("51932", "8201")]
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

    /// <summary>Rejects truncated payloads that expose only the main DP version byte.</summary>
    [Fact]
    public void DpVersionRequiresContiguousMainAndSubBytes()
    {
        byte[] image = new byte[0x68];
        image[0x67] = 0x12;

        Assert.False(GenFlashVersionCatalog.TryReadDpVersion(
            "NT51929",
            image,
            out _));
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
