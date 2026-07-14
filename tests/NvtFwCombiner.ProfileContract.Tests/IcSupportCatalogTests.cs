using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests the centralized IC onboarding support catalog.</summary>
public sealed class IcSupportCatalogTests
{
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
                Assert.Contains(workflowId, IcWorkflowIds.All));
        }
    }

    /// <summary>The workflow id list is owned by production catalog code instead of test-only constants.</summary>
    [Fact]
    public void KnownWorkflowIdsAreCatalogOwned()
    {
        Assert.Equal(
            [
                "standard-merge",
                "dp-replace",
                "ctrlram-replace",
                "general-merge",
                "general-replace",
            ],
            IcWorkflowIds.All);
        Assert.All(IcWorkflowIds.All, workflowId => Assert.True(IcWorkflowIds.IsKnown(workflowId), workflowId));
    }

    /// <summary>Every IC workflow id maps to a runtime experience descriptor.</summary>
    [Fact]
    public void KnownWorkflowIdsExistInDomainExperienceCatalog()
    {
        HashSet<string> domainExperienceIds =
        [
            .. ExperienceCatalog.All.Select(experience => experience.ExperienceId),
        ];

        Assert.All(IcWorkflowIds.All, workflowId => Assert.Contains(workflowId, domainExperienceIds));
    }

    /// <summary>Unknown or empty workflow declarations fail before an invalid IC row can be surfaced.</summary>
    [Fact]
    public void IcSupportEntryRejectsInvalidWorkflowDeclarations()
    {
        ArgumentException empty = Assert.Throws<ArgumentException>(() =>
            new IcSupportEntry("NT51999", []));
        ArgumentException unknown = Assert.Throws<ArgumentException>(() =>
            new IcSupportEntry("NT51999", ["unsupported-workflow"]));

        Assert.Contains("At least one supported workflow id", empty.Message, StringComparison.Ordinal);
        Assert.Contains("Unknown IC workflow id 'unsupported-workflow'", unknown.Message, StringComparison.Ordinal);
    }

    /// <summary>The shell default IC is a catalog-owned onboarding decision, not a UI constant.</summary>
    [Fact]
    public void DefaultIcIdIsSupportedByCatalog()
    {
        Assert.Equal("NT51950", IcSupportCatalog.DefaultIcId);
        Assert.True(IcSupportCatalog.TryFind(IcSupportCatalog.DefaultIcId, out IcSupportEntry? entry));
        Assert.NotNull(entry);
        Assert.True(entry.SupportsWorkflow(IcWorkflowIds.StandardMerge));
        Assert.True(entry.SupportsWorkflow(IcWorkflowIds.GeneralMerge));
        Assert.True(entry.SupportsWorkflow(IcWorkflowIds.GeneralReplace));
    }

    /// <summary>Every production Standard Merge registration has an onboarding entry.</summary>
    [Fact]
    public void StandardMergeCatalogContainsEveryRegisteredIc()
    {
        string[] supportedIcIds = [.. IcSupportCatalog.IcIds.Order(StringComparer.Ordinal)];
        string[] expectedStandardMergeIcIds =
        [
            "NT51917",
            "NT51919",
            "NT51920",
            "NT51923",
            "NT51926",
            "NT51927",
            "NT51928",
            "NT51929",
            "NT51930",
            "NT51931",
            "NT51932",
            "NT51950",
            "NT51951",
        ];

        Assert.Equal(expectedStandardMergeIcIds, supportedIcIds);
        Assert.All(expectedStandardMergeIcIds, icId =>
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
        HashSet<string> standardMergeIcIds =
        [
            .. IcSupportCatalog.All
                .Where(entry => entry.SupportsWorkflow(IcWorkflowIds.StandardMerge))
                .Select(entry => entry.IcId),
        ];

        foreach (IcSupportEntry entry in IcSupportCatalog.All)
        {
            if (entry.StandardMergeSourceIcId is not null)
            {
                Assert.Contains(entry.StandardMergeSourceIcId, supportedIcIds);
                Assert.Contains(entry.StandardMergeSourceIcId, standardMergeIcIds);
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
