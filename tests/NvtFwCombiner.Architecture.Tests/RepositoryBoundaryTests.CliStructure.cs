namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies Replace CLI dispatch stays split from parsing, usage, and Workbench reporting.</summary>
    [Fact]
    public void ReplaceCliCommandHandlerConcernsStaySplit()
    {
        string dispatch = ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.cs");
        string options = ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.Options.cs");
        string optionParser = ReadText("src/NvtFwCombiner.Bootstrap/CliOptionParser.cs");
        string result = ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.Result.cs");
        string usage = ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.Usage.cs");
        string workbenchReport = ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.WorkbenchReport.cs");

        Assert.Contains("RunAsync", dispatch, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool TryCreateBindings", dispatch, StringComparison.Ordinal);
        Assert.DoesNotContain("private const string GeneralReplaceInputAddressSpaceId", dispatch, StringComparison.Ordinal);
        Assert.DoesNotContain("private const string GeneralReplaceOperationId", dispatch, StringComparison.Ordinal);
        Assert.Contains("CliOptionParser.TryParse", dispatch, StringComparison.Ordinal);
        Assert.DoesNotContain("private static async Task PrintRunResultAsync", dispatch, StringComparison.Ordinal);
        Assert.DoesNotContain("private static async Task WriteUsageAsync", dispatch, StringComparison.Ordinal);
        Assert.DoesNotContain("private sealed record ParsedOptions", dispatch, StringComparison.Ordinal);
        Assert.Contains("private static bool RequireOption", options, StringComparison.Ordinal);
        Assert.DoesNotContain("TryParse", options, StringComparison.Ordinal);
        Assert.Contains("internal static bool TryParse", optionParser, StringComparison.Ordinal);
        Assert.Contains("internal sealed record ParsedCliOptions", optionParser, StringComparison.Ordinal);
        Assert.DoesNotContain("private static async Task PrintRunResultAsync", result, StringComparison.Ordinal);
        Assert.Contains("private static async Task<int> UnknownReplaceProfileAsync", result, StringComparison.Ordinal);
        Assert.Contains("private static async Task PrintWorkbenchRunResultAsync", workbenchReport, StringComparison.Ordinal);
        Assert.Contains("private static async Task WriteUsageAsync", usage, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-", usage, StringComparison.Ordinal);
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
        string schemaTokens = ReadText("src/NvtFwCombiner.Bootstrap/SavedRuleSchemaTokens.cs");
        string compatibility = ReadText("src/NvtFwCombiner.Bootstrap/SavedCompositionRuleLoader.Compatibility.cs");
        string inputSlots = ReadText("src/NvtFwCombiner.Bootstrap/SavedCompositionRuleLoader.InputSlots.cs");
        string mappingRows = ReadText("src/NvtFwCombiner.Bootstrap/SavedCompositionRuleLoader.MappingRows.cs");
        string operationFragments = ReadText("src/NvtFwCombiner.Bootstrap/SavedCompositionRuleLoader.OperationFragments.cs");
        string savedRuleCli = ReadText("src/NvtFwCombiner.Bootstrap/MergeCliCommandHandler.SavedRules.cs");
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
        Assert.Contains("internal const string CompositionKindMerge = \"merge\";", schemaTokens, StringComparison.Ordinal);
        Assert.Contains("internal const string MappingOverlapReject = \"reject\";", schemaTokens, StringComparison.Ordinal);
        Assert.Contains("internal const string OperationKindCopyRange = \"copy-range\";", schemaTokens, StringComparison.Ordinal);
        Assert.Contains("SavedRuleSchemaTokens.CompositionKindMerge", schema, StringComparison.Ordinal);
        Assert.Contains("SavedRuleSchemaTokens.MappingOverlapReject", schema, StringComparison.Ordinal);
        Assert.Contains("SavedRuleSchemaTokens.OperationKindCopyRange", schema, StringComparison.Ordinal);
        Assert.Contains("SavedRuleSchemaTokens.CompositionKindMerge", compatibility, StringComparison.Ordinal);
        Assert.Contains("SavedRuleSchemaTokens.CompositionKindReplace", compatibility, StringComparison.Ordinal);
        Assert.Contains("SavedRuleSchemaTokens.SupportStatusCandidate", compatibility, StringComparison.Ordinal);
        Assert.Contains("SavedRuleSchemaTokens.CompositionKindMerge", mappingRows, StringComparison.Ordinal);
        Assert.Contains("SavedRuleSchemaTokens.MappingOverlapReject", mappingRows, StringComparison.Ordinal);
        Assert.Contains("SavedRuleSchemaTokens.CompositionKindMerge", operationFragments, StringComparison.Ordinal);
        Assert.Contains("SavedRuleSchemaTokens.OperationKindCopyRange", operationFragments, StringComparison.Ordinal);
        Assert.Contains("SavedRuleSchemaTokens.CompositionKindMerge", savedRuleCli, StringComparison.Ordinal);
        Assert.Contains("SavedRuleSchemaTokens.MappingOverlapReject", savedRuleCli, StringComparison.Ordinal);
        foreach (string literal in new[] { "\"merge\"", "\"reject\"", "\"copy-range\"" })
        {
            Assert.DoesNotContain(literal, compatibility, StringComparison.Ordinal);
            Assert.DoesNotContain(literal, mappingRows, StringComparison.Ordinal);
            Assert.DoesNotContain(literal, operationFragments, StringComparison.Ordinal);
            Assert.DoesNotContain(literal, savedRuleCli, StringComparison.Ordinal);
        }

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

    /// <summary>Verifies the root CLI entry point stays split from command-specific handlers and formatting helpers.</summary>
    [Fact]
    public void CliApplicationConcernsStaySplit()
    {
        string root = ReadText("src/NvtFwCombiner.Bootstrap/CliApplication.cs");
        string options = ReadText("src/NvtFwCombiner.Bootstrap/CliApplication.Options.cs");
        string optionParser = ReadText("src/NvtFwCombiner.Bootstrap/CliOptionParser.cs");
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
        Assert.DoesNotContain("TryParse", options, StringComparison.Ordinal);
        Assert.Contains("CliOptionParser.TryParse", standardMerge, StringComparison.Ordinal);
        Assert.Contains("internal static bool TryParse", optionParser, StringComparison.Ordinal);
        Assert.Contains("internal sealed record ParsedCliOptions", optionParser, StringComparison.Ordinal);
        Assert.Contains("private static async Task<int> RunProfilesAsync", profiles, StringComparison.Ordinal);
        Assert.Contains("GetStandardMergeProfileSummaries", profiles, StringComparison.Ordinal);
        Assert.Contains("GetReplaceProfileSummaries", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileDefinition", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("BuiltInStandardMergeProfiles", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("BuiltInReplaceProfiles", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileCompiler", profiles, StringComparison.Ordinal);
        Assert.Contains("private static async Task PrintRunResultAsync", result, StringComparison.Ordinal);
        Assert.Contains("private static async Task<int> RunStandardMergeAsync", standardMerge, StringComparison.Ordinal);
        Assert.Contains("private static async Task WriteUsageAsync", usage, StringComparison.Ordinal);
    }

    /// <summary>Verifies Replace CLI no longer owns a legacy profile compiler or selector adapter.</summary>
    [Fact]
    public void ReplaceCliUsesWorkbenchCompiledArtifactsOnly()
    {
        string handler = ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.cs");

        Assert.Contains("RunWorkbenchDpReplaceAsync", handler, StringComparison.Ordinal);
        Assert.Contains("RunWorkbenchCtrlRamReplaceAsync", handler, StringComparison.Ordinal);
        Assert.Contains("RunWorkbenchGeneralReplaceAsync", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("TryCompileProfile", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("TryCreateIcNumberSelection", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileDefinition", handler, StringComparison.Ordinal);
    }

    /// <summary>Verifies Standard Merge CLI and Workbench Run share one compiled-resolution boundary.</summary>
    [Fact]
    public void StandardMergeRuntimeConsumesSharedCompiledResolver()
    {
        string cli = ReadText("src/NvtFwCombiner.Bootstrap/CliApplication.StandardMerge.cs");
        string run = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.StandardMerge.Run.cs");
        string displayProfile = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.StandardMerge.DisplayProfile.cs");
        string display = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.StandardMerge.Display.cs");
        string coverage = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.StandardMerge.Coverage.cs");
        string resolver = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.StandardMerge.Compilation.cs");

        foreach (string runtimeSource in new[] { cli, run, displayProfile, display, coverage })
        {
            Assert.Contains("TryCompileStandardMerge", runtimeSource, StringComparison.Ordinal);
            Assert.DoesNotContain("CompositionProfileDefinition", runtimeSource, StringComparison.Ordinal);
            Assert.DoesNotContain("CompositionProfileCompiler", runtimeSource, StringComparison.Ordinal);
            Assert.DoesNotContain("ProfileCompileResult", runtimeSource, StringComparison.Ordinal);
            Assert.DoesNotContain("BuiltInStandardMergeProfiles", runtimeSource, StringComparison.Ordinal);
            Assert.DoesNotContain("NvtFwCombiner.Profiles", runtimeSource, StringComparison.Ordinal);
        }

        Assert.Contains("out CompiledComposition? composition", resolver, StringComparison.Ordinal);
        Assert.Contains("SequenceEqual", cli, StringComparison.Ordinal);
        Assert.Contains("TryGetBuiltInV2StandardMergeCompilation", resolver, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateDpPerspectiveProfileForInputLength", resolver, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileCompiler.Compile", resolver, StringComparison.Ordinal);
        Assert.DoesNotContain("BuiltInStandardMergeProfiles", resolver, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Profiles", resolver, StringComparison.Ordinal);
        Assert.DoesNotContain("new CompositionPlan", resolver, StringComparison.Ordinal);
        Assert.DoesNotContain("new CompositionOperation", resolver, StringComparison.Ordinal);
        Assert.DoesNotContain("RunCompiledCompositionAsync", resolver, StringComparison.Ordinal);
    }
}
