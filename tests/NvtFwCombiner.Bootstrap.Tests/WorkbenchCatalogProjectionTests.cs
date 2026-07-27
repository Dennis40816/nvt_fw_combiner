using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Convergence tests for stable workbench catalog and profile projections.</summary>
public sealed class WorkbenchCatalogProjectionTests
{
    /// <summary>Workbench selectors hide retired 0.9.17 rows without deleting their internal catalog facts.</summary>
    [Fact]
    public void IcCatalogProjectionPreservesSelectableRowsAndChoices()
    {
        IReadOnlyList<string> icIds = WorkbenchCompositionService.GetSupportedIcIds();

        Assert.Equal(10, icIds.Count);
        Assert.Equal(
            IcSupportCatalog.IcIds.Where(static icId =>
                icId is not ("NT51920" or "NT51925" or "NT51930" or "NT51931")),
            icIds);
        Assert.DoesNotContain("NT51920", icIds);
        Assert.DoesNotContain("NT51925", icIds);
        Assert.DoesNotContain("NT51930", icIds);
        Assert.DoesNotContain("NT51931", icIds);
        Assert.True(IcSupportCatalog.TryFind("NT51920", out _));
        Assert.True(IcSupportCatalog.TryFind("NT51930", out _));
        Assert.True(IcSupportCatalog.TryFind("NT51931", out _));
        Assert.Equal("NT51950", WorkbenchCompositionService.GetDefaultIcId());
        Assert.Equal(
            WorkbenchCompositionService.GetNumberSelectionChoices("NT51926"),
            WorkbenchCompositionService.GetNumberSelectionChoices("51926"));
        foreach (string icId in icIds)
        {
            IReadOnlyList<LegacyCombinerPostbuildProfile> profiles = LegacyCombinerPostbuildCatalog.GetProfiles(icId);
            Assert.Equal(
                IcNumberChoicePolicy.GetNumberSelectionChoices(profiles).Select(static choice => (
                    choice.Token,
                    choice.DisplayLabel)),
                WorkbenchCompositionService.GetNumberSelectionChoices(icId).Select(static choice => (
                    choice.Token,
                    choice.DisplayLabel)));
        }
    }

    /// <summary>Public catalog projections cannot mutate the cached selectable IC or number choices.</summary>
    [Fact]
    public void CatalogProjectionsRejectMutation()
    {
        IReadOnlyList<string> supportedIcIds = WorkbenchCompositionService.GetSupportedIcIds();
        IReadOnlyList<WorkbenchIcNumberChoice> numberChoices =
            WorkbenchCompositionService.GetNumberSelectionChoices("NT51950");
        string originalIcId = supportedIcIds[0];
        WorkbenchIcNumberChoice originalNumberChoice = numberChoices[0];

        var mutableIcIds = (IList<string>)supportedIcIds;
        var mutableNumberChoices = (IList<WorkbenchIcNumberChoice>)numberChoices;
        Assert.True(mutableIcIds.IsReadOnly);
        Assert.True(mutableNumberChoices.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => mutableIcIds[0] = "NT00000");
        _ = Assert.Throws<NotSupportedException>(() =>
            mutableNumberChoices[0] = new WorkbenchIcNumberChoice("invalid", "Invalid"));

        Assert.Equal(originalIcId, WorkbenchCompositionService.GetSupportedIcIds()[0]);
        Assert.Equal(originalNumberChoice, WorkbenchCompositionService.GetNumberSelectionChoices("NT51950")[0]);
    }

    /// <summary>Replace summaries expose only manifest-pinned V2 runtime profiles.</summary>
    [Fact]
    public void ProfileSummariesExcludeSyntheticCompilerFixtures()
    {
        IReadOnlyList<WorkbenchProfileSummary> standardSummaries = WorkbenchCompositionService.GetStandardMergeProfileSummaries();
        IReadOnlyList<WorkbenchProfileSummary> replaceSummaries = WorkbenchCompositionService.GetReplaceProfileSummaries();
        AssertStandardMergeProfileSummaries(standardSummaries);
        AssertV2DpReplaceProfileSummaries(replaceSummaries);
        Assert.DoesNotContain(replaceSummaries, static summary => summary.IcId == "NT-SYNTHETIC");

        WorkbenchSettingsSnapshot settings = WorkbenchCompositionService.GetSettingsSnapshot();
        Assert.Equal(13, settings.CatalogIcCount);
        Assert.Equal(standardSummaries.Count, settings.StandardMergeProfileCount);
        Assert.Equal(13, settings.DpReplaceProfileCount);
        Assert.Equal(13, settings.CtrlRamReplaceAvailableIcCount);
    }

    private static void AssertStandardMergeProfileSummaries(IReadOnlyList<WorkbenchProfileSummary> summaries)
    {
        Assert.Equal(
            [
                "NT51917", "NT51919", "NT51920", "NT51923", "NT51926", "NT51927", "NT51928",
                "NT51929", "NT51930", "NT51931", "NT51932", "NT51950", "NT51951",
            ],
            summaries.Select(static summary => summary.IcId).Order(StringComparer.Ordinal));

        foreach (WorkbenchProfileSummary summary in summaries)
        {
            long? dpLength = summary.IcId is "NT51950" or "NT51951" ? 0x40000 : null;
            Assert.True(
                WorkbenchCompositionService.TryCompileStandardMerge(
                    summary.IcId,
                    dpLength,
                    out CompiledComposition? composition,
                    out IReadOnlyList<CompositionIssue> issues),
                string.Join(Environment.NewLine, issues.Select(static issue => $"{issue.Code}: {issue.Message}")));

            Assert.True(summary.CompileSucceeded);
            Assert.Empty(summary.IssueCodes);
            Assert.Equal(composition.ProfileId, summary.ProfileId);
            Assert.Equal(composition.CompositionKind, summary.CompositionKind);
            Assert.Equal(composition.Plan.RequiredInputAddressSpaceIds, summary.RequiredInputAddressSpaceIds);
            Assert.Equal(composition.DefaultOutputFileName, summary.DefaultOutputFileName);
            Assert.Equal(composition.IcNumberPolicy, summary.IcNumberPolicy);
        }
    }

    private static void AssertV2DpReplaceProfileSummaries(IReadOnlyList<WorkbenchProfileSummary> summaries)
    {
        Assert.Equal(
            [
                "NT51917", "NT51919", "NT51920", "NT51923", "NT51926", "NT51927", "NT51928",
                "NT51929", "NT51930", "NT51931", "NT51932", "NT51950", "NT51951",
            ],
            summaries.Select(static summary => summary.IcId).Order(StringComparer.Ordinal));

        foreach (WorkbenchProfileSummary summary in summaries)
        {
            long baseCapacity = summary.IcId == "NT51928" ? 0x80000 : 0x40000;
            Assert.True(
                WorkbenchCompositionService.TryCompileBuiltInV2DpReplace(
                    summary.IcId,
                    baseCapacity,
                    out CompiledComposition? composition,
                    out IReadOnlyList<CompositionIssue> issues),
                string.Join(Environment.NewLine, issues.Select(static issue => $"{issue.Code}: {issue.Message}")));

            CompiledComposition artifact = Assert.IsType<CompiledComposition>(composition);
            Assert.True(summary.CompileSucceeded);
            Assert.Empty(summary.IssueCodes);
            Assert.Equal(artifact.ProfileId, summary.ProfileId);
            Assert.Equal(artifact.CompositionKind, summary.CompositionKind);
            Assert.Equal(artifact.Plan.RequiredInputAddressSpaceIds, summary.RequiredInputAddressSpaceIds);
            Assert.Equal(artifact.DefaultOutputFileName, summary.DefaultOutputFileName);
            Assert.Equal(artifact.IcNumberPolicy, summary.IcNumberPolicy);
        }
    }
}
