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

    /// <summary>Profile summaries project V2 Standard Merge and DP Replace facts without legacy profile exposure.</summary>
    [Fact]
    public void ProfileSummariesProjectCompiledArtifactsWithoutLegacyTypes()
    {
        CompositionProfileDefinition[] legacyReplaceProfiles =
        [
            .. BuiltInReplaceProfiles.All
                .OrderBy(static profile => profile.ProfileId, StringComparer.Ordinal),
        ];
        WorkbenchProfileSummary[] standardSummaries = [.. WorkbenchCompositionService.GetStandardMergeProfileSummaries()];
        WorkbenchProfileSummary[] replaceSummaries = [.. WorkbenchCompositionService.GetReplaceProfileSummaries()];

        Assert.Equal(IcSupportCatalog.IcIds, standardSummaries.Select(static summary => summary.IcId));
        Assert.All(standardSummaries, static summary =>
        {
            Assert.True(summary.CompileSucceeded);
            Assert.Equal(CompositionKind.Merge, summary.CompositionKind);
            Assert.NotEmpty(summary.RequiredInputAddressSpaceIds);
            Assert.Equal(CompiledIcNumberPolicy.NotApplicable, summary.IcNumberPolicy);
        });
        Assert.Equal(
            [
                "nt51950-standard-merge-dp-perspective",
                "nt51951-standard-merge-dp-perspective",
            ],
            standardSummaries
                .Where(static summary => summary.IcId is "NT51950" or "NT51951")
                .Select(static summary => summary.ProfileId));
        AssertProfileSummaries(
            legacyReplaceProfiles,
            [.. replaceSummaries.Where(static summary => summary.IcId == "NT-SYNTHETIC")]);
        Assert.Equal(
            [
                "nt51950-dp-replace-dp-perspective",
                "nt51951-dp-replace-dp-perspective",
                "synthetic-ctrlram-replace",
                "synthetic-dp-replace",
                "synthetic-general-replace",
            ],
            replaceSummaries.Select(static summary => summary.ProfileId));
        Assert.All(
            replaceSummaries.Where(static summary => summary.IcId is "NT51950" or "NT51951"),
            static summary =>
            {
                Assert.True(summary.CompileSucceeded);
                Assert.Equal(CompositionKind.Replace, summary.CompositionKind);
                Assert.Equal(
                    ["dp-replacement", "reference-base"],
                    summary.RequiredInputAddressSpaceIds.Order(StringComparer.Ordinal));
                Assert.Equal(CompiledIcNumberPolicy.SingleSelector, summary.IcNumberPolicy);
            });

        WorkbenchSettingsSnapshot settings = WorkbenchCompositionService.GetSettingsSnapshot();
        Assert.Equal(standardSummaries.Length, settings.StandardMergeProfileCount);
        Assert.Equal(replaceSummaries.Length, settings.ReplaceProfileCount);
        Assert.Equal(13, settings.FlashMapIcCount);
    }

    /// <summary>A failed compatibility compile remains visible with source identity and stable issue codes.</summary>
    [Fact]
    public void ProfileSummaryRetainsCompileFailureDiagnostics()
    {
        CompositionProfileDefinition source = BuiltInStandardMergeProfiles.SyntheticStandardMerge;
        var invalid = new CompositionProfileDefinition(
            source.ProfileId,
            source.ProfileVersion,
            source.IcId,
            source.ModeId,
            source.CompositionKind,
            source.ExperienceId,
            source.DefaultOutputFileName,
            source.Initialization,
            source.AddressSpaces,
            source.Operations,
            source.Regions,
            source.RegionAccessRules,
            IcNumberInputMode.SingleSelector);

        WorkbenchProfileSummary summary = WorkbenchCompositionService.CreateProfileSummary(invalid);

        Assert.False(summary.CompileSucceeded);
        Assert.Equal(source.ProfileId, summary.ProfileId);
        Assert.Equal(source.IcId, summary.IcId);
        Assert.Equal(source.CompositionKind, summary.CompositionKind);
        Assert.Equal(source.DefaultOutputFileName, summary.DefaultOutputFileName);
        Assert.Empty(summary.RequiredInputAddressSpaceIds);
        Assert.Null(summary.IcNumberPolicy);
        Assert.Contains("profile.ic-number-mode.not-applicable", summary.IssueCodes);
    }

    private static void AssertProfileSummaries(
        CompositionProfileDefinition[] profiles,
        IReadOnlyList<WorkbenchProfileSummary> summaries)
    {
        Assert.Equal(profiles.Length, summaries.Count);
        for (int index = 0; index < profiles.Length; index++)
        {
            CompositionProfileDefinition profile = profiles[index];
            ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
            CompiledComposition composition = Assert.IsType<CompiledComposition>(compile.CompiledComposition);
            WorkbenchProfileSummary summary = summaries[index];

            Assert.True(summary.CompileSucceeded);
            Assert.Empty(summary.IssueCodes);
            Assert.Equal(composition.ProfileId, summary.ProfileId);
            Assert.Equal(composition.IcId, summary.IcId);
            Assert.Equal(composition.CompositionKind, summary.CompositionKind);
            Assert.Equal(composition.Plan.RequiredInputAddressSpaceIds, summary.RequiredInputAddressSpaceIds);
            Assert.Equal(composition.DefaultOutputFileName, summary.DefaultOutputFileName);
            Assert.Equal(composition.IcNumberPolicy, summary.IcNumberPolicy);
        }
    }
}
