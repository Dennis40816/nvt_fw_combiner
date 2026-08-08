namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Application composition and external-tool compatibility shells stay retired.</summary>
    [Fact]
    public void ApplicationCompositionCompatibilityShellsStayRetired()
    {
        string runResult = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionRunResult.cs");
        string replay = ReadText(
                "src/NvtFwCombiner.Application/Composition/OutputDifferenceReplaySegment.cs")
            .ReplaceLineEndings("\n");
        string invocationProfile = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/ExternalCombinerInvocationProfile.cs");
        string abOutputName = ReadText(
            "src/NvtFwCombiner.Application/Composition/AbCodeOutputNameResolver.cs");
        string toolRegistry = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/ExternalCombinerToolRegistry.cs");
        string processorResult = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/ExternalProcessorResult.cs")
            .ReplaceLineEndings("\n");

        Assert.DoesNotContain("public CompositionRunResult(", runResult, StringComparison.Ordinal);
        Assert.DoesNotContain("ClonePublicOutputBytes", runResult, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "public bool MatchesDifferenceEvidence(\n"
                + "        long differenceStart,\n"
                + "        long differenceLength,\n"
                + "        string beforeSha256,",
            replay,
            StringComparison.Ordinal);
        Assert.Equal(
            1,
            CountOccurrences(
                invocationProfile,
                "ExternalCombinerStagingTokens.FindArgumentTemplateErrors(argumentTemplateSnapshot)"));
        Assert.DoesNotContain(
            "output?.RendererKind != CompiledOutputNameRendererKind.AbCodeV1",
            abOutputName,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "public IReadOnlyCollection<ExternalCombinerToolManifest> Manifests",
            toolRegistry,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "public static ExternalProcessorResult Success(\n"
                + "        ReadOnlyMemory<byte> outputBytes,\n"
                + "        IReadOnlyList<ByteRange> changedRanges)",
            processorResult,
            StringComparison.Ordinal);
    }
}
