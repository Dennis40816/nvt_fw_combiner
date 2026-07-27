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

        Assert.Contains("\"schemaVersion\": \"2.2\"", catalog, StringComparison.Ordinal);
        Assert.Equal(15, catalog.Split("\"effectiveCommonFwVersion\":", StringSplitOptions.None).Length - 1);
        Assert.Equal(15, catalog.Split("\"planSelectors\":", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("\"branchRules\":", catalog, StringComparison.Ordinal);
        Assert.Equal(15, catalog.Split("\"processorId\":", StringSplitOptions.None).Length - 1);
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
        Assert.Contains("ResolveSelector", root, StringComparison.Ordinal);
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
        Assert.DoesNotContain("private static IReadOnlyList<LegacyCombinerPostbuildWriteRange> NormalizeCandidateWriteRangeSections", integrityRanges, StringComparison.Ordinal);

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
        Assert.Contains("command.Family != LegacyCombinerCommandFamily.MergeMode", staging, StringComparison.Ordinal);
        Assert.Contains("expectedLength - minimumLength", staging, StringComparison.Ordinal);
        Assert.Contains("ReadExactlyAsync(tailBytes", staging, StringComparison.Ordinal);
        Assert.Contains("FileMode.Append", staging, StringComparison.Ordinal);
    }

    /// <summary>Locks manifest discovery and processor construction to one process lifetime.</summary>
    [Fact]
    public void ExternalProcessorDiscoveryUsesOneExplicitProcessLifetime()
    {
        string factory = ReadText("src/NvtFwCombiner.Bootstrap/ExternalProcessorFactory.cs");
        string ctrlRam = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.CtrlRam.cs");

        Assert.Contains("ProcessLifetime = new(CreateUncached)", factory, StringComparison.Ordinal);
        Assert.Contains("internal static ExternalProcessorGenerationLease AcquireCurrent()", factory, StringComparison.Ordinal);
        Assert.Contains("internal static void Refresh()", factory, StringComparison.Ordinal);
        Assert.Contains("public static void RefreshCtrlRamRuntimeDependencies()", ctrlRam, StringComparison.Ordinal);
        Assert.Contains("ExternalProcessorFactory.Refresh()", ctrlRam, StringComparison.Ordinal);
        Assert.Contains("LazyThreadSafetyMode.ExecutionAndPublication", factory, StringComparison.Ordinal);
        Assert.Equal(1, factory.Split("Directory.EnumerateFiles(", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("static IExternalProcessor? CreateOrNull()", factory, StringComparison.Ordinal);
    }
}
