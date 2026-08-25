namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Locks runtime-reference compilation and execution to one per-run base snapshot.</summary>
    [Fact]
    public void CtrlRamRuntimeReferenceRouteReusesItsCompilationBaseSnapshot()
    {
        string context = ReadText("src/NvtFwCombiner.Infrastructure/Composition/BuiltInCtrlRamAuthoringAdapter.Context.cs");
        string v2 = ReadText("src/NvtFwCombiner.Infrastructure/Composition/BuiltInCtrlRamAuthoringAdapter.V2.cs");
        string runner = ReadText("src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs");
        string presentation = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.Authoring.cs");
        string authoringInspection = ReadText(
            "src/NvtFwCombiner.Application/Authoring/CtrlRamAuthoringExperience.Inspection.cs");
        string boundedRead = ReadText(
            "src/NvtFwCombiner.Infrastructure/Composition/BuiltInFirmwareInspection.ContentRead.cs");

        Assert.Contains("baseBytes = selectedInputBytes is null", context, StringComparison.Ordinal);
        Assert.Contains(
            "selectedInputBytes.GetValueOrDefault(CompositionSlotIds.ReplaceBase)",
            context,
            StringComparison.Ordinal);
        Assert.DoesNotContain("GetAuthoringCatalog(", presentation, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAuthoringCatalog(", authoringInspection, StringComparison.Ordinal);
        Assert.Equal(
            1,
            CountOccurrences(presentation, "CtrlRamAuthoring.AdoptInspectedBatch("));
        Assert.DoesNotContain("CtrlRamAuthoring.PrepareSession(", presentation, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptedBytes", presentation, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptedByteArray", presentation, StringComparison.Ordinal);
        Assert.Contains(
            "internal static bool IsEquivalentExactCapability(",
            ReadText("src/NvtFwCombiner.Application/Authoring/CompiledAuthoringWorkflow.cs"),
            StringComparison.Ordinal);
        Assert.Contains(
            "CompiledInputArtifactInspectionService.MaximumContentReadBytes",
            boundedRead,
            StringComparison.Ordinal);
        Assert.Contains("FirmwareArtifactPayload referencePayload", v2, StringComparison.Ordinal);
        Assert.DoesNotContain("File.ReadAllBytes(context.BasePath!)", v2, StringComparison.Ordinal);
        Assert.Contains("AcceptedSessionExecutionInputs.CreateBindings(", runner, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyDictionary<string, byte[]> artifacts", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("requiredBaseSha256", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("referencePayload.Sha256", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("File.ReadAllBytes", runner, StringComparison.Ordinal);
        Assert.Contains("AcceptedSessionCompositionExecution.ExecuteAsync", runner, StringComparison.Ordinal);
    }
}
