using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests the centralized IC onboarding support catalog.</summary>
public sealed class IcSupportCatalogTests
{
    /// <summary>IC identifiers have one catalog-owned canonical representation.</summary>
    [Theory]
    [InlineData("51950", "NT51950")]
    [InlineData("nt51950", "NT51950")]
    [InlineData(" NT51950 ", "NT51950")]
    public void IcIdentifierNormalizationIsCatalogOwned(string value, string expected)
    {
        Assert.Equal(expected, IcSupportCatalog.NormalizeIcId(value));
    }

    /// <summary>Missing IC identifiers fail before a canonical token can be produced.</summary>
    [Fact]
    public void IcIdentifierNormalizationRejectsMissingValues()
    {
        _ = Assert.Throws<ArgumentNullException>(() => IcSupportCatalog.NormalizeIcId(null!));
        _ = Assert.Throws<ArgumentException>(() => IcSupportCatalog.NormalizeIcId(" "));
    }

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

    /// <summary>Golden-verified rows cannot promise evidence for a workflow they do not expose.</summary>
    [Fact]
    public void IcSupportEntryRejectsGoldenEvidenceForUnavailableWorkflow()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            new IcSupportEntry(
                "NT51999",
                [IcWorkflowIds.StandardMerge],
                goldenVerifiedWorkflowIds: [IcWorkflowIds.DpReplace]));

        Assert.Contains("must also be exposed", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>DP Replace cannot exist without the same IC's canonical Standard Merge map.</summary>
    [Fact]
    public void DpReplaceRequiresStandardMerge()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            new IcSupportEntry("NT51999", [IcWorkflowIds.DpReplace]));

        Assert.Contains("canonical Standard Merge map", exception.Message, StringComparison.Ordinal);
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

    /// <summary>DP Replace exposure follows a canonical Standard Merge map; evidence readiness remains separate.</summary>
    [Fact]
    public void DpReplaceExposureRequiresCanonicalStandardMergeMap()
    {
        string[] dpReplaceIcIds =
        [
            .. IcSupportCatalog.All
                .Where(entry => entry.SupportsWorkflow(IcWorkflowIds.DpReplace))
                .Select(entry => entry.IcId)
                .Order(StringComparer.Ordinal),
        ];

        Assert.Equal(
            IcSupportCatalog.IcIds.Order(StringComparer.Ordinal),
            dpReplaceIcIds);
        Assert.All(dpReplaceIcIds, icId =>
            Assert.True(IcSupportCatalog.SupportsWorkflow(icId, IcWorkflowIds.StandardMerge), icId));
    }

    /// <summary>NT51931 exposes canonical DP and CtrlRAM Replace while General Replace remains closed.</summary>
    [Fact]
    public void Nt51931DpAndCtrlRamReplaceAreExposed()
    {
        Assert.True(IcSupportCatalog.TryFind("NT51931", out IcSupportEntry? entry));
        Assert.True(entry!.SupportsWorkflow(IcWorkflowIds.StandardMerge));
        Assert.True(entry.SupportsWorkflow(IcWorkflowIds.GeneralMerge));
        Assert.True(entry.SupportsWorkflow(IcWorkflowIds.DpReplace));
        Assert.True(entry.SupportsWorkflow(IcWorkflowIds.CtrlRamReplace));
        Assert.False(entry.SupportsWorkflow(IcWorkflowIds.GeneralReplace));
        Assert.Contains("canonical 256 KiB", entry.Notes, StringComparison.Ordinal);
        Assert.Contains("support-neutral", entry.Notes, StringComparison.Ordinal);
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

    /// <summary>Reply.md perfect and partial IC families are explicit typed catalog facts.</summary>
    [Theory]
    [InlineData("NT51927", "nt51927-family", "NT51927", IcFamilyRelationship.Canonical)]
    [InlineData("NT51917", "nt51927-family", "NT51927", IcFamilyRelationship.PerfectAlias)]
    [InlineData("NT51928", "nt51927-family", "NT51927", IcFamilyRelationship.PartialAlias)]
    [InlineData("NT51929", "nt51929-nt51932-family", "NT51929", IcFamilyRelationship.Canonical)]
    [InlineData("NT51919", "nt51929-nt51932-family", "NT51929", IcFamilyRelationship.PerfectAlias)]
    [InlineData("NT51932", "nt51929-nt51932-family", "NT51929", IcFamilyRelationship.PerfectAlias)]
    public void OwnerDeclaredIcFamiliesAreTyped(
        string icId,
        string familyId,
        string familySourceIcId,
        IcFamilyRelationship relationship)
    {
        Assert.True(IcSupportCatalog.TryFind(icId, out IcSupportEntry? entry));

        Assert.Equal(familyId, entry!.FamilyId);
        Assert.Equal(familySourceIcId, entry.FamilySourceIcId);
        Assert.Equal(relationship, entry.FamilyRelationship);
        Assert.False(string.IsNullOrWhiteSpace(entry.FamilyScope));
    }

    /// <summary>Golden readiness is independent from authoring availability and product support.</summary>
    [Fact]
    public void WorkflowEvidenceStatusDoesNotBanEvidenceGatedAuthoring()
    {
        Assert.True(IcSupportCatalog.TryFind("NT51932", out IcSupportEntry? nt51932));
        Assert.Equal(
            IcWorkflowEvidenceStatus.GoldenVerified,
            nt51932!.GetWorkflowEvidenceStatus(IcWorkflowIds.StandardMerge));
        Assert.Equal(
            IcWorkflowEvidenceStatus.EvidenceGated,
            nt51932.GetWorkflowEvidenceStatus(IcWorkflowIds.CtrlRamReplace));
        Assert.Equal(
            IcWorkflowEvidenceStatus.EvidenceGated,
            nt51932.GetWorkflowEvidenceStatus(IcWorkflowIds.DpReplace));

        Assert.True(IcSupportCatalog.TryFind("NT51950", out IcSupportEntry? nt51950));
        Assert.Equal(
            IcWorkflowEvidenceStatus.GoldenVerified,
            nt51950!.GetWorkflowEvidenceStatus(IcWorkflowIds.DpReplace));

        Assert.True(IcSupportCatalog.TryFind("NT51930", out IcSupportEntry? nt51930));
        Assert.Equal(
            IcWorkflowEvidenceStatus.EvidenceGated,
            nt51930!.GetWorkflowEvidenceStatus(IcWorkflowIds.DpReplace));
    }
}
