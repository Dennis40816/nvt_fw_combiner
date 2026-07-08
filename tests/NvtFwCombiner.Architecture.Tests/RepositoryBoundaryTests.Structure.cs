namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies external combiner versions are documented as exact string tokens.</summary>
    [Fact]
    public void ExternalCombinerVersionsAreDocumentedAsStringTokens()
    {
        string adr = ReadText("docs/adr/0006-external-combiner-tool-runner.md");

        Assert.Contains("`toolVersion` is always a string", adr, StringComparison.Ordinal);
        Assert.Contains("`1.10` and `1.9` are exact version tokens", adr, StringComparison.Ordinal);
    }

    /// <summary>Verifies UI planning documents keep firmware behavior out of ViewModels.</summary>
    [Fact]
    public void UiDocumentsForbidFirmwareSemanticsInViewModels()
    {
        string boundaries = ReadText("docs/ui/viewmodel-boundaries.md");

        Assert.Contains("byte range arithmetic", boundaries, StringComparison.Ordinal);
        Assert.Contains("CRC/Header calculation or `combiner.exe` invocation", boundaries, StringComparison.Ordinal);
        Assert.Contains("No `File.ReadAllBytes` or `Process.Start` in ViewModels", boundaries, StringComparison.Ordinal);
    }

    /// <summary>Verifies Presentation reaches firmware workflow catalogs only through the Bootstrap workbench facade.</summary>
    [Fact]
    public void PresentationUsesBootstrapFacadeInsteadOfFirmwareCatalogs()
    {
        string project = ReadText("src/NvtFwCombiner.Presentation.Avalonia/NvtFwCombiner.Presentation.Avalonia.csproj");
        string presentationSource = ReadPresentationSources();
        string[] forbiddenTokens =
        [
            "NvtFwCombiner.Application.",
            "NvtFwCombiner.Domain.",
            "NvtFwCombiner.Infrastructure.",
            "NvtFwCombiner.Profiles",
            "GenFlashVersionCatalog",
            "TpFlashMapCatalog",
            "TpHeaderCatalog",
            "LegacyCombinerPostbuildCatalog",
            "DpPerspectiveCatalog",
            "NT51950",
            "PostbuildSetup_",
        ];

        Assert.Contains("NvtFwCombiner.Bootstrap.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Application.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Domain.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Infrastructure.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Profiles.csproj", project, StringComparison.Ordinal);
        Assert.Contains("WorkbenchCompositionService", presentationSource, StringComparison.Ordinal);
        foreach (string token in forbiddenTokens)
        {
            Assert.DoesNotContain(token, presentationSource, StringComparison.Ordinal);
        }
    }

    /// <summary>Verifies the Presentation runner remains a thin split adapter over Bootstrap workbench contracts.</summary>
    [Fact]
    public void UiCompositionRunnerConcernsStaySplit()
    {
        string root = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.cs");
        string catalog = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Catalog.cs");
        string common = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Common.cs");
        string facts = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.FirmwareFacts.cs");
        string merge = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Merge.cs");
        string replace = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Replace.cs");

        Assert.Contains("public static partial class UiCompositionRunner", root, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchCompositionService.", root, StringComparison.Ordinal);
        Assert.DoesNotContain("GetFirmwareSlotFacts", root, StringComparison.Ordinal);
        Assert.DoesNotContain("GetStandardMergeMemoryMapRows", root, StringComparison.Ordinal);
        Assert.DoesNotContain("GetReplaceMemoryMapRows", root, StringComparison.Ordinal);
        Assert.Contains("GetSupportedIcIds", catalog, StringComparison.Ordinal);
        Assert.Contains("GetDefaultIcId", catalog, StringComparison.Ordinal);
        Assert.Contains("GetSettingsSnapshot", catalog, StringComparison.Ordinal);
        Assert.Contains("private static MemoryMapRowViewModel ToMemoryMapRow", common, StringComparison.Ordinal);
        Assert.Contains("GetFirmwareSlotFacts", facts, StringComparison.Ordinal);
        Assert.Contains("CreateFlashCodeOutputFileName", facts, StringComparison.Ordinal);
        Assert.Contains("GetStandardMergeMemoryMapRows", merge, StringComparison.Ordinal);
        Assert.Contains("RunGeneralMergeAsync", merge, StringComparison.Ordinal);
        Assert.Contains("GetReplaceMemoryMapRows", replace, StringComparison.Ordinal);
        Assert.Contains("RunReplaceAsync", replace, StringComparison.Ordinal);
    }

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

    /// <summary>Verifies alias-heavy postbuild profile rows stay grouped by IC family.</summary>
    [Fact]
    public void LegacyPostbuildProfileRowsStaySplitByFamily()
    {
        string sharedRows = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildCatalog.Profiles.cs");
        string nt51927Family = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildCatalog.Profiles.Nt51927Family.cs");
        string nt51930Family = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildCatalog.Profiles.Nt51930Family.cs");
        string nt51932Family = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildCatalog.Profiles.Nt51932Family.cs");
        string nt51950Family = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildCatalog.Profiles.Nt51950Family.cs");

        Assert.DoesNotContain("NT51927 CtrlRAM postbuild profile", sharedRows, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51930 CtrlRAM postbuild profile", sharedRows, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51932 CtrlRAM postbuild profile", sharedRows, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51950 CtrlRAM postbuild profile", sharedRows, StringComparison.Ordinal);
        Assert.Contains("public static LegacyCombinerPostbuildProfile Nt51927", nt51927Family, StringComparison.Ordinal);
        Assert.Contains("public static LegacyCombinerPostbuildProfile Nt51917", nt51927Family, StringComparison.Ordinal);
        Assert.Contains("public static LegacyCombinerPostbuildProfile Nt51928", nt51927Family, StringComparison.Ordinal);
        Assert.Contains("owner confirmation: NT51917 follows NT51927", nt51927Family, StringComparison.Ordinal);
        Assert.Contains("owner confirmation: NT51928 follows NT51927", nt51927Family, StringComparison.Ordinal);
        Assert.Contains("public static LegacyCombinerPostbuildProfile Nt51930", nt51930Family, StringComparison.Ordinal);
        Assert.Contains("public static LegacyCombinerPostbuildProfile Nt51930CommonFw1x", nt51930Family, StringComparison.Ordinal);
        Assert.Contains("public static LegacyCombinerPostbuildProfile Nt51931", nt51930Family, StringComparison.Ordinal);
        Assert.Contains("public static LegacyCombinerPostbuildProfile Nt51932", nt51932Family, StringComparison.Ordinal);
        Assert.Contains("public static LegacyCombinerPostbuildProfile Nt51929", nt51932Family, StringComparison.Ordinal);
        Assert.Contains("public static LegacyCombinerPostbuildProfile Nt51919", nt51932Family, StringComparison.Ordinal);
        Assert.Contains("owner confirmation: NT51929 follows NT51932", nt51932Family, StringComparison.Ordinal);
        Assert.Contains("owner confirmation: NT51919 follows NT51929", nt51932Family, StringComparison.Ordinal);
        Assert.Contains("public static LegacyCombinerPostbuildProfile Nt51950", nt51950Family, StringComparison.Ordinal);
        Assert.Contains("public static LegacyCombinerPostbuildProfile Nt51951", nt51950Family, StringComparison.Ordinal);
        Assert.Contains("owner confirmation: NT51951 follows NT51950", nt51950Family, StringComparison.Ordinal);
    }

    /// <summary>Verifies legacy postbuild contract types stay split by responsibility.</summary>
    [Fact]
    public void LegacyPostbuildContractTypesStaySplit()
    {
        string profile = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildProfile.cs");
        string enums = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildEnums.cs");
        string commonFwRule = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerCommonFwVersionRule.cs");
        string branchRule = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildBranchRule.cs");
        string blockArgument = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerBlockArgument.cs");
        string command = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildCommand.cs");
        string commandPlan = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildCommandPlan.cs");

        Assert.Contains("public sealed class LegacyCombinerPostbuildProfile", profile, StringComparison.Ordinal);
        Assert.DoesNotContain("public enum LegacyCombinerCommandFamily", profile, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed class LegacyCombinerPostbuildCommand", profile, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed class LegacyCombinerPostbuildCommandPlan", profile, StringComparison.Ordinal);
        Assert.Contains("public enum LegacyCombinerCommandFamily", enums, StringComparison.Ordinal);
        Assert.Contains("public enum LegacyCombinerBlockSourceKind", enums, StringComparison.Ordinal);
        Assert.Contains("public sealed class LegacyCombinerCommonFwVersionRule", commonFwRule, StringComparison.Ordinal);
        Assert.Contains("public sealed class LegacyCombinerPostbuildBranchRule", branchRule, StringComparison.Ordinal);
        Assert.Contains("public sealed class LegacyCombinerBlockArgument", blockArgument, StringComparison.Ordinal);
        Assert.Contains("public sealed class LegacyCombinerPostbuildCommand", command, StringComparison.Ordinal);
        Assert.Contains("public sealed class LegacyCombinerPostbuildCommandPlan", commandPlan, StringComparison.Ordinal);
    }

    /// <summary>Verifies legacy postbuild planning stays split from write-range and integrity helpers.</summary>
    [Fact]
    public void LegacyPostbuildPlannerConcernsStaySplit()
    {
        string root = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildPlanner.cs");
        string writeRanges = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildPlanner.WriteRanges.cs");
        string integrityRanges = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildPlanner.IntegrityRanges.cs");
        string normalize = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildPlanner.Normalize.cs");

        Assert.Contains("public static partial class LegacyCombinerPostbuildPlanner", root, StringComparison.Ordinal);
        Assert.Contains("CreatePlan", root, StringComparison.Ordinal);
        Assert.Contains("GetStagedFileBlocks", root, StringComparison.Ordinal);
        Assert.Contains("CalculateRequiredCapacity", root, StringComparison.Ordinal);
        Assert.Contains("ResolveBranch", root, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAllowedWriteRangeSectionsForStagedSources", root, StringComparison.Ordinal);
        Assert.DoesNotContain("NormalizeCandidateWriteRangeSections", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static void AddNtBasedHeaderIntegrityRanges", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static void AddNt51927BasedCrcOnlyIntegrityRanges", root, StringComparison.Ordinal);

        Assert.Contains("GetKnownIntegrityWriteRanges", writeRanges, StringComparison.Ordinal);
        Assert.Contains("GetKnownIntegrityWriteRangeSections", writeRanges, StringComparison.Ordinal);
        Assert.Contains("GetAllowedWriteRangeSectionsForStagedSources", writeRanges, StringComparison.Ordinal);
        Assert.Contains("GetAllowedWriteRangeSectionsForInPlaceRefresh", writeRanges, StringComparison.Ordinal);
        Assert.DoesNotContain("private static void AddNtBasedHeaderIntegrityRanges", writeRanges, StringComparison.Ordinal);
        Assert.DoesNotContain("private static string SelectWriteRangeSectionId", writeRanges, StringComparison.Ordinal);

        Assert.Contains("AddNtBasedHeaderIntegrityRanges", integrityRanges, StringComparison.Ordinal);
        Assert.Contains("AddNt51927BasedCrcOnlyIntegrityRanges", integrityRanges, StringComparison.Ordinal);
        Assert.Contains("GetPostbuildBlockSectionId", integrityRanges, StringComparison.Ordinal);
        Assert.DoesNotContain("private static IReadOnlyList<LegacyCombinerPostbuildWriteRange> NormalizeCandidateWriteRangeSections", integrityRanges, StringComparison.Ordinal);

        Assert.Contains("NormalizeCandidateWriteRangeSections", normalize, StringComparison.Ordinal);
        Assert.Contains("SelectWriteRangeSectionId", normalize, StringComparison.Ordinal);
        Assert.DoesNotContain("private static void AddNtBasedHeaderIntegrityRanges", normalize, StringComparison.Ordinal);
    }
}
