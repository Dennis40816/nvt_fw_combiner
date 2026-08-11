namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class CanonicalCompositionBoundaryTests : RepositoryBoundaryTestBase
{
    /// <summary>Locks runtime-reference compilation and execution to one per-run base snapshot.</summary>
    [Fact]
    public void CtrlRamRuntimeReferenceRouteReusesItsCompilationBaseSnapshot()
    {
        string context = ReadText("src/NvtFwCombiner.Infrastructure/Composition/BuiltInCtrlRamAuthoringAdapter.Context.cs");
        string v2 = ReadText("src/NvtFwCombiner.Infrastructure/Composition/BuiltInCtrlRamAuthoringAdapter.V2.cs");
        string runner = ReadText("src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs");

        Assert.Contains("baseBytes = selectedInputBytes is null", context, StringComparison.Ordinal);
        Assert.Contains("BuiltInFirmwareInspection.TryReadFirmwareImage(basePath)", context, StringComparison.Ordinal);
        Assert.Contains(
            "selectedInputBytes.GetValueOrDefault(CompositionSlotIds.ReplaceBase)",
            context,
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
