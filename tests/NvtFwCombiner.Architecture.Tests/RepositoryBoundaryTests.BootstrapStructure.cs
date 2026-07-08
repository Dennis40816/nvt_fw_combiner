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

    /// <summary>Verifies General Merge workbench orchestration, mapping, profile, and report helpers stay split.</summary>
    [Fact]
    public void GeneralMergeWorkbenchConcernsStaySplit()
    {
        string orchestration = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMerge.cs");
        string mapping = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMerge.Mapping.cs");
        string profile = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMerge.Profile.cs");
        string report = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMerge.Report.cs");

        Assert.Contains("RunGeneralMergeAsync", orchestration, StringComparison.Ordinal);
        Assert.Contains("GetGeneralMergeMemoryMapRows", orchestration, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool TryCreateGeneralMergeMappings", orchestration, StringComparison.Ordinal);
        Assert.DoesNotContain("private static CompositionProfileDefinition CreateGeneralMergeProfile", orchestration, StringComparison.Ordinal);
        Assert.DoesNotContain("private static WorkbenchRunResult CreateGeneralMergeReportRunResult", orchestration, StringComparison.Ordinal);
        Assert.Contains("private static bool TryCreateGeneralMergeMappings", mapping, StringComparison.Ordinal);
        Assert.Contains("public sealed record WorkbenchGeneralMergeMappingInput", mapping, StringComparison.Ordinal);
        Assert.Contains("private static CompositionProfileDefinition CreateGeneralMergeProfile", profile, StringComparison.Ordinal);
        Assert.Contains("private static WorkbenchRunResult CreateGeneralMergeReportRunResult", report, StringComparison.Ordinal);
    }

    /// <summary>Verifies Replace CLI dispatch stays split from option parsing, usage text, and result printing.</summary>
    [Fact]
    public void ReplaceCliCommandHandlerConcernsStaySplit()
    {
        string dispatch = ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.cs");
        string options = ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.Options.cs");
        string result = ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.Result.cs");
        string usage = ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.Usage.cs");

        Assert.Contains("RunAsync", dispatch, StringComparison.Ordinal);
        Assert.Contains("TryCreateBindings", dispatch, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool TryParseOptions", dispatch, StringComparison.Ordinal);
        Assert.DoesNotContain("private static async Task PrintRunResultAsync", dispatch, StringComparison.Ordinal);
        Assert.DoesNotContain("private static async Task WriteUsageAsync", dispatch, StringComparison.Ordinal);
        Assert.DoesNotContain("private sealed record ParsedOptions", dispatch, StringComparison.Ordinal);
        Assert.Contains("private static bool TryParseOptions", options, StringComparison.Ordinal);
        Assert.Contains("private static bool RequireOption", options, StringComparison.Ordinal);
        Assert.Contains("private sealed record ParsedOptions", options, StringComparison.Ordinal);
        Assert.Contains("private static async Task PrintRunResultAsync", result, StringComparison.Ordinal);
        Assert.Contains("private static async Task<int> UnknownReplaceProfileAsync", result, StringComparison.Ordinal);
        Assert.Contains("private static async Task WriteUsageAsync", usage, StringComparison.Ordinal);
    }

    /// <summary>Verifies General Merge CLI dispatch stays split from parsing, mapping adaptation, usage text, and result printing.</summary>
    [Fact]
    public void MergeCliCommandHandlerConcernsStaySplit()
    {
        string dispatch = ReadText("src/NvtFwCombiner.Bootstrap/MergeCliCommandHandler.cs");
        string manualMappings = ReadText("src/NvtFwCombiner.Bootstrap/MergeCliCommandHandler.ManualMappings.cs");
        string options = ReadText("src/NvtFwCombiner.Bootstrap/MergeCliCommandHandler.Options.cs");
        string result = ReadText("src/NvtFwCombiner.Bootstrap/MergeCliCommandHandler.Result.cs");
        string usage = ReadText("src/NvtFwCombiner.Bootstrap/MergeCliCommandHandler.Usage.cs");
        string savedRules = ReadText("src/NvtFwCombiner.Bootstrap/MergeCliCommandHandler.SavedRules.cs");

        Assert.Contains("RunAsync", dispatch, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool TryCreateMappings", dispatch, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool TryParseMappingValue", dispatch, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool TryParseOptions", dispatch, StringComparison.Ordinal);
        Assert.DoesNotContain("private static async Task PrintResultAsync", dispatch, StringComparison.Ordinal);
        Assert.DoesNotContain("private static Task WriteUsageAsync", dispatch, StringComparison.Ordinal);
        Assert.DoesNotContain("private sealed record ParsedOptions", dispatch, StringComparison.Ordinal);
        Assert.Contains("private static bool TryCreateMappings", manualMappings, StringComparison.Ordinal);
        Assert.Contains("private static bool TryParseMappingValue", manualMappings, StringComparison.Ordinal);
        Assert.Contains("private static bool TryResolveIc", manualMappings, StringComparison.Ordinal);
        Assert.Contains("private static bool TryCreateMappingsFromSavedRule", savedRules, StringComparison.Ordinal);
        Assert.Contains("private static bool TryParseOptions", options, StringComparison.Ordinal);
        Assert.Contains("private static bool RequireOption", options, StringComparison.Ordinal);
        Assert.Contains("private sealed record ParsedOptions", options, StringComparison.Ordinal);
        Assert.Contains("private static async Task PrintResultAsync", result, StringComparison.Ordinal);
        Assert.Contains("private static async Task PrintReportIssuesAsync", result, StringComparison.Ordinal);
        Assert.Contains("private static Task WriteUsageAsync", usage, StringComparison.Ordinal);
    }

    /// <summary>Verifies saved-rule loading keeps schema shape, compatibility policy, and input slots out of the root parser.</summary>
    [Fact]
    public void SavedCompositionRuleLoaderConcernsStaySplit()
    {
        string root = ReadText("src/NvtFwCombiner.Bootstrap/SavedCompositionRuleLoader.cs");
        string schema = ReadText("src/NvtFwCombiner.Bootstrap/SavedCompositionRuleLoader.Schema.cs");
        string compatibility = ReadText("src/NvtFwCombiner.Bootstrap/SavedCompositionRuleLoader.Compatibility.cs");
        string inputSlots = ReadText("src/NvtFwCombiner.Bootstrap/SavedCompositionRuleLoader.InputSlots.cs");
        string mappingRows = ReadText("src/NvtFwCombiner.Bootstrap/SavedCompositionRuleLoader.MappingRows.cs");
        string operationFragments = ReadText("src/NvtFwCombiner.Bootstrap/SavedCompositionRuleLoader.OperationFragments.cs");
        string json = ReadText("src/NvtFwCombiner.Bootstrap/SavedCompositionRuleLoader.Json.cs");
        string ranges = ReadText("src/NvtFwCombiner.Bootstrap/SavedCompositionRuleLoader.Ranges.cs");
        string grammar = ReadText("src/NvtFwCombiner.Bootstrap/SavedCompositionRuleLoader.Grammar.cs");
        string issues = ReadText("src/NvtFwCombiner.Bootstrap/SavedCompositionRuleLoader.Issues.cs");

        Assert.Contains("public static SavedCompositionRuleLoadResult Load", root, StringComparison.Ordinal);
        Assert.Contains("private static SavedCompositionRuleLoadResult Parse", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static readonly HashSet<string> TopLevelProperties", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static SavedRuleCompatibility ReadCompatibility", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static HashSet<string> ReadInputSlotTemplateIds", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static void ValidateRuleCompatibility", root, StringComparison.Ordinal);
        Assert.Contains("TopLevelProperties", schema, StringComparison.Ordinal);
        Assert.Contains("MappingRowProperties", schema, StringComparison.Ordinal);
        Assert.Contains("OperationFragmentKindValues", schema, StringComparison.Ordinal);
        Assert.Contains("private static SavedRuleCompatibility ReadCompatibility", compatibility, StringComparison.Ordinal);
        Assert.Contains("private static void ValidateRuleCompatibility", compatibility, StringComparison.Ordinal);
        Assert.Contains("private static HashSet<string> ReadInputSlotTemplateIds", inputSlots, StringComparison.Ordinal);
        Assert.Contains("private static List<SavedRuleMappingRow> ReadMappingRows", mappingRows, StringComparison.Ordinal);
        Assert.Contains("private static List<SavedRuleOperationFragment> ReadOperationFragments", operationFragments, StringComparison.Ordinal);
        Assert.Contains("private static string RequiredEnum", json, StringComparison.Ordinal);
        Assert.DoesNotContain("ByteRange", json, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedRegex", json, StringComparison.Ordinal);
        Assert.Contains("private static ByteRange? ParseByteRange", ranges, StringComparison.Ordinal);
        Assert.Contains("private static int? OptionalPositiveInt", ranges, StringComparison.Ordinal);
        Assert.Contains("private static partial Regex IdRegex", grammar, StringComparison.Ordinal);
        Assert.Contains("private static partial Regex SemverRegex", grammar, StringComparison.Ordinal);
        Assert.Contains("private static SavedRuleValidationIssue Issue", issues, StringComparison.Ordinal);
        Assert.Contains("private static void AddDuplicateIssues", issues, StringComparison.Ordinal);
    }

    /// <summary>Verifies the Workbench facade stays split into catalog, Standard Merge, and shared adapter helpers.</summary>
    [Fact]
    public void WorkbenchCompositionServiceConcernsStaySplit()
    {
        string facade = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.cs");
        string catalog = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Catalog.cs");
        string common = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Common.cs");
        string standardMerge = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.StandardMerge.cs");
        string standardMergeDisplay = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.StandardMerge.Display.cs");
        string standardMergeRun = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.StandardMerge.Run.cs");
        string firmwareMetadata = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.FirmwareMetadata.cs");
        string outputNaming = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.OutputNaming.cs");
        string ctrlRamDisplay = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.CtrlRamDisplay.cs");
        string replacePostbuild = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Postbuild.cs");

        Assert.Contains("public static partial class WorkbenchCompositionService", facade, StringComparison.Ordinal);
        Assert.DoesNotContain("GetStandardMergeMemoryMapRows", facade, StringComparison.Ordinal);
        Assert.DoesNotContain("GetSettingsSnapshot", facade, StringComparison.Ordinal);
        Assert.DoesNotContain("ToRunProfile", facade, StringComparison.Ordinal);
        Assert.Contains("GetSupportedIcIds", catalog, StringComparison.Ordinal);
        Assert.Contains("GetSettingsSnapshot", catalog, StringComparison.Ordinal);
        Assert.Contains("private static CompositionRunProfile ToRunProfile", common, StringComparison.Ordinal);
        Assert.Contains("private static string FormatIssues", common, StringComparison.Ordinal);
        Assert.Contains("StandardMergeProfilesByIc", standardMerge, StringComparison.Ordinal);
        Assert.Contains("GetStandardMergePolicySummary", standardMerge, StringComparison.Ordinal);
        Assert.DoesNotContain("RunStandardMergeAsync", standardMerge, StringComparison.Ordinal);
        Assert.DoesNotContain("GetStandardMergeMemoryMapRows", standardMerge, StringComparison.Ordinal);
        Assert.Contains("GetStandardMergeMemoryMapRows", standardMergeDisplay, StringComparison.Ordinal);
        Assert.Contains("GetStandardMergeCoverageSegments", standardMergeDisplay, StringComparison.Ordinal);
        Assert.Contains("RunStandardMergeAsync", standardMergeRun, StringComparison.Ordinal);
        Assert.Contains("ResolveStandardMergeProfileForInputs", standardMergeRun, StringComparison.Ordinal);
        Assert.Contains("TryReadBaseCommonFwVersion", firmwareMetadata, StringComparison.Ordinal);
        Assert.Contains("FirmwareConfigMetadataReader.TryRead", firmwareMetadata, StringComparison.Ordinal);
        Assert.Contains("GenFlashVersionCatalog.TryReadDpVersion", firmwareMetadata, StringComparison.Ordinal);
        Assert.Contains("DisplayCategory", firmwareMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("PostbuildSetup_", firmwareMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateFlashCodeOutputFileName", firmwareMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("GetCtrlRamRegions", firmwareMetadata, StringComparison.Ordinal);
        Assert.Contains("CreateFlashCodeOutputFileName", outputNaming, StringComparison.Ordinal);
        Assert.Contains("FindDpVersionToken", outputNaming, StringComparison.Ordinal);
        Assert.Contains("FindTpVersionToken", outputNaming, StringComparison.Ordinal);
        Assert.DoesNotContain("GenFlashVersionCatalog", outputNaming, StringComparison.Ordinal);
        Assert.DoesNotContain("OutputMainAbsoluteAddress", outputNaming, StringComparison.Ordinal);
        Assert.DoesNotContain("InputRelativeOffset", outputNaming, StringComparison.Ordinal);
        Assert.DoesNotContain("FirmwareConfigMetadataReader.TryRead", outputNaming, StringComparison.Ordinal);
        Assert.Contains("GetCtrlRamRegions", ctrlRamDisplay, StringComparison.Ordinal);
        Assert.Contains("TpFlashMapCatalog.GetCtrlRamRegions", ctrlRamDisplay, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateFlashCodeOutputFileName", ctrlRamDisplay, StringComparison.Ordinal);
        Assert.DoesNotContain("FirmwareConfigMetadataReader.TryRead", replacePostbuild, StringComparison.Ordinal);
    }

    /// <summary>Verifies the root CLI entry point stays split from command-specific handlers and formatting helpers.</summary>
    [Fact]
    public void CliApplicationConcernsStaySplit()
    {
        string root = ReadText("src/NvtFwCombiner.Bootstrap/CliApplication.cs");
        string options = ReadText("src/NvtFwCombiner.Bootstrap/CliApplication.Options.cs");
        string profiles = ReadText("src/NvtFwCombiner.Bootstrap/CliApplication.Profiles.cs");
        string result = ReadText("src/NvtFwCombiner.Bootstrap/CliApplication.Result.cs");
        string standardMerge = ReadText("src/NvtFwCombiner.Bootstrap/CliApplication.StandardMerge.cs");
        string usage = ReadText("src/NvtFwCombiner.Bootstrap/CliApplication.Usage.cs");

        Assert.Contains("RunAsync", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static async Task<int> RunStandardMergeAsync", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static async Task<int> RunProfilesAsync", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool TryParseOptions", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static async Task PrintRunResultAsync", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static async Task WriteUsageAsync", root, StringComparison.Ordinal);
        Assert.Contains("private static bool TryParseOptions", options, StringComparison.Ordinal);
        Assert.Contains("private sealed record ParsedOptions", options, StringComparison.Ordinal);
        Assert.Contains("private static async Task<int> RunProfilesAsync", profiles, StringComparison.Ordinal);
        Assert.Contains("private static async Task PrintRunResultAsync", result, StringComparison.Ordinal);
        Assert.Contains("private static async Task<int> RunStandardMergeAsync", standardMerge, StringComparison.Ordinal);
        Assert.Contains("private static async Task WriteUsageAsync", usage, StringComparison.Ordinal);
    }
}
