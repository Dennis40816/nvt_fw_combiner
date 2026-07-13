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

    /// <summary>Verifies Replace CLI root stays a command flow instead of owning parsing sub-concerns.</summary>
    [Fact]
    public void BootstrapReplaceCliRootStaysSplit()
    {
        string root = ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.cs");
        string bindings = ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.Bindings.cs");
        string icNumbers = ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.IcNumbers.cs");
        string profileResolution = ReadText(
            "src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.ProfileResolution.cs");
        string profileCompile = ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.ProfileCompile.cs");

        Assert.Contains("internal static async Task<int> RunAsync", root, StringComparison.Ordinal);
        Assert.DoesNotContain("FixedInputOptionsByAddressSpace", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool TryCreateBindings", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool TryCreateIcNumberSelection", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool TryFindReplaceProfile", root, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneralReplaceOperationId", root, StringComparison.Ordinal);
        Assert.Contains("FixedInputOptionsByAddressSpace", bindings, StringComparison.Ordinal);
        Assert.Contains("private static bool TryCreateBindings", bindings, StringComparison.Ordinal);
        Assert.Contains("private static bool TryCreateIcNumberSelection", icNumbers, StringComparison.Ordinal);
        Assert.Contains("private static bool TryFindReplaceProfile", profileResolution, StringComparison.Ordinal);
        Assert.Contains("private const string GeneralReplaceOperationId", profileCompile, StringComparison.Ordinal);
    }

    /// <summary>Verifies shared Replace workbench CLI helpers stay out of the CtrlRAM workflow file.</summary>
    [Fact]
    public void BootstrapReplaceWorkbenchCliHelpersStaySplit()
    {
        string ctrlRam = ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.CtrlRamWorkbench.cs");
        string ctrlRamSlots = ReadText(
            "src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.CtrlRamWorkbench.Slots.cs");
        string support = ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.WorkbenchSupport.cs");
        string report = ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.WorkbenchReport.cs");

        Assert.Contains("RunWorkbenchCtrlRamReplaceAsync", ctrlRam, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool TryResolveWorkbenchIc", ctrlRam, StringComparison.Ordinal);
        Assert.DoesNotContain("private static InputArtifactBinding[] CreateWorkbenchBindings", ctrlRam, StringComparison.Ordinal);
        Assert.DoesNotContain("private static async Task WriteWorkbenchReportFileIfRequestedAsync", ctrlRam, StringComparison.Ordinal);
        Assert.DoesNotContain("private static async Task PrintWorkbenchRunResultAsync", ctrlRam, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool TryCreateWorkbenchCtrlRamSlotPaths", ctrlRam, StringComparison.Ordinal);
        Assert.Contains("private static bool TryCreateWorkbenchCtrlRamSlotPaths", ctrlRamSlots, StringComparison.Ordinal);
        Assert.Contains("private static Dictionary<string, WorkbenchReplaceInputSlot> CreateCtrlRamSlotLookup", ctrlRamSlots, StringComparison.Ordinal);
        Assert.Contains("private static bool TryResolveWorkbenchIc", support, StringComparison.Ordinal);
        Assert.Contains("private static InputArtifactBinding[] CreateWorkbenchBindings", support, StringComparison.Ordinal);
        Assert.Contains("private static async Task WriteWorkbenchReportFileIfRequestedAsync", report, StringComparison.Ordinal);
        Assert.Contains("private static async Task PrintWorkbenchRunResultAsync", report, StringComparison.Ordinal);
    }

    /// <summary>Verifies DP Replace IC facts remain catalog-owned instead of hard-coded in Bootstrap.</summary>
    [Fact]
    public void BootstrapKeepsDpReplaceIcFactsCatalogOwned()
    {
        string bootstrapSource = string.Concat(
            ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.DpWorkbench.cs"),
            ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Dp.cs"));

        Assert.Contains("DpPerspectiveCatalog.FormatSupportedIcIds()", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51950/NT51951", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51950", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Nt51950", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51928", bootstrapSource, StringComparison.Ordinal);
    }

    /// <summary>Verifies supported DP Replace reaches the shared engine from the trusted V2 bundle, not legacy profiles.</summary>
    [Fact]
    public void BootstrapRoutesSupportedDpReplaceThroughTrustedV2Artifacts()
    {
        string replaceDp = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Dp.cs");
        string v2Resolution = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Dp.BuiltInV2.cs");
        string v2Display = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Dp.V2Display.cs");
        string replaceDisplay = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Display.cs");
        string replaceCoverage = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Coverage.cs");
        string replaceCli = ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.DpWorkbench.cs");
        string bundle = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.StandardMerge.BuiltInV2.cs");

        Assert.Contains("TryCompileDpPerspectiveDpReplace", replaceDp, StringComparison.Ordinal);
        Assert.Contains("CompiledCompositionInputBindingFactory.Create", replaceDp, StringComparison.Ordinal);
        Assert.DoesNotContain("BuiltInReplaceProfiles", replaceDp, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileCompiler", replaceDp, StringComparison.Ordinal);
        Assert.Contains("TryResolveDpPerspectiveDpReplaceSelector", replaceCli, StringComparison.Ordinal);
        Assert.DoesNotContain("BuiltInReplaceProfiles", replaceCli, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileDefinition", replaceCli, StringComparison.Ordinal);
        Assert.Contains("IcWorkflowIds.DpReplace", v2Resolution, StringComparison.Ordinal);
        Assert.Contains("s_nt51950Nt51951V2Bundle", v2Resolution, StringComparison.Ordinal);
        Assert.Contains("TryResolveDpPerspectiveDpReplaceDisplay", v2Resolution, StringComparison.Ordinal);
        Assert.DoesNotContain("DpPerspectiveCatalog", v2Display, StringComparison.Ordinal);
        Assert.Contains("TryCreateV2DpReplaceMemoryMapRows", replaceDisplay, StringComparison.Ordinal);
        Assert.Contains("TryGetV2DpReplaceMemoryRangeLabel", replaceDisplay, StringComparison.Ordinal);
        Assert.Contains("TryCreateV2DpReplaceCoverageSegments", replaceCoverage, StringComparison.Ordinal);
        Assert.Contains("ProfileBundleLoader.Load", bundle, StringComparison.Ordinal);
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

    /// <summary>Verifies trusted bundle catalog handoff remains a structural Bootstrap bridge.</summary>
    [Fact]
    public void BootstrapTrustedBundleCatalogBridgeDoesNotOwnSemanticResolution()
    {
        string bridge = ReadText("src/NvtFwCombiner.Bootstrap/TrustedProfileBundleCatalogProjection.cs");
        string infrastructureProject = ReadText("src/NvtFwCombiner.Infrastructure/NvtFwCombiner.Infrastructure.csproj");

        Assert.Contains("TrustedProfileBundleCatalogFactory.Create", bridge, StringComparison.Ordinal);
        Assert.Contains("CopyIdentity", bridge, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer", bridge, StringComparison.Ordinal);
        Assert.DoesNotContain("Normalizer", bridge, StringComparison.Ordinal);
        Assert.DoesNotContain("MapResolution", bridge, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileCompiler", bridge, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Profiles", infrastructureProject, StringComparison.Ordinal);
    }

}
