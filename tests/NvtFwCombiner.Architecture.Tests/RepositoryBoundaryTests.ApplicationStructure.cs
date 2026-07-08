namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies the Application run service root stays split from processor, report, and hash helpers.</summary>
    [Fact]
    public void CompositionRunServiceConcernsStaySplit()
    {
        string root = ReadText("src/NvtFwCombiner.Application/Composition/CompositionRunService.cs");
        string externalProcessors = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionRunService.ExternalProcessors.cs");
        string reports = ReadText("src/NvtFwCombiner.Application/Composition/CompositionRunService.Reports.cs");
        string hashing = ReadText("src/NvtFwCombiner.Application/Composition/CompositionRunService.Hashing.cs");
        string inputs = ReadText("src/NvtFwCombiner.Application/Composition/CompositionRunService.Inputs.cs");
        string previewTokens = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionRunService.PreviewTokens.cs");
        string outputDifferences = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionRunService.OutputDifferences.cs");

        Assert.Contains("public sealed partial class CompositionRunService", root, StringComparison.Ordinal);
        Assert.Contains("PreviewAsync", root, StringComparison.Ordinal);
        Assert.Contains("BuildAsync", root, StringComparison.Ordinal);
        Assert.Contains("RunAsync", root, StringComparison.Ordinal);
        Assert.DoesNotContain("TransformExternalProcessorAsync", root, StringComparison.Ordinal);
        Assert.DoesNotContain("ExternalProcessorRequest", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static CompositionRunReport CreateReport", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static string ToSha256Hex", root, StringComparison.Ordinal);
        Assert.DoesNotContain("SHA256", root, StringComparison.Ordinal);
        Assert.Contains("ExecutePlanAsync", externalProcessors, StringComparison.Ordinal);
        Assert.Contains("TransformExternalProcessorAsync", externalProcessors, StringComparison.Ordinal);
        Assert.Contains("ExternalProcessorRequest", externalProcessors, StringComparison.Ordinal);
        Assert.Contains("private static CompositionRunReport CreateReport", reports, StringComparison.Ordinal);
        Assert.Contains("private static MutationRunSummary ToMutationSummary", reports, StringComparison.Ordinal);
        Assert.Contains("private static OperationRunSummary ToOperationSummary", reports, StringComparison.Ordinal);
        Assert.Contains("private static string ToSha256Hex", hashing, StringComparison.Ordinal);
        Assert.Contains("SHA256.HashData", hashing, StringComparison.Ordinal);
        Assert.Contains("ToSha256Hex(buffer)", inputs, StringComparison.Ordinal);
        Assert.Contains("ToSha256Hex(execution.OutputBytes.Span)", previewTokens, StringComparison.Ordinal);
        Assert.Contains("ToSliceSha256Hex", outputDifferences, StringComparison.Ordinal);
        Assert.Contains("ToSliceHexPreview", outputDifferences, StringComparison.Ordinal);
    }
}
