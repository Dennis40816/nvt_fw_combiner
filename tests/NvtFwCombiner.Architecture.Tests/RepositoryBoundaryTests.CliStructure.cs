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

    /// <summary>Verifies every Workbench Replace command shares one CLI run lifecycle.</summary>
    [Fact]
    public void WorkbenchReplaceCommandsShareRunLifecycle()
    {
        string support = ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.WorkbenchSupport.cs");
        string dp = ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.DpWorkbench.cs");
        string ctrlRam = ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.CtrlRamWorkbench.cs");
        string general = ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.GeneralWorkbench.cs");

        Assert.Contains("private static async Task<int> RunWorkbenchReplaceAsync", support, StringComparison.Ordinal);
        Assert.Contains("EnsureOutputDoesNotAliasInputs", support, StringComparison.Ordinal);
        Assert.Contains("EnsureReportDoesNotAliasProtectedPaths", support, StringComparison.Ordinal);
        Assert.Contains("CliCompositionRunSupport.WriteReportJsonAsync", support, StringComparison.Ordinal);
        Assert.Contains("PrintWorkbenchRunResultAsync", support, StringComparison.Ordinal);
        foreach (string workflow in new[] { dp, ctrlRam, general })
        {
            Assert.Equal(1, CountOccurrences(workflow, "RunWorkbenchReplaceAsync("));
            Assert.DoesNotContain("EnsureOutputDoesNotAliasInputs", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("EnsureReportDoesNotAliasProtectedPaths", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("output file already exists", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("WriteWorkbenchReportFileIfRequestedAsync", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("PrintWorkbenchRunResultAsync", workflow, StringComparison.Ordinal);
        }

        Assert.Contains("CompositionExecutionAdapter.RunReplaceAsync", dp, StringComparison.Ordinal);
        Assert.Contains("CompositionExecutionAdapter.RunReplaceAsync", ctrlRam, StringComparison.Ordinal);
        Assert.Contains(
            "CompositionExecutionAdapter.PreviewGeneralReplaceEphemeralDraftAsync",
            general,
            StringComparison.Ordinal);
        Assert.Contains(
            "CompositionExecutionAdapter.BuildGeneralReplaceEphemeralDraftAsync",
            general,
            StringComparison.Ordinal);
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
        Assert.Contains("private static bool TryCreateGeneralMergeDraft", manualMappings, StringComparison.Ordinal);
        Assert.Contains("private static bool TryParseMappingValue", manualMappings, StringComparison.Ordinal);
        Assert.Contains("private static bool TryResolveIc", manualMappings, StringComparison.Ordinal);
        Assert.Contains("private static bool TryCreateDraftFromSavedRule", savedRules, StringComparison.Ordinal);
        Assert.Contains("SavedRuleV2GeneralMergeDraftLoader.Load", savedRules, StringComparison.Ordinal);
        Assert.Contains("private static bool TryParseOptions", options, StringComparison.Ordinal);
        Assert.Contains("private static bool RequireOption", options, StringComparison.Ordinal);
        Assert.Contains("private sealed record ParsedOptions", options, StringComparison.Ordinal);
        Assert.Contains("private static async Task PrintResultAsync", result, StringComparison.Ordinal);
        Assert.Contains("private static async Task PrintReportIssuesAsync", result, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteReportAsync", result, StringComparison.Ordinal);
        Assert.Contains("CliCompositionRunSupport.WriteReportJsonAsync", dispatch, StringComparison.Ordinal);
        Assert.Contains("private static Task WriteUsageAsync", usage, StringComparison.Ordinal);
    }

    /// <summary>Verifies normal v2 rule execution admits the complete canonical contract before draft projection.</summary>
    [Fact]
    public void SavedRuleV2ExecutionUsesCanonicalSchemaBeforeMaterialization()
    {
        string handler = ReadText(
            "src/NvtFwCombiner.Bootstrap/MergeCliCommandHandler.SavedRules.cs");
        string draftLoader = ReadText(
            "src/NvtFwCombiner.Bootstrap/SavedRuleV2GeneralMergeDraftLoader.cs");
        string admission = ReadText(
            "src/NvtFwCombiner.Bootstrap/SavedCompositionRuleV2Admission.cs");
        string schema = ReadText(
            "src/NvtFwCombiner.Infrastructure/Contracts/SavedCompositionRuleV2Schema.cs");
        string infrastructureProject = ReadText(
            "src/NvtFwCombiner.Infrastructure/NvtFwCombiner.Infrastructure.csproj");

        Assert.Contains(
            "GetGeneralMergeSavedRuleAdmissionContext",
            handler,
            StringComparison.Ordinal);
        int admissionIndex = draftLoader.IndexOf(
            "SavedCompositionRuleV2Admission.ValidateGeneralMerge",
            StringComparison.Ordinal);
        int materializationIndex = draftLoader.IndexOf(
            "new GeneralMergeDraftState",
            StringComparison.Ordinal);
        Assert.True(admissionIndex >= 0);
        Assert.True(materializationIndex > admissionIndex);
        Assert.DoesNotContain(
            "SavedRuleV2GeneralMergeInitializerLoader.Parse",
            draftLoader,
            StringComparison.Ordinal);
        Assert.Contains(
            "SavedCompositionRuleV2Schema.IsValid",
            admission,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "TopLevelProperties",
            admission,
            StringComparison.Ordinal);
        Assert.Contains(
            "ProfileBundleSchemaValidator.IsInstanceValid",
            schema,
            StringComparison.Ordinal);
        Assert.Contains(
            @"..\..\docs\contracts\saved-composition-rule-v2.schema.json",
            infrastructureProject,
            StringComparison.Ordinal);
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
        Assert.Contains("GetDpReplaceProfileSummaries", profiles, StringComparison.Ordinal);
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
        string run = ReadText("src/NvtFwCombiner.Bootstrap/CompositionExecutionAdapter.StandardMerge.cs");
        string display = ReadText(
            "src/NvtFwCombiner.Bootstrap/CompositionMemoryProjection.StandardMerge.cs");
        string generalMergeProfile = ReadText(
            "src/NvtFwCombiner.Bootstrap/CanonicalAuthoringAdapter.GeneralMerge.Profile.cs");
        string resolver = ReadText(
            "src/NvtFwCombiner.Bootstrap/CanonicalCapabilityResolution.StandardMerge.Compilation.cs");

        string[] compileSources =
        [
            .. Directory.EnumerateFiles(
                    Path.Combine(Root.FullName, "src", "NvtFwCombiner.Bootstrap"),
                    "*.cs",
                    SearchOption.TopDirectoryOnly)
                .Where(path => File.ReadAllText(path).Contains("TryCompileStandardMerge(", StringComparison.Ordinal))
                .Select(static path => Path.GetFileName(path))
                .Order(StringComparer.Ordinal),
        ];
        Assert.Equal(
            [
                "CanonicalAuthoringAdapter.GeneralMerge.Profile.cs",
                "CanonicalAuthoringAdapter.StandardMerge.cs",
                "CanonicalCapabilityProjection.WorkflowDisclosure.cs",
                "CanonicalCapabilityResolution.StandardMerge.Compilation.cs",
                "CliApplication.StandardMerge.cs",
                "CompositionExecutionAdapter.StandardMerge.cs",
                "CompositionMemoryProjection.StandardMerge.cs",
            ],
            compileSources);

        foreach (string runtimeSource in new[] { cli, run, display, generalMergeProfile })
        {
            Assert.Contains("TryCompileStandardMerge", runtimeSource, StringComparison.Ordinal);
            Assert.DoesNotContain("CompositionProfileDefinition", runtimeSource, StringComparison.Ordinal);
            Assert.DoesNotContain("CompositionProfileCompiler", runtimeSource, StringComparison.Ordinal);
            Assert.DoesNotContain("ProfileCompileResult", runtimeSource, StringComparison.Ordinal);
            Assert.DoesNotContain("BuiltInStandardMergeProfiles", runtimeSource, StringComparison.Ordinal);
            Assert.DoesNotContain("NvtFwCombiner.Profiles", runtimeSource, StringComparison.Ordinal);
        }

        Assert.Equal(2, CountOccurrences(cli, "TryCompileStandardMerge("));
        Assert.Equal(1, CountOccurrences(run, "TryCompileStandardMerge("));
        Assert.Equal(1, CountOccurrences(display, "TryCompileStandardMerge("));
        Assert.Equal(1, CountOccurrences(generalMergeProfile, "TryCompileStandardMerge("));
        Assert.Contains("out CompiledComposition? composition", resolver, StringComparison.Ordinal);
        Assert.Contains("GetStandardMergeInputAddressSpaces", cli, StringComparison.Ordinal);
        Assert.Contains("InputOptionsByAddressSpace", cli, StringComparison.Ordinal);
        Assert.Contains("TryGetBuiltInV2StandardMergeCompilation", resolver, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateDpPerspectiveProfileForInputLength", resolver, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileCompiler.Compile", resolver, StringComparison.Ordinal);
        Assert.DoesNotContain("BuiltInStandardMergeProfiles", resolver, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Profiles", resolver, StringComparison.Ordinal);
        Assert.DoesNotContain("new CompositionPlan", resolver, StringComparison.Ordinal);
        Assert.DoesNotContain("new CompositionOperation", resolver, StringComparison.Ordinal);
        Assert.DoesNotContain("RunCompiledCompositionAsync", resolver, StringComparison.Ordinal);
    }

    /// <summary>Standard Merge hosts adapt compiler and bytes while Application owns readiness semantics.</summary>
    [Fact]
    public void StandardMergeReadinessSemanticsStayApplicationOwned()
    {
        string application = ReadText(
            "src/NvtFwCombiner.Application/Authoring/CompiledAuthoringWorkflow.cs");
        string authoringAdapter = ReadText(
            "src/NvtFwCombiner.Bootstrap/CanonicalAuthoringAdapter.StandardMerge.cs");
        string inspectionAdapter = ReadText(
            "src/NvtFwCombiner.Bootstrap/CanonicalAuthoringAdapter.StandardMerge.InputInspection.cs");
        string presentation = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.StandardMergeAuthoring.cs");

        Assert.Contains("new InputSelectionMemberReadiness", application, StringComparison.Ordinal);
        Assert.Contains("new InputSelectionNextAction", application, StringComparison.Ordinal);
        foreach (string adapter in new[] { authoringAdapter, inspectionAdapter })
        {
            Assert.DoesNotContain("new InputSelectionMemberReadiness", adapter, StringComparison.Ordinal);
            Assert.DoesNotContain("new InputSelectionNextAction", adapter, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("File.Exists", presentation, StringComparison.Ordinal);
        Assert.DoesNotContain("FileInfo", presentation, StringComparison.Ordinal);
        Assert.DoesNotContain("TryCompileStandardMerge", presentation, StringComparison.Ordinal);
        Assert.DoesNotContain("new InputSelectionMemberReadiness", presentation, StringComparison.Ordinal);
    }
}
