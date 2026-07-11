using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests the shared NT51950/NT51951 DP Perspective policy catalog.</summary>
public sealed class DpPerspectiveCatalogTests
{
    /// <summary>Supported lengths are the single authoritative DP Perspective length list.</summary>
    [Fact]
    public void SupportedLengthsAreSharedByMergeAndReplace()
    {
        Assert.Equal([0x40000, 0x80000, 0x100000], DpPerspectiveCatalog.SupportedContainerLengths);
        Assert.Equal(0x100000, DpPerspectiveCatalog.MaxContainerLength);
        Assert.Equal(["NT51950", "NT51951"], DpPerspectiveCatalog.SupportedIcIds);
        Assert.Equal("NT51950/NT51951", DpPerspectiveCatalog.FormatSupportedIcIds());
        Assert.Equal("0x40000 / 0x80000 / 0x100000", DpPerspectiveCatalog.FormatSupportedLengths());
        Assert.Equal("0x0A000-0x36FFF (len 0x2D000)", DpPerspectiveCatalog.FormatRange(DpPerspectiveCatalog.TpOverlayRange));
        Assert.Equal("0x37000-0x37FFF (len 0x1000)", DpPerspectiveCatalog.FormatRange(DpPerspectiveCatalog.CustomerInfoPreserveRange));
        Assert.Equal("dp-perspective-container", DpPerspectiveCatalog.ContainerRegionId);
        Assert.Equal("copy-dp-container", DpPerspectiveCatalog.CopyDpContainerOperationId);
        Assert.Equal(100, DpPerspectiveCatalog.CopyDpContainerSequence);
        Assert.Equal("overlay-tp", DpPerspectiveCatalog.OverlayTpOperationId);
        Assert.Equal(200, DpPerspectiveCatalog.OverlayTpSequence);
        Assert.Equal("replace-dp-container", DpPerspectiveCatalog.ReplaceDpContainerOperationId);
        Assert.Equal(100, DpPerspectiveCatalog.ReplaceDpContainerSequence);
        Assert.Equal("restore-base-tp", DpPerspectiveCatalog.RestoreBaseTpOperationId);
        Assert.Equal(200, DpPerspectiveCatalog.RestoreBaseTpSequence);
        Assert.Equal("restore-base-customer-info", DpPerspectiveCatalog.RestoreBaseCustomerInfoOperationId);
        Assert.Equal(210, DpPerspectiveCatalog.RestoreBaseCustomerInfoSequence);

        Assert.Equal(
            DpPerspectiveCatalog.SupportedContainerLengths,
            BuiltInReplaceProfiles.DpPerspectiveSupportedDpBaseLengths);
        Assert.Equal(
            DpPerspectiveCatalog.SupportedIcIds,
            BuiltInStandardMergeProfiles.DpPerspectiveStandardMergeProfiles.Select(profile => profile.IcId));
        Assert.Equal(
            DpPerspectiveCatalog.SupportedIcIds,
            BuiltInReplaceProfiles.DpPerspectiveDpReplaceProfiles.Select(profile => profile.IcId));
    }

    /// <summary>TP overlay and customer-info preserve ranges are shared by Standard Merge and DP Replace profiles.</summary>
    [Theory]
    [InlineData("NT51950", 0x40000)]
    [InlineData("NT51951", 0x80000)]
    public void ProfilesUseSharedDpPerspectiveRanges(string icId, long length)
    {
        CompositionProfileDefinition merge = BuiltInStandardMergeProfiles.CreateDpPerspectiveProfileForInputLength(icId, length);
        CompositionProfileDefinition replace = BuiltInReplaceProfiles.CreateDpPerspectiveDpReplaceProfile(icId, length);

        ProfileCompileResult mergeCompile = CompositionProfileCompiler.Compile(merge, []);
        ProfileCompileResult replaceCompile = CompositionProfileCompiler.Compile(replace, []);

        Assert.True(mergeCompile.IsSuccess, FormatIssues(mergeCompile.Issues));
        Assert.True(replaceCompile.IsSuccess, FormatIssues(replaceCompile.Issues));
        Assert.Contains(merge.AddressSpaces, space =>
            space.AddressSpaceId == "tp-input" &&
            space.Length == DpPerspectiveCatalog.TpInputLength);
        Assert.Contains(mergeCompile.Plan!.OrderedOperations, operation =>
            operation.OperationId == DpPerspectiveCatalog.OverlayTpOperationId &&
            operation.TargetRange == DpPerspectiveCatalog.TpOverlayRange);
        Assert.Contains(replaceCompile.Plan!.OrderedOperations, operation =>
            operation.OperationId == DpPerspectiveCatalog.RestoreBaseTpOperationId &&
            operation.TargetRange == DpPerspectiveCatalog.TpOverlayRange);
        Assert.Contains(replaceCompile.Plan.OrderedOperations, operation =>
            operation.OperationId == DpPerspectiveCatalog.RestoreBaseCustomerInfoOperationId &&
            operation.TargetRange == DpPerspectiveCatalog.CustomerInfoPreserveRange);
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

    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(issue => $"{issue.Code}: {issue.Message}"));
    }
}
