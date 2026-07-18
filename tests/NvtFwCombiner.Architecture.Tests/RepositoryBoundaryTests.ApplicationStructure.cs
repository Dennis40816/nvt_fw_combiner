namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies typed run progress stays Application-owned and cannot invoke host callbacks inline.</summary>
    [Fact]
    public void CompositionRunProgressStaysApplicationOwnedAndAsynchronous()
    {
        string progress = ReadText("src/NvtFwCombiner.Application/Composition/CompositionRunProgress.cs");
        string root = ReadText("src/NvtFwCombiner.Application/Composition/CompositionRunService.cs");
        string externalProcessors = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionRunService.ExternalProcessors.cs");
        string domainSources = ReadDomainSources();

        Assert.Contains("public enum CompositionRunPhase", progress, StringComparison.Ordinal);
        Assert.Contains("public sealed class CompositionRunProgressSnapshot", progress, StringComparison.Ordinal);
        Assert.Contains("public sealed class CompositionRunProgressFeed", progress, StringComparison.Ordinal);
        Assert.Contains("Channel.CreateBounded<CompositionRunProgressSnapshot>", progress, StringComparison.Ordinal);
        Assert.Contains("_feed?.Publish", progress, StringComparison.Ordinal);
        Assert.DoesNotContain("IProgress<", progress, StringComparison.Ordinal);
        Assert.Contains("progress.Complete()", root, StringComparison.Ordinal);
        Assert.Contains(
            "progressPublisher.Report(CompositionRunPhase.RunningExternalProcessor)",
            externalProcessors,
            StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionRunPhase", domainSources, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionRunProgressSnapshot", domainSources, StringComparison.Ordinal);
    }

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
        string outputDifferenceBytes = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionRunService.OutputDifferenceBytes.cs");
        string outputDifferenceExpectations = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionRunService.OutputDifferenceExpectations.cs");

        Assert.Contains("public sealed partial class CompositionRunService", root, StringComparison.Ordinal);
        Assert.Contains("PreviewAsync", root, StringComparison.Ordinal);
        Assert.Contains("BuildAsync", root, StringComparison.Ordinal);
        Assert.Contains("PreviewOrBuildAsync", root, StringComparison.Ordinal);
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
        Assert.Contains("CreateOutputDifferences", outputDifferences, StringComparison.Ordinal);
        Assert.DoesNotContain("private static IEnumerable<OutputDifferenceExpectation> CreateOutputDifferenceExpectations", outputDifferences, StringComparison.Ordinal);
        Assert.DoesNotContain("private static string ToSliceSha256Hex", outputDifferences, StringComparison.Ordinal);
        Assert.Contains("ToSliceSha256Hex", outputDifferenceBytes, StringComparison.Ordinal);
        Assert.Contains("ToSliceHexPreview", outputDifferenceBytes, StringComparison.Ordinal);
        Assert.Contains("CreateOutputDifferenceExpectations", outputDifferenceExpectations, StringComparison.Ordinal);
        Assert.Contains("ClassifyDifferenceSegment", outputDifferenceExpectations, StringComparison.Ordinal);
    }

    /// <summary>Verifies final-output postconditions are compiled policy, not caller-provided run callbacks.</summary>
    [Fact]
    public void FinalOutputPostconditionsStayArtifactBound()
    {
        string root = ReadText("src/NvtFwCombiner.Application/Composition/CompositionRunService.cs");
        string finalOutputValidations = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionRunService.FinalOutputValidations.cs");
        string composition = ReadText("src/NvtFwCombiner.Domain/Composition/CompiledComposition.cs");
        string fingerprint = ReadText("src/NvtFwCombiner.Domain/Composition/CompiledComposition.Fingerprint.cs");
        string validationRequirement = ReadText(
            "src/NvtFwCombiner.Domain/Composition/CompiledValidationRequirement.cs");
        string bootstrapSources = ReadBootstrapSources();

        Assert.Contains(
            "EvaluateFinalOutput(request.CompiledComposition, execution.OutputBytes)",
            root,
            StringComparison.Ordinal);
        Assert.Contains("ValidationRequirements { get; }", composition, StringComparison.Ordinal);
        Assert.Contains(
            "AppendValidationRequirements(builder, composition.ValidationRequirements)",
            fingerprint,
            StringComparison.Ordinal);
        Assert.Contains("CompiledFirmwareConfigBackupVersionValidation", finalOutputValidations, StringComparison.Ordinal);
        Assert.Contains("FirmwareConfigMetadataReader.TryReadBackup", finalOutputValidations, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Bootstrap", finalOutputValidations, StringComparison.Ordinal);
        Assert.DoesNotContain("LegacyProfileValidationRequirements", ReadProfileSources(), StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileCompiler", ReadProfileSources(), StringComparison.Ordinal);
        Assert.DoesNotContain("replace.ctrlram", validationRequirement, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionOutputValidator", bootstrapSources, StringComparison.Ordinal);
        Assert.DoesNotContain("ValidateCtrlRamFirmwareVersionOutput", bootstrapSources, StringComparison.Ordinal);
    }
}
