namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies Replace CLI dispatch stays split from parsing, usage, and Workbench reporting.</summary>
    [Fact]
    public void ReplaceCliCommandHandlerConcernsStaySplit()
    {
        string dispatch = ReadText("src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.cs");
        string options = ReadText("src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.Options.cs");
        string optionParser = ReadText("src/NvtFwCombiner.Cli/CliOptionParser.cs");
        string result = ReadText("src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.Result.cs");
        string usage = ReadText("src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.Usage.cs");
        string workbenchReport = ReadText("src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.Report.cs");

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
        Assert.Contains("private static async Task PrintCompositionRunResultAsync", workbenchReport, StringComparison.Ordinal);
        Assert.Contains("private static async Task WriteUsageAsync", usage, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-", usage, StringComparison.Ordinal);
    }

    /// <summary>Verifies every Replace command shares one CLI run lifecycle.</summary>
    [Fact]
    public void ReplaceCommandsShareRunLifecycle()
    {
        string support = ReadText("src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.RunSupport.cs");
        string dp = ReadText("src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.Dp.cs");
        string ctrlRam = ReadText("src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.cs");
        string general = ReadText("src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.General.cs");

        Assert.Contains("private static async Task<int> CompleteReplaceRunAsync", support, StringComparison.Ordinal);
        Assert.Contains("EnsureOutputDoesNotAliasInputs", support, StringComparison.Ordinal);
        Assert.Contains("EnsureReportDoesNotAliasProtectedPaths", support, StringComparison.Ordinal);
        Assert.Contains("CliCompositionRunSupport.WriteReportJsonAsync", support, StringComparison.Ordinal);
        Assert.Contains("PrintCompositionRunResultAsync", support, StringComparison.Ordinal);
        foreach (string workflow in new[] { dp, ctrlRam, general })
        {
            Assert.Equal(1, CountOccurrences(workflow, "CompleteReplaceRunAsync("));
            Assert.DoesNotContain("EnsureOutputDoesNotAliasInputs", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("output file already exists", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("WriteWorkbenchReportFileIfRequestedAsync", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("PrintCompositionRunResultAsync", workflow, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("EnsureReportDoesNotAliasProtectedPaths", dp, StringComparison.Ordinal);
        Assert.DoesNotContain("EnsureReportDoesNotAliasProtectedPaths", ctrlRam, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(general, "EnsureReportDoesNotAliasProtectedPaths("));

        Assert.Contains("DpReplaceAuthoring.PrepareSession", dp, StringComparison.Ordinal);
        Assert.Contains("host.CompositionExecution.ExecuteAsync", dp, StringComparison.Ordinal);
        Assert.Contains("host.CtrlRamAuthoring.PrepareSession", ctrlRam, StringComparison.Ordinal);
        Assert.Contains("host.CompositionExecution", ctrlRam, StringComparison.Ordinal);
        Assert.Contains(".ExecuteAsync(", ctrlRam, StringComparison.Ordinal);
        Assert.Contains(
            "host.GeneralAuthoring.PrepareReplaceSessionAsync",
            general,
            StringComparison.Ordinal);
        Assert.Contains(
            "host.CompositionExecution.ExecuteAsync",
            general,
            StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(general, "host.CompositionExecution.ExecuteAsync"));
        foreach (string workflow in new[] { dp, ctrlRam, general })
        {
            Assert.Contains("ResolveAcceptedOutput", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("GetReplaceDefaultOutputFileName", workflow, StringComparison.Ordinal);
        }
        Assert.Contains("AcceptedCompositionExecutionRequest", general, StringComparison.Ordinal);
    }

    /// <summary>Verifies General Merge CLI dispatch stays split from parsing, mapping adaptation, usage text, and result printing.</summary>
    [Fact]
    public void MergeCliCommandHandlerConcernsStaySplit()
    {
        string dispatch = ReadText("src/NvtFwCombiner.Cli/MergeCliCommandHandler.cs");
        string manualMappings = ReadText("src/NvtFwCombiner.Cli/MergeCliCommandHandler.ManualMappings.cs");
        string options = ReadText("src/NvtFwCombiner.Cli/MergeCliCommandHandler.Options.cs");
        string result = ReadText("src/NvtFwCombiner.Cli/MergeCliCommandHandler.Result.cs");
        string usage = ReadText("src/NvtFwCombiner.Cli/MergeCliCommandHandler.Usage.cs");
        string savedRules = ReadText("src/NvtFwCombiner.Cli/MergeCliCommandHandler.SavedRules.cs");

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
        Assert.Contains("authoring.LoadGeneralMergeSavedRule", savedRules, StringComparison.Ordinal);
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
            "src/NvtFwCombiner.Cli/MergeCliCommandHandler.SavedRules.cs");
        string draftLoader = ReadText(
            "src/NvtFwCombiner.Infrastructure/Composition/SavedRuleV2GeneralMergeDraftLoader.cs");
        string admission = ReadText(
            "src/NvtFwCombiner.Infrastructure/Composition/SavedCompositionRuleV2Admission.cs");
        string schema = ReadText(
            "src/NvtFwCombiner.Infrastructure/Contracts/SavedCompositionRuleV2Schema.cs");
        string infrastructureProject = ReadText(
            "src/NvtFwCombiner.Infrastructure/NvtFwCombiner.Infrastructure.csproj");

        Assert.Contains(
            "authoring.LoadGeneralMergeSavedRule",
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
        string root = ReadText("src/NvtFwCombiner.Cli/CliApplication.cs");
        string optionParser = ReadText("src/NvtFwCombiner.Cli/CliOptionParser.cs");
        string profiles = ReadText("src/NvtFwCombiner.Cli/CliApplication.Profiles.cs");
        string result = ReadText("src/NvtFwCombiner.Cli/CliApplication.Result.cs");
        string standardMerge = ReadText("src/NvtFwCombiner.Cli/CliApplication.StandardMerge.cs");
        string usage = ReadText("src/NvtFwCombiner.Cli/CliApplication.Usage.cs");

        Assert.Contains("RunAsync", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static async Task<int> RunStandardMergeAsync", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static async Task<int> RunProfilesAsync", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool TryParseOptions", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static async Task PrintRunResultAsync", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static async Task WriteUsageAsync", root, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "CliApplication.Options.cs")));
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
        string handler = ReadText("src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.cs");

        Assert.Contains("RunDpReplaceAsync", handler, StringComparison.Ordinal);
        Assert.Contains("RunCtrlRamReplaceAsync", handler, StringComparison.Ordinal);
        Assert.Contains("RunGeneralReplaceAsync", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("RunWorkbench", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("TryCompileProfile", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("TryCreateIcNumberSelection", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileDefinition", handler, StringComparison.Ordinal);
    }

    /// <summary>Verifies Standard Merge CLI and desktop share one compiled-resolution boundary.</summary>
    [Fact]
    public void StandardMergeRuntimeConsumesSharedCompiledResolver()
    {
        string cli = ReadText("src/NvtFwCombiner.Cli/CliApplication.StandardMerge.cs");
        string run = ReadText("src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs");
        string sharedRun = run;
        string generalMergeProfile = ReadText(
            "src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.cs");
        string resolver = ReadText(
            "src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityCompiler.StandardMerge.cs");

        string[] compileSources =
        [
            .. Directory.EnumerateFiles(
                    Path.Combine(Root.FullName, "src"),
                    "*.cs",
                    SearchOption.AllDirectories)
                .Where(path => !HasPathSegment(path, "bin") && !HasPathSegment(path, "obj"))
                .Where(path => File.ReadAllText(path).Contains("TryCompileStandardMerge(", StringComparison.Ordinal))
                .Select(path => Path.GetRelativePath(
                    Path.Combine(Root.FullName, "src"),
                    path).Replace('\\', '/'))
                .Order(StringComparer.Ordinal),
        ];
        Assert.Equal(
            [
                "NvtFwCombiner.Application/Authoring/StandardMergeAuthoringExperience.cs",
                "NvtFwCombiner.Application/Capabilities/CanonicalCapabilityCompiler.StandardMerge.cs",
                "NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.cs",
            ],
            compileSources);

        foreach (string runtimeSource in new[] { generalMergeProfile })
        {
            Assert.Contains("TryCompileStandardMerge", runtimeSource, StringComparison.Ordinal);
            Assert.DoesNotContain("CompositionProfileDefinition", runtimeSource, StringComparison.Ordinal);
            Assert.DoesNotContain("CompositionProfileCompiler", runtimeSource, StringComparison.Ordinal);
            Assert.DoesNotContain("ProfileCompileResult", runtimeSource, StringComparison.Ordinal);
            Assert.DoesNotContain("BuiltInStandardMergeProfiles", runtimeSource, StringComparison.Ordinal);
            Assert.DoesNotContain("NvtFwCombiner.Profiles", runtimeSource, StringComparison.Ordinal);
        }

        Assert.Equal(0, CountOccurrences(cli, "TryCompileStandardMerge("));
        Assert.Contains("StandardMergeAuthoring.PrepareSession(", cli, StringComparison.Ordinal);
        Assert.DoesNotContain("TryCompileStandardMerge(", run, StringComparison.Ordinal);
        Assert.Contains("ExecuteAcceptedCompositionAsync(", run, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(sharedRun, "AcceptedSessionExecutionInputs.CreateBindings("));
        Assert.Equal(1, CountOccurrences(generalMergeProfile, "TryCompileStandardMerge("));
        Assert.Contains("out CompiledComposition? composition", resolver, StringComparison.Ordinal);
        Assert.Contains("StandardMergeAuthoring.GetInputAddressSpaces", cli, StringComparison.Ordinal);
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
            "src/NvtFwCombiner.Application/Authoring/StandardMergeAuthoringExperience.cs");
        string inspectionAdapter = ReadText(
            "src/NvtFwCombiner.Application/Authoring/StandardMergeAuthoringExperience.InputInspection.cs");
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
