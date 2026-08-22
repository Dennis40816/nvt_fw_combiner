namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies Postbuild command facts live in one hash-pinned data catalog, not static C# rows.</summary>
    [Fact]
    public void PostbuildProfileRowsStayDataOwned()
    {
        string catalog = ReadText("profiles/built-in/ctrlram-postbuild-v2/catalog.json");
        string loader = ReadText("src/NvtFwCombiner.Infrastructure/ExternalTools/BuiltInPostbuildProfileCatalog.cs");
        string pinnedJsonLoader = ReadText("src/NvtFwCombiner.Infrastructure/PinnedJsonCatalogLoader.cs");

        Assert.Contains("\"schemaVersion\": \"2.3\"", catalog, StringComparison.Ordinal);
        Assert.Contains("\"diffDlmPolicies\":", catalog, StringComparison.Ordinal);
        Assert.Equal(11, catalog.Split("\"effectiveCommonFwVersion\":", StringSplitOptions.None).Length - 1);
        Assert.Equal(11, catalog.Split("\"planSelectors\":", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("\"branchRules\":", catalog, StringComparison.Ordinal);
        Assert.Equal(11, catalog.Split("\"processorId\":", StringSplitOptions.None).Length - 1);
        Assert.Contains("NT51917", catalog, StringComparison.Ordinal);
        Assert.Contains("NT51951", catalog, StringComparison.Ordinal);
        Assert.Contains("ExpectedSha256", loader, StringComparison.Ordinal);
        Assert.Contains("PinnedJsonCatalogLoader.Load", loader, StringComparison.Ordinal);
        Assert.Contains(
            "UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow",
            pinnedJsonLoader,
            StringComparison.Ordinal);
        AssertNoProductionText("LegacyCombinerPostbuildCatalog");
    }

    /// <summary>Locks every Dynamic DiffDLM summary to active-prefix scatter and immutable inactive records.</summary>
    [Fact]
    public void DynamicDiffDlmSummariesPreserveInactiveRecords()
    {
        string[] summaries =
        [
            NormalizeWhitespace(ReadText(
                "docs/architecture/integrity-processing-matrix.md")),
            NormalizeWhitespace(ReadText(
                "docs/architecture/ctrlram-postbuild-command-matrix.md")),
        ];

        foreach (string summary in summaries)
        {
            Assert.Contains(
                "scatters only the declared `N - 1` active DLM prefixes",
                summary,
                StringComparison.Ordinal);
            Assert.Contains(
                "AE suffix after the active prefix does not enter the read set or write set",
                summary,
                StringComparison.Ordinal);
            Assert.Contains(
                "Every active Diff NF tail and every inactive target record remains byte-identical",
                summary,
                StringComparison.Ordinal);
        }

        static string NormalizeWhitespace(string text)
        {
            return string.Join(
                ' ',
                text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }
    }

    /// <summary>Verifies retained Legacy Combiner runner contract types stay split by responsibility.</summary>
    [Fact]
    public void LegacyPostbuildContractTypesStaySplit()
    {
        string profile = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildProfile.cs");
        string enums = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildEnums.cs");
        string commonFwVersion = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerCommonFwVersion.cs");
        string planSelector = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildPlanSelector.cs");
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
        Assert.Contains("public readonly record struct LegacyCombinerCommonFwVersion", commonFwVersion, StringComparison.Ordinal);
        Assert.Contains("public sealed class LegacyCombinerPostbuildPlanSelector", planSelector, StringComparison.Ordinal);
        Assert.Contains("public enum LegacyCombinerPostbuildPlanSelectorKind", planSelector, StringComparison.Ordinal);
        Assert.Contains("public sealed class LegacyCombinerBlockArgument", blockArgument, StringComparison.Ordinal);
        Assert.Contains("public sealed class LegacyCombinerPostbuildCommand", command, StringComparison.Ordinal);
        Assert.Contains("public sealed class LegacyCombinerPostbuildCommandPlan", commandPlan, StringComparison.Ordinal);
        Assert.Contains("internal LegacyCombinerPostbuildCommandPlan(", commandPlan, StringComparison.Ordinal);
        Assert.DoesNotContain("public LegacyCombinerPostbuildCommandPlan(", commandPlan, StringComparison.Ordinal);
        Assert.Contains("private readonly CompiledPlanTemplate[] _compiledPlans", profile, StringComparison.Ordinal);
        Assert.Contains("public LegacyCombinerPostbuildCommandPlan ResolvePlan(", profile, StringComparison.Ordinal);
    }

    /// <summary>Verifies profile-time protocol compilation stays split from derived write-range helpers.</summary>
    [Fact]
    public void LegacyPostbuildCompiledPlanConcernsStaySplit()
    {
        string root = string.Concat(
            ReadText("src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildPlanCompiler.cs"),
            ReadText("src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildPlanCompiler.Resolve.cs"));
        string writeRanges = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildPlanCompiler.WriteRanges.cs");
        string integrityRanges = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildPlanCompiler.IntegrityRanges.cs");
        string normalize = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildPlanCompiler.Normalize.cs");

        Assert.Contains("public static partial class LegacyCombinerPostbuildPlanCompiler", root, StringComparison.Ordinal);
        Assert.Contains("CompileProtocol", root, StringComparison.Ordinal);
        Assert.Contains("ResolveCommands", root, StringComparison.Ordinal);
        Assert.Contains("GetStagedFileBlocks", root, StringComparison.Ordinal);
        Assert.Contains("CalculateRequiredCapacity", root, StringComparison.Ordinal);
        Assert.DoesNotContain("CreatePlan", root, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveSelector", root, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAllowedWriteRangeSectionsForStagedSources", root, StringComparison.Ordinal);
        Assert.DoesNotContain("NormalizeCandidateWriteRangeSections", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static void AddNtBasedHeaderIntegrityRanges", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static void AddNt51927BasedCrcOnlyIntegrityRanges", root, StringComparison.Ordinal);

        Assert.Contains("GetKnownIntegrityWriteRangeSections", writeRanges, StringComparison.Ordinal);
        Assert.Contains("GetAllowedWriteRangeSectionsForStagedSources", writeRanges, StringComparison.Ordinal);
        Assert.Contains("GetAllowedWriteRangeSectionsForInPlaceRefresh", writeRanges, StringComparison.Ordinal);
        Assert.DoesNotContain("GetKnownIntegrityWriteRanges", writeRanges, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAllowedWriteRangesForStagedSources", writeRanges, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAllowedWriteRangesForInPlaceRefresh", writeRanges, StringComparison.Ordinal);
        Assert.DoesNotContain("private static void AddNtBasedHeaderIntegrityRanges", writeRanges, StringComparison.Ordinal);
        Assert.DoesNotContain("private static string SelectWriteRangeSectionId", writeRanges, StringComparison.Ordinal);

        Assert.Contains("AddNtBasedHeaderIntegrityRanges", integrityRanges, StringComparison.Ordinal);
        Assert.Contains("AddNt51927BasedCrcOnlyIntegrityRanges", integrityRanges, StringComparison.Ordinal);
        Assert.Contains("GetPostbuildBlockSectionId", integrityRanges, StringComparison.Ordinal);
        Assert.DoesNotContain("NormalizeCandidateWriteRangeSections", integrityRanges, StringComparison.Ordinal);

        Assert.Contains("NormalizeCandidateWriteRangeSections", normalize, StringComparison.Ordinal);
        Assert.Contains("SelectWriteRangeSection", normalize, StringComparison.Ordinal);
        Assert.DoesNotContain("private static void AddNtBasedHeaderIntegrityRanges", normalize, StringComparison.Ordinal);
    }

    /// <summary>Locks one final full staging read while preserving selective short-output normalization.</summary>
    [Fact]
    public void LegacyPostbuildPipelineReadsTheCompleteFirmwareOnlyAtFinalImport()
    {
        string processor = ReadText(
            "src/NvtFwCombiner.Infrastructure/ExternalTools/LegacyCombinerPostbuildProcessor.cs");
        string staging = ReadText(
            "src/NvtFwCombiner.Infrastructure/ExternalTools/LegacyCombinerPostbuildProcessor.Staging.cs");

        string pipelineSource = processor + staging;
        Assert.Equal(
            1,
            pipelineSource.Split("File.ReadAllBytesAsync(firmwarePath", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("File.ReadAllBytes(firmwarePath", pipelineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("File.ReadAllBytesAsync(firmwarePath", staging, StringComparison.Ordinal);
        Assert.Contains("if (!command.RetainShortOutputTail)", staging, StringComparison.Ordinal);
        Assert.Contains("expectedLength - minimumLength", staging, StringComparison.Ordinal);
        Assert.Contains("ReadExactlyAsync(tailBytes", staging, StringComparison.Ordinal);
        Assert.Contains("FileMode.Append", staging, StringComparison.Ordinal);
    }

    /// <summary>Locks bounded discovery, publication, and leases to one Infrastructure lifecycle owner.</summary>
    [Fact]
    public void ExternalProcessorDiscoveryUsesOneExplicitProcessLifetime()
    {
        string loader = ReadText(
            "src/NvtFwCombiner.Infrastructure/ExternalTools/ExternalProcessorEnvironmentLoader.cs");
        string host = ReadText("src/NvtFwCombiner.Bootstrap/CompositionHostServices.cs");
        string cli = ReadText("src/NvtFwCombiner.Cli/CliApplication.cs");
        string ctrlRam = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs");

        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src/NvtFwCombiner.Bootstrap/ExternalProcessorFactory.cs")));
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src/NvtFwCombiner.Bootstrap/RuntimeDependencyReadinessLeaseProvider.cs")));
        Assert.Equal(1, CountOccurrences(host, "new ExternalProcessorEnvironmentLoader()"));
        Assert.Contains("Channel.CreateBounded<ExternalProcessorEnvironmentLoadUpdate>", loader,
            StringComparison.Ordinal);
        Assert.Contains("MaximumDepth = 16", loader, StringComparison.Ordinal);
        Assert.Contains("MaximumVisitedEntries = 4_096", loader, StringComparison.Ordinal);
        Assert.Contains("MaximumManifestCount = 256", loader, StringComparison.Ordinal);
        Assert.Contains("MaximumManifestBytes = 1_048_576", loader, StringComparison.Ordinal);
        Assert.Contains("MaximumCumulativeManifestBytes = 16_777_216", loader,
            StringComparison.Ordinal);
        Assert.Contains("ExternalProcessorEnvironmentLease AcquireCurrent()", loader,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Process.Start", loader, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshCtrlRamRuntimeDependencies", ctrlRam, StringComparison.Ordinal);
        Assert.Contains("_acquireExternalProcessor()", ctrlRam, StringComparison.Ordinal);
        Assert.Contains("_externalProcessorGenerationIsCurrent", ctrlRam, StringComparison.Ordinal);
        int version = cli.IndexOf("if (args is [\"--version\"]", StringComparison.Ordinal);
        int help = cli.IndexOf("if (args.Length == 0", StringComparison.Ordinal);
        int load = cli.IndexOf("ExternalEnvironmentLoader.LoadToCompletionAsync", StringComparison.Ordinal);
        Assert.True(version >= 0 && help > version && load > help);
    }
}
