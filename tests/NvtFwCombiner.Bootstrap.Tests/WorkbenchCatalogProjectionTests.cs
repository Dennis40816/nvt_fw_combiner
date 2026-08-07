using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Convergence tests for stable workbench catalog and profile projections.</summary>
public sealed class WorkbenchCatalogProjectionTests
{
    /// <summary>Workbench IC and selector projections preserve catalog order, defaults, and display choices.</summary>
    [Fact]
    public void IcCatalogProjectionPreservesSelectableRowsAndChoices()
    {
        IReadOnlyList<string> icIds = CanonicalCapabilityProjection.GetIcIds();

        Assert.Equal(10, icIds.Count);
        Assert.Equal(icIds.Order(StringComparer.Ordinal), icIds);
        Assert.Equal("NT51950", CanonicalCapabilityProjection.DefaultIcId);
        Assert.Equal(
            CanonicalCapabilityProjection.GetNumberSelectionChoices("NT51926"),
            CanonicalCapabilityProjection.GetNumberSelectionChoices("51926"));
        foreach (string icId in icIds)
        {
            IReadOnlyList<LegacyCombinerPostbuildProfile> profiles = LegacyCombinerPostbuildCatalog.GetProfiles(icId);
            Assert.Equal(
                IcNumberChoicePolicy.GetNumberSelectionChoices(profiles).Select(static choice => (
                    choice.Token,
                    choice.DisplayLabel)),
                CanonicalCapabilityProjection.GetNumberSelectionChoices(icId).Select(static choice => (
                    choice.Token,
                    choice.DisplayLabel)));
        }
    }

    /// <summary>Public catalog projections cannot mutate the cached selectable IC or number choices.</summary>
    [Fact]
    public void CatalogProjectionsRejectMutation()
    {
        IReadOnlyList<string> supportedIcIds = CanonicalCapabilityProjection.GetIcIds();
        IReadOnlyList<CapabilityNumberChoice> numberChoices =
            CanonicalCapabilityProjection.GetNumberSelectionChoices("NT51950");
        string originalIcId = supportedIcIds[0];
        CapabilityNumberChoice originalNumberChoice = numberChoices[0];

        var mutableIcIds = (IList<string>)supportedIcIds;
        var mutableNumberChoices = (IList<CapabilityNumberChoice>)numberChoices;
        Assert.True(mutableIcIds.IsReadOnly);
        Assert.True(mutableNumberChoices.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => mutableIcIds[0] = "NT00000");
        _ = Assert.Throws<NotSupportedException>(() =>
            mutableNumberChoices[0] = new CapabilityNumberChoice("invalid", "Invalid"));

        Assert.Equal(originalIcId, CanonicalCapabilityProjection.GetIcIds()[0]);
        Assert.Equal(
            originalNumberChoice,
            CanonicalCapabilityProjection.GetNumberSelectionChoices("NT51950")[0]);
    }

    /// <summary>Retired ICs are absent from every production selector and compiled profile summary.</summary>
    [Theory]
    [InlineData("NT51920")]
    [InlineData("NT51925")]
    [InlineData("NT51930")]
    [InlineData("NT51931")]
    public void RetiredIcIdsAreNotProjectedByWorkbenchCatalogs(string icId)
    {
        Assert.DoesNotContain(icId, CanonicalCapabilityProjection.GetIcIds());
        Assert.DoesNotContain(
            CanonicalCapabilityProjection.GetStandardMergeProfileSummaries(),
            summary => StringComparer.Ordinal.Equals(summary.IcId, icId));
        Assert.DoesNotContain(
            CanonicalCapabilityProjection.GetDpReplaceProfileSummaries(),
            summary => StringComparer.Ordinal.Equals(summary.IcId, icId));
    }

    /// <summary>Replace summaries expose only manifest-pinned V2 runtime profiles.</summary>
    [Fact]
    public void ProfileSummariesExcludeSyntheticCompilerFixtures()
    {
        IReadOnlyList<CapabilityProfileSummary> standardSummaries =
            CanonicalCapabilityProjection.GetStandardMergeProfileSummaries();
        IReadOnlyList<CapabilityProfileSummary> replaceSummaries =
            CanonicalCapabilityProjection.GetDpReplaceProfileSummaries();
        AssertStandardMergeProfileSummaries(standardSummaries);
        AssertV2DpReplaceProfileSummaries(replaceSummaries);
        Assert.DoesNotContain(replaceSummaries, static summary => summary.IcId == "NT-SYNTHETIC");

        CapabilityCatalogSummary settings = CanonicalCapabilityProjection.GetCatalogSummary();
        Assert.Equal(10, settings.CatalogIcCount);
        Assert.Equal(standardSummaries.Count, settings.StandardMergeProfileCount);
        Assert.Equal(10, settings.DpReplaceProfileCount);
        Assert.Equal(10, settings.CtrlRamReplaceAvailableIcCount);
    }

    private static void AssertStandardMergeProfileSummaries(
        IReadOnlyList<CapabilityProfileSummary> summaries)
    {
        Assert.Equal(
            [
                "NT51917", "NT51919", "NT51923", "NT51926", "NT51927", "NT51928",
                "NT51929", "NT51932", "NT51950", "NT51951",
            ],
            summaries.Select(static summary => summary.IcId).Order(StringComparer.Ordinal));

        foreach (CapabilityProfileSummary summary in summaries)
        {
            long? dpLength = summary.IcId is "NT51950" or "NT51951" ? 0x40000 : null;
            Assert.True(
                CanonicalCapabilityResolution.TryCompileStandardMerge(
                    summary.IcId,
                    dpLength,
                    out CompiledComposition? composition,
                    out IReadOnlyList<CompositionIssue> issues),
                string.Join(Environment.NewLine, issues.Select(static issue => $"{issue.Code}: {issue.Message}")));

            Assert.True(summary.CompileSucceeded);
            Assert.Empty(summary.IssueCodes);
            Assert.Equal(composition.V2Details.ProfileId, summary.ProfileId);
            Assert.Equal(composition.V2Details.CompositionKind, summary.CompositionKind);
            Assert.Equal(composition.Plan.RequiredInputAddressSpaceIds, summary.RequiredInputAddressSpaceIds);
            Assert.Equal(composition.V2Details.OutputNamingRequirement.FileNameTemplate, summary.DefaultOutputFileName);
            Assert.Equal(composition.V2Details.IcNumberInputMode, summary.IcNumberInputMode);
        }
    }

    private static void AssertV2DpReplaceProfileSummaries(
        IReadOnlyList<CapabilityProfileSummary> summaries)
    {
        Assert.Equal(
            [
                "NT51917", "NT51919", "NT51923", "NT51926", "NT51927", "NT51928",
                "NT51929", "NT51932", "NT51950", "NT51951",
            ],
            summaries.Select(static summary => summary.IcId).Order(StringComparer.Ordinal));

        foreach (CapabilityProfileSummary summary in summaries)
        {
            long baseCapacity = summary.IcId == "NT51928" ? 0x80000 : 0x40000;
            Assert.True(
                CanonicalCapabilityResolution.TryCompileDpReplace(
                    summary.IcId,
                    baseCapacity,
                    out CompiledComposition? composition,
                    out IReadOnlyList<CompositionIssue> issues),
                string.Join(Environment.NewLine, issues.Select(static issue => $"{issue.Code}: {issue.Message}")));

            CompiledComposition artifact = Assert.IsType<CompiledComposition>(composition);
            Assert.True(summary.CompileSucceeded);
            Assert.Empty(summary.IssueCodes);
            Assert.Equal(artifact.V2Details.ProfileId, summary.ProfileId);
            Assert.Equal(artifact.V2Details.CompositionKind, summary.CompositionKind);
            Assert.Equal(artifact.Plan.RequiredInputAddressSpaceIds, summary.RequiredInputAddressSpaceIds);
            Assert.Equal(artifact.V2Details.OutputNamingRequirement.FileNameTemplate, summary.DefaultOutputFileName);
            Assert.Equal(artifact.V2Details.IcNumberInputMode, summary.IcNumberInputMode);
        }
    }
}
