using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests the centralized IC onboarding support catalog.</summary>
public sealed class IcSupportCatalogTests
{
    private static readonly string[] KnownWorkflowIds =
    [
        IcWorkflowIds.StandardMerge,
        IcWorkflowIds.DpReplace,
        IcWorkflowIds.CtrlRamReplace,
        IcWorkflowIds.GeneralMerge,
        IcWorkflowIds.GeneralReplace,
    ];

    /// <summary>IC onboarding rows are unique and use only documented workflow ids.</summary>
    [Fact]
    public void IcSupportRowsAreUniqueAndUseKnownWorkflowIds()
    {
        Assert.Equal(
            IcSupportCatalog.All.Count,
            IcSupportCatalog.All.Select(entry => entry.IcId).Distinct(StringComparer.Ordinal).Count());

        foreach (IcSupportEntry entry in IcSupportCatalog.All)
        {
            Assert.NotEmpty(entry.WorkflowIds);
            Assert.Equal(
                entry.WorkflowIds.Count,
                entry.WorkflowIds.Distinct(StringComparer.Ordinal).Count());
            Assert.All(entry.WorkflowIds, workflowId =>
                Assert.Contains(workflowId, KnownWorkflowIds));
        }
    }

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

    /// <summary>Alias facts point to supported IC rows instead of orphan source ids.</summary>
    [Fact]
    public void AliasFactsPointToSupportedIcRows()
    {
        HashSet<string> supportedIcIds = [.. IcSupportCatalog.IcIds];
        HashSet<string> standardMergeProfileIcIds =
        [
            .. BuiltInStandardMergeProfiles.ExecutableStandardMergeProfiles
                .Select(profile => profile.IcId),
        ];

        foreach (IcSupportEntry entry in IcSupportCatalog.All)
        {
            if (entry.StandardMergeSourceIcId is not null)
            {
                Assert.Contains(entry.StandardMergeSourceIcId, supportedIcIds);
                Assert.Contains(entry.StandardMergeSourceIcId, standardMergeProfileIcIds);
                Assert.NotEqual(entry.IcId, entry.StandardMergeSourceIcId);
            }

            if (entry.CtrlRamPostbuildSourceIcId is not null)
            {
                Assert.Contains(entry.CtrlRamPostbuildSourceIcId, supportedIcIds);
                Assert.NotEqual(entry.IcId, entry.CtrlRamPostbuildSourceIcId);
            }
        }
    }
}
