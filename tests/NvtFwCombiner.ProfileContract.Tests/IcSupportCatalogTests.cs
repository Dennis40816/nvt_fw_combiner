using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests the centralized IC onboarding support catalog.</summary>
public sealed class IcSupportCatalogTests
{
    /// <summary>Every executable Standard Merge IC has an onboarding entry.</summary>
    [Fact]
    public void StandardMergeProfilesAreCoveredByIcSupportCatalog()
    {
        string[] supportedIcIds = [.. IcSupportCatalog.IcIds.Order(StringComparer.Ordinal)];
        string[] standardMergeIcIds =
        [
            .. BuiltInStandardMergeProfiles.ExecutableStandardMergeProfiles
                .Select(profile => profile.IcId)
                .Order(StringComparer.Ordinal),
        ];

        Assert.Equal(standardMergeIcIds, supportedIcIds);
        Assert.All(standardMergeIcIds, icId =>
            Assert.True(IcSupportCatalog.SupportsWorkflow(icId, IcWorkflowIds.StandardMerge), icId));
    }

    /// <summary>Only NT51950/NT51951 currently expose DP Perspective DP Replace.</summary>
    [Fact]
    public void DpReplaceExposureIsLimitedToDpPerspectiveIcs()
    {
        string[] dpReplaceIcIds =
        [
            .. IcSupportCatalog.All
                .Where(entry => entry.SupportsWorkflow(IcWorkflowIds.DpReplace))
                .Select(entry => entry.IcId)
                .Order(StringComparer.Ordinal),
        ];

        Assert.Equal(["NT51950", "NT51951"], dpReplaceIcIds);
        Assert.All(dpReplaceIcIds, icId =>
            Assert.True(DpPerspectiveCatalog.IsSupportedIc(icId), icId));
    }

    /// <summary>Alias facts are explicit instead of being hidden in separate profile tables.</summary>
    [Theory]
    [InlineData("NT51917", "NT51927", "NT51927")]
    [InlineData("NT51919", "NT51929", "NT51929")]
    [InlineData("NT51928", null, "NT51927")]
    [InlineData("NT51951", "NT51950", "NT51950")]
    public void AliasFactsAreDeclaredInOnePlace(
        string icId,
        string? standardMergeSource,
        string? ctrlRamPostbuildSource)
    {
        Assert.True(IcSupportCatalog.TryFind(icId, out IcSupportEntry? entry));

        Assert.Equal(standardMergeSource, entry!.StandardMergeSourceIcId);
        Assert.Equal(ctrlRamPostbuildSource, entry.CtrlRamPostbuildSourceIcId);
    }

}
