namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies CLI and Workbench share the same preview-before-build execution gate.</summary>
    [Fact]
    public void BootstrapUsesOnePreviewBeforeBuildGate()
    {
        string bootstrapSource = ReadBootstrapSources();
        string executionSupport = ReadText(
            "src/NvtFwCombiner.Bootstrap/CompositionRunExecutionSupport.cs");

        Assert.Contains("CompositionRunExecutionSupport.PreviewOrBuildAsync", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildWithInternalPreviewAsync", bootstrapSource, StringComparison.Ordinal);
        Assert.Contains("service.PreviewAsync(request, cancellationToken)", executionSupport, StringComparison.Ordinal);
        Assert.Contains("service.BuildAsync(request.WithApprovedPreviewToken(preview.PreviewToken!)", executionSupport, StringComparison.Ordinal);
        Assert.Equal(
            1,
            CountOccurrences(bootstrapSource, "request.WithApprovedPreviewToken(preview.PreviewToken!)"));
    }

    /// <summary>Verifies DP Replace IC facts remain catalog-owned instead of hard-coded in Bootstrap.</summary>
    [Fact]
    public void BootstrapKeepsDpReplaceIcFactsCatalogOwned()
    {
        string bootstrapSource = ReadBootstrapSources();

        Assert.Contains("DpPerspectiveCatalog.FormatSupportedIcIds()", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51950/NT51951", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51950", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Nt51950", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51928", bootstrapSource, StringComparison.Ordinal);
    }

    /// <summary>Verifies workbench Replace mode ids stay centralized for UI and CLI adapters.</summary>
    [Fact]
    public void BootstrapOwnsWorkbenchReplaceModeIds()
    {
        string bootstrapSource = ReadBootstrapSources();
        string replaceModes = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchReplaceModes.cs");
        string mergeModes = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchMergeModes.cs");
        string bootstrapWithoutReplaceModes = bootstrapSource
            .Replace(replaceModes, string.Empty, StringComparison.Ordinal)
            .Replace(mergeModes, string.Empty, StringComparison.Ordinal);

        Assert.Contains("public const string Dp = \"DP\"", replaceModes, StringComparison.Ordinal);
        Assert.Contains("public const string CtrlRam = \"CtrlRAM\"", replaceModes, StringComparison.Ordinal);
        Assert.Contains("public const string General = \"General\"", replaceModes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"DP\"", bootstrapWithoutReplaceModes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"CtrlRAM\"", bootstrapWithoutReplaceModes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"General\"", bootstrapWithoutReplaceModes, StringComparison.Ordinal);
    }

    /// <summary>Verifies workbench Merge mode ids stay centralized for UI adapters.</summary>
    [Fact]
    public void BootstrapOwnsWorkbenchMergeModeIds()
    {
        string bootstrapSource = ReadBootstrapSources();
        string mergeModes = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchMergeModes.cs");
        string replaceModes = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchReplaceModes.cs");
        string bootstrapWithoutModeCatalogs = bootstrapSource
            .Replace(mergeModes, string.Empty, StringComparison.Ordinal)
            .Replace(replaceModes, string.Empty, StringComparison.Ordinal);

        Assert.Contains("public const string Standard = \"Normal\"", mergeModes, StringComparison.Ordinal);
        Assert.Contains("public const string AbCode = \"AB Code\"", mergeModes, StringComparison.Ordinal);
        Assert.Contains("public const string General = \"General\"", mergeModes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Normal\"", bootstrapWithoutModeCatalogs, StringComparison.Ordinal);
        Assert.DoesNotContain("\"AB Code\"", bootstrapWithoutModeCatalogs, StringComparison.Ordinal);
    }

    /// <summary>Verifies workbench report/run id prefixes stay centralized by workflow mode.</summary>
    [Fact]
    public void BootstrapOwnsWorkbenchRunIdPrefixes()
    {
        string runner = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Runner.cs");
        string standardMerge = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.StandardMerge.Run.cs");
        string generalMerge = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMerge.cs");
        string generalMergeReport = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMerge.Report.cs");
        string replaceDp = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Dp.cs");
        string replaceCtrlRam = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.CtrlRam.cs");
        string replaceGeneral = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.General.cs");
        string replaceReport = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Report.cs");

        Assert.Contains("private const string StandardMergeRunIdPrefix = \"ui\";", runner, StringComparison.Ordinal);
        Assert.Contains("private const string GeneralMergeRunIdPrefix = \"ui-merge-general\";", runner, StringComparison.Ordinal);
        Assert.Contains("private const string DpReplaceRunIdPrefix = \"ui-replace-dp\";", runner, StringComparison.Ordinal);
        Assert.Contains("private const string CtrlRamReplaceRunIdPrefix = \"ui-replace-ctrlram\";", runner, StringComparison.Ordinal);
        Assert.Contains("private const string GeneralReplaceRunIdPrefix = \"ui-replace-general\";", runner, StringComparison.Ordinal);
        Assert.Contains("private static string CreateWorkbenchReportRunId", runner, StringComparison.Ordinal);
        Assert.Contains("private static string GetReplaceRunIdPrefix", runner, StringComparison.Ordinal);
        Assert.Contains("StandardMergeRunIdPrefix", standardMerge, StringComparison.Ordinal);
        Assert.Contains("GeneralMergeRunIdPrefix", generalMerge, StringComparison.Ordinal);
        Assert.Contains(
            "CreateWorkbenchReportRunId(GeneralMergeRunIdPrefix, build, timestamp)",
            generalMergeReport,
            StringComparison.Ordinal);
        Assert.Contains("DpReplaceRunIdPrefix", replaceDp, StringComparison.Ordinal);
        Assert.Contains("CtrlRamReplaceRunIdPrefix", replaceCtrlRam, StringComparison.Ordinal);
        Assert.Contains("GeneralReplaceRunIdPrefix", replaceGeneral, StringComparison.Ordinal);
        Assert.Contains(
            "CreateWorkbenchReportRunId(GetReplaceRunIdPrefix(replaceMode), build, timestamp)",
            replaceReport,
            StringComparison.Ordinal);
        foreach (string source in new[]
        {
            standardMerge,
            generalMerge,
            generalMergeReport,
            replaceDp,
            replaceCtrlRam,
            replaceGeneral,
            replaceReport,
        })
        {
            Assert.DoesNotContain("\"ui-merge-general\"", source, StringComparison.Ordinal);
            Assert.DoesNotContain("\"ui-replace\"", source, StringComparison.Ordinal);
            Assert.DoesNotContain("\"ui-replace-dp\"", source, StringComparison.Ordinal);
            Assert.DoesNotContain("\"ui-replace-ctrlram\"", source, StringComparison.Ordinal);
            Assert.DoesNotContain("\"ui-replace-general\"", source, StringComparison.Ordinal);
        }
    }

    /// <summary>Verifies CLI and Workbench create IC-number selections through one Bootstrap helper.</summary>
    [Fact]
    public void BootstrapOwnsIcNumberSelectionConstruction()
    {
        string bootstrapSource = ReadBootstrapSources();
        string helper = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchIcNumberSelections.cs");
        string bootstrapWithoutHelper = bootstrapSource.Replace(helper, string.Empty, StringComparison.Ordinal);

        Assert.Contains("new IcNumberSelection", helper, StringComparison.Ordinal);
        Assert.Contains("WorkbenchIcNumberSelections.FromNumberToken", bootstrapSource, StringComparison.Ordinal);
        Assert.Contains("WorkbenchIcNumberSelections.Single", bootstrapSource, StringComparison.Ordinal);
        Assert.Contains("WorkbenchIcNumberSelections.Numeric", bootstrapSource, StringComparison.Ordinal);
        Assert.Contains("WorkbenchIcNumberSelections.Cascade", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new IcNumberSelection", bootstrapWithoutHelper, StringComparison.Ordinal);
    }

    /// <summary>Verifies workbench slot ids stay centralized for CLI, UI, and report adapters.</summary>
    [Fact]
    public void BootstrapOwnsWorkbenchSlotIds()
    {
        string bootstrapSource = ReadBootstrapSources();
        string slotIds = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchSlotIds.cs");
        string bootstrapWithoutSlotIds = bootstrapSource.Replace(slotIds, string.Empty, StringComparison.Ordinal);

        Assert.Contains("public const string MergeDp = \"merge-dp\"", slotIds, StringComparison.Ordinal);
        Assert.Contains("public const string MergeTp = \"merge-tp\"", slotIds, StringComparison.Ordinal);
        Assert.Contains("public const string MergeLd = \"merge-ld\"", slotIds, StringComparison.Ordinal);
        Assert.Contains("public const string ReplaceBase = \"replace-base\"", slotIds, StringComparison.Ordinal);
        Assert.Contains("public const string ReplaceDp = \"replace-dp\"", slotIds, StringComparison.Ordinal);
        Assert.Contains(
            "public const string ReplaceCtrlRamPrefix = CompositionAddressSpaceIds.DynamicCtrlRamReplacementPrefix;",
            slotIds,
            StringComparison.Ordinal);
        foreach (string slotLiteral in new[]
        {
            "\"merge-dp\"",
            "\"merge-tp\"",
            "\"merge-ld\"",
            "\"replace-base\"",
            "\"replace-dp\"",
            "\"replace-ctrlram-",
        })
        {
            Assert.DoesNotContain(slotLiteral, bootstrapWithoutSlotIds, StringComparison.Ordinal);
        }
    }

    /// <summary>Verifies UI-facing workflow ids are projected from the profile catalog without repeating literals.</summary>
    [Fact]
    public void BootstrapProjectsWorkflowIdsForUiAdapters()
    {
        string workflowIds = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchWorkflowIds.cs");

        Assert.Contains("public const string StandardMerge = IcWorkflowIds.StandardMerge;", workflowIds, StringComparison.Ordinal);
        Assert.Contains("public const string GeneralMerge = IcWorkflowIds.GeneralMerge;", workflowIds, StringComparison.Ordinal);
        Assert.Contains("public const string DpReplace = IcWorkflowIds.DpReplace;", workflowIds, StringComparison.Ordinal);
        Assert.Contains("public const string CtrlRamReplace = IcWorkflowIds.CtrlRamReplace;", workflowIds, StringComparison.Ordinal);
        Assert.Contains("public const string GeneralReplace = IcWorkflowIds.GeneralReplace;", workflowIds, StringComparison.Ordinal);
        Assert.DoesNotContain("\"standard-merge\"", workflowIds, StringComparison.Ordinal);
        Assert.DoesNotContain("\"dp-replace\"", workflowIds, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ctrlram-replace\"", workflowIds, StringComparison.Ordinal);
        Assert.DoesNotContain("\"general-merge\"", workflowIds, StringComparison.Ordinal);
        Assert.DoesNotContain("\"general-replace\"", workflowIds, StringComparison.Ordinal);
    }

    /// <summary>Verifies report output-difference classifications are projected from the report contract.</summary>
    [Fact]
    public void BootstrapProjectsOutputDifferenceClassifications()
    {
        string classifications = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchOutputDifferenceClassifications.cs");

        Assert.Contains("OutputDifferenceClassifications.DeclaredReplacement", classifications, StringComparison.Ordinal);
        Assert.Contains("OutputDifferenceClassifications.PostbuildCrcHeader", classifications, StringComparison.Ordinal);
        Assert.Contains("OutputDifferenceClassifications.PreservedReference", classifications, StringComparison.Ordinal);
        Assert.Contains("OutputDifferenceClassifications.Unexpected", classifications, StringComparison.Ordinal);
        Assert.DoesNotContain("\"DeclaredReplacement\"", classifications, StringComparison.Ordinal);
        Assert.DoesNotContain("\"PostbuildCrcHeader\"", classifications, StringComparison.Ordinal);
        Assert.DoesNotContain("\"PreservedReference\"", classifications, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Unexpected\"", classifications, StringComparison.Ordinal);
    }

    /// <summary>Verifies UI-facing composition issue codes are projected from the Domain contract.</summary>
    [Fact]
    public void BootstrapProjectsCompositionIssueCodes()
    {
        string bootstrapSource = ReadBootstrapSources();
        string issueCodes = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionIssueCodes.cs");

        Assert.Contains("CompositionIssueCodes.InputAddressSpaceLengthMismatch", bootstrapSource, StringComparison.Ordinal);
        Assert.Contains("CompositionIssueCodes.InputAddressSpaceTruncated", issueCodes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"input.address-space.length-mismatch\"", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\"input.address-space.truncated\"", bootstrapSource, StringComparison.Ordinal);
    }

    /// <summary>Verifies workbench planning/report issue codes stay centralized for UI and CLI adapters.</summary>
    [Fact]
    public void BootstrapOwnsWorkbenchIssueCodes()
    {
        string bootstrapSource = ReadBootstrapSources();
        string issueCodes = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchIssueCodes.cs");
        string bootstrapWithoutIssueCodes = bootstrapSource.Replace(issueCodes, string.Empty, StringComparison.Ordinal);

        Assert.Contains("public const string GeneralMergeSourceOutOfBounds = \"ui.general-merge.source-out-of-bounds\"", issueCodes, StringComparison.Ordinal);
        Assert.Contains("public const string ReplaceCtrlRamPostbuildCategoryUnknown = \"replace.ctrlram.postbuild-category-unknown\"", issueCodes, StringComparison.Ordinal);
        Assert.Contains("public const string InputArtifactReadFailed = \"input.artifact.read-failed\"", issueCodes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ui.general-merge.", bootstrapWithoutIssueCodes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ui.general-replace.", bootstrapWithoutIssueCodes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ui.input.", bootstrapWithoutIssueCodes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"replace.ctrlram.", bootstrapWithoutIssueCodes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"replace.general.", bootstrapWithoutIssueCodes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"replace.dp.", bootstrapWithoutIssueCodes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"replace.mode.", bootstrapWithoutIssueCodes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"input.artifact.", bootstrapWithoutIssueCodes, StringComparison.Ordinal);
    }

    /// <summary>Verifies saved-rule validation codes stay centralized as a Bootstrap CLI contract.</summary>
    [Fact]
    public void BootstrapOwnsSavedRuleIssueCodes()
    {
        string bootstrapSource = ReadBootstrapSources();
        string issueCodes = ReadText("src/NvtFwCombiner.Bootstrap/SavedRuleIssueCodes.cs");
        string bootstrapWithoutIssueCodes = bootstrapSource.Replace(issueCodes, string.Empty, StringComparison.Ordinal);

        Assert.Contains("public const string PropertyUnknown = \"saved-rule.property.unknown\"", issueCodes, StringComparison.Ordinal);
        Assert.Contains(
            "public const string ProcessorDependencyUnsupported = \"saved-rule.processor-dependency.unsupported\"",
            issueCodes,
            StringComparison.Ordinal);
        Assert.Contains(
            "public const string OperationFragmentProcessorDependencyUnsupported = \"saved-rule.operation-fragment.processor-dependency.unsupported\"",
            issueCodes,
            StringComparison.Ordinal);
        Assert.DoesNotContain("\"saved-rule.", bootstrapWithoutIssueCodes, StringComparison.Ordinal);
    }

    /// <summary>Verifies General mapping text parsing is owned by one Bootstrap helper.</summary>
    [Fact]
    public void BootstrapRangeTextOwnsGeneralMappingParsing()
    {
        string bootstrapSource = ReadBootstrapSources();
        string rangeText = ReadText("src/NvtFwCombiner.Bootstrap/BootstrapRangeText.cs");

        Assert.Contains("internal static bool TryParseNonNegativeLong", rangeText, StringComparison.Ordinal);
        Assert.Contains("internal static string FormatHex", rangeText, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(bootstrapSource, "internal static bool TryParseNonNegativeLong"));
        Assert.Equal(1, CountOccurrences(bootstrapSource, "internal static string FormatHex"));
        Assert.DoesNotContain("private static bool TryParseNonNegativeLong", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private static string FormatHex(", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CliCompositionRunSupport.TryParseNonNegativeLong", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CliCompositionRunSupport.FormatHex", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("FormatWorkbenchHex", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryParseCliNonNegativeLong", bootstrapSource, StringComparison.Ordinal);
    }

}
