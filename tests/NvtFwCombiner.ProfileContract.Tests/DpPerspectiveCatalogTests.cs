using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests the shared NT51950/NT51951 DP Perspective policy catalog.</summary>
public sealed class DpPerspectiveCatalogTests
{
    /// <summary>Supported lengths are the single authoritative DP Perspective length list.</summary>
    [Fact]
    public void SupportedLengthsAndRangesAreSharedByV2MergeAndReplace()
    {
        Assert.Equal([0x40000, 0x80000, 0x100000], DpPerspectiveCatalog.SupportedContainerLengths);
        Assert.Equal(["NT51950", "NT51951"], DpPerspectiveCatalog.SupportedIcIds);
        Assert.Equal("NT51950/NT51951", DpPerspectiveCatalog.FormatSupportedIcIds());
        Assert.Equal("0x40000 / 0x80000 / 0x100000", DpPerspectiveCatalog.FormatSupportedLengths());
        Assert.Equal("0x0A000-0x36FFF (len 0x2D000)", DpPerspectiveCatalog.FormatRange(DpPerspectiveCatalog.TpOverlayRange));
        Assert.Equal("0x37000-0x37FFF (len 0x1000)", DpPerspectiveCatalog.FormatRange(DpPerspectiveCatalog.CustomerInfoRange));
        Assert.Equal("replace-dp-container", DpPerspectiveCatalog.ReplaceDpContainerOperationId);
        Assert.Equal(100, DpPerspectiveCatalog.ReplaceDpContainerSequence);
        Assert.Equal("restore-base-tp", DpPerspectiveCatalog.RestoreBaseTpOperationId);
        Assert.Equal(200, DpPerspectiveCatalog.RestoreBaseTpSequence);
    }

    /// <summary>Only the owner-approved 950/951 IC ids normalize as DP Perspective ICs.</summary>
    [Theory]
    [InlineData("51950", "NT51950", "51950")]
    [InlineData("nt51951", "NT51951", "51951")]
    public void NormalizeDpPerspectiveIcIds(string input, string expectedIcId, string expectedNumber)
    {
        Assert.Equal(expectedIcId, DpPerspectiveCatalog.NormalizeIcId(input));
        Assert.Equal(expectedNumber, DpPerspectiveCatalog.NormalizeIcNumber(input));
    }
}
