using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Convergence tests for stable workbench catalog and profile projections.</summary>
public sealed class WorkbenchCatalogProjectionTests
{
    /// <summary>Workbench IC and selector projections preserve catalog order, defaults, and display choices.</summary>
    [Fact]
    public void IcCatalogProjectionPreservesSelectableRowsAndChoices()
    {
        IReadOnlyList<string> icIds = WorkbenchCompositionService.GetSupportedIcIds();

        Assert.Equal(13, icIds.Count);
        Assert.Equal(IcSupportCatalog.IcIds, icIds);
        Assert.Equal("NT51950", WorkbenchCompositionService.GetDefaultIcId());
        foreach (string icId in icIds)
        {
            Assert.Equal(TpFlashMapCatalog.GetNumberChoices(icId), WorkbenchCompositionService.GetNumberChoices(icId));
            Assert.Equal(
                TpFlashMapCatalog.GetNumberSelectionChoices(icId).Select(static choice => (
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
        IReadOnlyList<string> numberChoices = WorkbenchCompositionService.GetNumberChoices("NT51950");
        string originalIcId = supportedIcIds[0];
        string originalNumberChoice = numberChoices[0];

        var mutableIcIds = (IList<string>)supportedIcIds;
        var mutableNumberChoices = (IList<string>)numberChoices;
        Assert.True(mutableIcIds.IsReadOnly);
        Assert.True(mutableNumberChoices.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => mutableIcIds[0] = "NT00000");
        _ = Assert.Throws<NotSupportedException>(() => mutableNumberChoices[0] = "invalid");

        Assert.Equal(originalIcId, WorkbenchCompositionService.GetSupportedIcIds()[0]);
        Assert.Equal(originalNumberChoice, WorkbenchCompositionService.GetNumberChoices("NT51950")[0]);
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
        Assert.Equal(13, settings.SupportedIcCount);
        Assert.Equal(standardSummaries.Count, settings.StandardMergeProfileCount);
        Assert.Equal(2, settings.ReplaceProfileCount);
        Assert.Equal(12, settings.CtrlRamReplaceIcCount);
        Assert.Equal(1, settings.ExternalToolBindingCount);
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
        WorkbenchProfileSummary[] dpReplaceSummaries =
        [
            .. summaries.Where(static summary => summary.ProfileId.EndsWith(
                "-dp-replace-dp-perspective",
                StringComparison.Ordinal)),
        ];
        Assert.Equal(["NT51950", "NT51951"], dpReplaceSummaries.Select(static summary => summary.IcId));

        foreach (WorkbenchProfileSummary summary in dpReplaceSummaries)
        {
            Assert.True(
                WorkbenchCompositionService.TryCompileDpPerspectiveDpReplace(
                    summary.IcId,
                    0x40000,
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
